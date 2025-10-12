// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemoryEngine360.
// 
// MemoryEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemoryEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemoryEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using MemEngine360.Engine.Events.XbdmEvents;
using MemEngine360.Xbox360XBDM.Consoles.Xbdm;

namespace MemEngine360.Xbox360XBDM;

public class TestDebuggerEventReceiver {
    public static void Main() {
        Task.Run(async () => {
            // 1. Start listening for the Xbox to connect
            using TcpClient client = new TcpClient() {
                ReceiveTimeout = 0
            };
            await client.ConnectAsync("192.168.1.202", 730);

            using StreamReader cmdReader = new StreamReader(client.GetStream(), Encoding.ASCII);
            string? strresponse = cmdReader.ReadLine()?.ToLower();
            if (strresponse != "201- connected") {
                throw new Exception("Borken");
            }

            XbdmConsoleConnection connection = new XbdmConsoleConnection(client, "192.168.1.202");

            XbdmResponse response = await connection.SendCommand($"debugger connect override name=\"MemoryEngine360\" user=\"{Environment.MachineName}\"");
            if (response.ResponseType != XbdmResponseType.SingleResponse) {
                throw new Exception($"Failed to enable debugger. Response = {response.ToString()}");
            }

            // no idea what reconnectport does, surely it's not the port it tries to reconnect on
            response = await connection.SendCommand($"notify reconnectport=12345");
            if (response.ResponseType != XbdmResponseType.DedicatedConnection) {
                throw new Exception($"Failed to setup notifications. Response type is not {nameof(XbdmResponseType.DedicatedConnection)}: {response.RawMessage}");
            }

            new Thread(() => {
                while (!connection.IsClosed) {
                    string line;
                    try {
                        line = connection.ReadLineFromStream().GetAwaiter().GetResult();
                    }
                    catch (Exception) {
                        continue;
                    }

                    Debug.Assert(line != null);

                    XbdmEventArgs? e = XbdmEventUtils.ParseSpecial(line);
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

            while (!connection.IsClosed) {
                await Task.Delay(500);
            }
        }).Wait();
    }
}