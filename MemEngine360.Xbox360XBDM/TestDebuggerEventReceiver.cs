// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemEngine360.
// 
// MemEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using MemEngine360.Xbox360XBDM.Consoles.Xbdm;
using MemEngine360.Xbox360XBDM.StandardEvents;

namespace MemEngine360.Xbox360XBDM;

public class TestDebuggerEventReceiver {
    public static void Main() {
        Task.Run(async () => {
            // 1. Start listening for the Xbox to connect
            using TcpClient client = new TcpClient() {
                ReceiveTimeout = 5000
            };
            await client.ConnectAsync("192.168.1.202", 730);

            using StreamReader cmdReader = new StreamReader(client.GetStream(), Encoding.ASCII);
            string? strresponse = cmdReader.ReadLine()?.ToLower();
            if (strresponse != "201- connected") {
                throw new Exception("Borken");
            }

            XbdmConsoleConnection connection = new XbdmConsoleConnection(client, cmdReader);

            ConsoleResponse response = await connection.SendCommand($"debugger connect override name=\"{Environment.MachineName}\" user=\"MemEngine360\"");
            if (response.ResponseType != ResponseType.SingleResponse) {
                throw new Exception($"Failed to enable debugger. Response = {response.ToString()}");
            }

            // no idea what reconnectport does, surely it's not the port it tries to reconnect on
            response = await connection.SendCommand($"notify reconnectport=12345");
            if (response.ResponseType != ResponseType.DedicatedConnection) {
                throw new Exception($"Failed to setup notifications. Response type is not {nameof(ResponseType.DedicatedConnection)}: {response.RawMessage}");
            }

            new Thread(() => {
                string line = connection.ReadLineFromStream().GetAwaiter().GetResult();
                if (line != "execution started") {
                    throw new Exception("wut");
                }

                while (connection.IsConnected) {
                    try {
                        line = connection.ReadLineFromStream().AsTask().GetAwaiter().GetResult();
                    }
                    catch (Exception) {
                        continue;
                    }

                    Debug.Assert(line != null);
                    
                    StdEvent? e = Parse(line);
                    if (e != null) {
                        Debug.WriteLine(e);
                    }
                    else {
                        Debug.WriteLine("Unprocessed event. Raw = " + line);
                    }
                }
            }) {
                IsBackground = true, Name = "XBDM Notify Thread"
            }.Start();

            while (connection.IsConnected) {
                await Task.Delay(500);
            }
        }).Wait();
    }

    private static StdEvent? Parse(string text) {
        uint addr, thread, dataAddr, code, rwAddr;
        int cmdLen = text.IndexOf(' ');
        if (cmdLen == -1) {
            return null;
        }

        string cmd = text.Substring(0, cmdLen).ToLower();
        if (cmd.Contains('!')) {
            return new StdEventExternal(text);
        }

        ReadOnlySpan<char> textRos = text.AsSpan();
        switch (cmd) {
            case "break":
            case "singlestep":
            case "data": {
                ParamUtils.GetDwParam(textRos, "addr", false, out addr);
                ParamUtils.GetDwParam(textRos, "thread", false, out thread);
                if (cmd != "data") {
                    return new StdEventBreakpoint(text, cmd == "break" ? NotificationType.Break : NotificationType.SingleStep) {
                        Address = addr, Thread = thread
                    };
                }
                else {
                    BreakType bType;
                    if (ParamUtils.GetDwParam(textRos, "write", false, out dataAddr))
                        bType = BreakType.Write;
                    else if (ParamUtils.GetDwParam(textRos, "read", false, out dataAddr))
                        bType = BreakType.Read;
                    else if (ParamUtils.GetDwParam(textRos, "execute", false, out dataAddr))
                        bType = BreakType.Execute;
                    else
                        bType = BreakType.None;

                    return new StdEventDataBreakpoint(text, bType, dataAddr) {
                        Address = addr, Thread = thread
                    };
                }
            }

            case "exception": {
                if (!ParamUtils.GetDwParam(textRos, "code", false, out code))
                    return null;

                ParamUtils.GetDwParam(textRos, "thread", false, out thread);
                ParamUtils.GetDwParam(textRos, "address", false, out addr);

                ExceptionFlags flags = ExceptionFlags.None;
                if (ParamUtils.GetOffsetToValue(textRos, "first", false, false) != -1)
                    flags |= ExceptionFlags.FirstChance;
                else if (ParamUtils.GetOffsetToValue(textRos, "noncont", false, false) != -1)
                    flags |= ExceptionFlags.NonContinuable;

                bool isWrite = !ParamUtils.GetDwParam(textRos, "read", false, out rwAddr);
                if (isWrite) // not read so try parse write addr
                    isWrite = ParamUtils.GetDwParam(textRos, "write", false, out rwAddr);

                return new StdEventException(text) {
                    Flags = flags, IsOnWrite = isWrite,
                    Address = addr, Thread = thread,
                    Code = code, ReadOrWriteAddress = rwAddr
                };
            }
            case "rip": {
                ParamUtils.GetDwParam(textRos, "thread", false, out thread);
                return new StdEventRip(text, NotificationType.Rip) {
                    Thread = thread
                };
            }
            case "assert": {
                if (!ParamUtils.GetDwParam(textRos, "thread", false, out thread))
                    return null;

                string? str = null;
                bool isPrompt = false;
                int idxStrVal = ParamUtils.GetOffsetToValue(textRos, "string", true, false);
                if (idxStrVal != -1) {
                    // assume the string spans the remainder of szCmd
                    str = textRos.Slice(idxStrVal).ToString();
                }
                else if (ParamUtils.GetOffsetToValue(textRos, "prompt", false, false) != -1) {
                    isPrompt = true;
                }

                return new StdEventAssert(text) {
                    Thread = thread, IsPrompt = isPrompt,
                    String = str
                };
            }
            case "execution": {
                ExecutionState state = ExecutionState.Unknown;
                if (ParamUtils.GetOffsetToValue(textRos, "started", false, false) != -1)
                    state = ExecutionState.Start;
                else if (ParamUtils.GetOffsetToValue(textRos, "stopped", false, false) != -1)
                    state = ExecutionState.Stop;
                else if (ParamUtils.GetOffsetToValue(textRos, "pending", false, false) != -1)
                    state = ExecutionState.Pending;
                else if (ParamUtils.GetOffsetToValue(textRos, "rebooting", false, false) != -1)
                    state = ExecutionState.Reboot;

                return new StdEventExecutionState(text, state);
            }
            case "debugstr": {
                string str = "";
                int offset = ParamUtils.GetOffsetToValue(textRos, "string", true, false);
                if (offset != -1) {
                    str = textRos.Slice(offset).ToString();
                }

                ParamUtils.GetDwParam(textRos, "thread", false, out thread);
                bool isStop = ParamUtils.GetOffsetToValue(textRos, "stop", false, false) != -1;

                return new StdEventDebugString(text, str) {
                    Thread = thread, IsThreadStop = isStop
                };
            }
        }

        return null;
    }
}