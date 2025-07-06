﻿// 
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

using MemEngine360.Connections.Utils;
using MemEngine360.Engine.Events.XbdmEvents;

namespace MemEngine360.Xbox360XBDM;

public static class XbdmEventUtils {
    public static XbdmEventArgs? ParseSpecial(string text) {
        uint addr, thread, dataAddr, code, rwAddr;
        int cmdLen = text.IndexOf(' ');
        if (cmdLen == -1) {
            return null;
        }

        string cmd = text.Substring(0, cmdLen).ToLower();
        if (cmd.Contains('!')) {
            return new XbdmEventArgsExternal(text);
        }

        ReadOnlySpan<char> textRos = text.AsSpan();
        switch (cmd) {
            case "break":
            case "singlestep":
            case "data": {
                ParamUtils.GetDwParam(textRos, "addr", false, out addr);
                ParamUtils.GetDwParam(textRos, "thread", false, out thread);
                if (cmd != "data") {
                    return new XbdmEventArgsBreakpoint(text, cmd == "break" ? NotificationType.Break : NotificationType.SingleStep) {
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

                    return new XbdmEventArgsDataBreakpoint(text, bType, dataAddr) {
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

                return new XbdmEventArgsException(text) {
                    Flags = flags, IsOnWrite = isWrite,
                    Address = addr, Thread = thread,
                    Code = code, ReadOrWriteAddress = rwAddr
                };
            }
            case "rip": {
                ParamUtils.GetDwParam(textRos, "thread", false, out thread);
                return new XbdmEventArgsRip(text) {
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

                return new XbdmEventArgsAssert(text) {
                    Thread = thread, IsPrompt = isPrompt,
                    String = str
                };
            }
            case "execution": {
                XbdmExecutionState state = XbdmExecutionState.Unknown;
                if (ParamUtils.GetOffsetToValue(textRos, "started", false, false) != -1)
                    state = XbdmExecutionState.Start;
                else if (ParamUtils.GetOffsetToValue(textRos, "stopped", false, false) != -1)
                    state = XbdmExecutionState.Stop;
                else if (ParamUtils.GetOffsetToValue(textRos, "pending", false, false) != -1)
                    state = XbdmExecutionState.Pending;
                else if (ParamUtils.GetOffsetToValue(textRos, "rebooting", false, false) != -1)
                    state = XbdmExecutionState.Reboot;

                return new XbdmEventArgsExecutionState(text, state);
            }
            case "debugstr": {
                string str = "";
                int offset = ParamUtils.GetOffsetToValue(textRos, "string", true, false);
                if (offset != -1) {
                    str = textRos.Slice(offset).ToString();
                }

                ParamUtils.GetDwParam(textRos, "thread", false, out thread);
                bool isStop = ParamUtils.GetOffsetToValue(textRos, "stop", false, false) != -1;

                return new XbdmEventArgsDebugString(text, str) {
                    Thread = thread, IsThreadStop = isStop
                };
            }
        }

        return null;
    }
}