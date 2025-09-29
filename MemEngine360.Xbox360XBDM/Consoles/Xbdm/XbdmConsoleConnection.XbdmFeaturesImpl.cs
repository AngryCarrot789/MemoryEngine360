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

using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Text;
using MemEngine360.Connections;
using MemEngine360.Connections.Features;
using MemEngine360.Engine.Debugging;
using MemEngine360.XboxBase;
using MemEngine360.XboxBase.Modules;
using ConsoleColor = MemEngine360.Connections.Features.ConsoleColor;

namespace MemEngine360.Xbox360XBDM.Consoles.Xbdm;

public partial class XbdmConsoleConnection {
    private class XbdmFeaturesImpl : IConsoleFeature, IFeatureXbox360Xbdm, IFeatureXboxDebugging, IFeatureSystemEvents {
        private readonly XbdmConsoleConnection connection;

        public IConsoleConnection Connection => this.connection;

        public XbdmFeaturesImpl(XbdmConsoleConnection connection) {
            this.connection = connection;
        }

        public Task<XboxThread> GetThreadInfo(uint threadId, bool requireName = true) {
            return this.connection.GetThreadInfo(threadId, requireName);
        }

        public async Task<List<XboxThread>> GetThreadDump(bool requireNames = true) {
            return await this.connection.GetThreadDump(requireNames);
        }

        public Task RebootConsole(bool cold = true) => this.connection.RebootConsole(cold);

        public Task ShutdownConsole() => this.connection.ShutdownConsole();

        public Task EjectDisk() => this.connection.SendCommand("dvdeject");

        public Task<FreezeResult> DebugFreeze() => this.connection.DebugFreeze();

        public Task<UnFreezeResult> DebugUnFreeze() => this.connection.DebugUnFreeze();

        public async Task<bool> IsFrozen() {
            XboxExecutionState state = await this.GetExecutionState();
            return state == XboxExecutionState.Stop;
        }

        public async Task<List<DriveEntry>> GetDriveList() {
            List<DriveEntry> drives = new List<DriveEntry>();
            List<string> list = await this.connection.SendCommandAndReceiveLines("drivelist").ConfigureAwait(false);
            foreach (string drive in list) {
                if (!ParamUtils.GetStrParam(drive, "drivename", true, out string? driveName)) {
                    continue;
                }

                DriveEntry entry = new DriveEntry { Name = driveName + ':' };
                List<string> freeSpaceResponse = await this.connection.SendCommandAndReceiveLines($"drivefreespace name=\"{entry.Name}\\\"");
                if (freeSpaceResponse.Count == 1) {
                    if (ParamUtils.GetDwParam(freeSpaceResponse[0], "totalbyteslo", true, out uint lo) &&
                        ParamUtils.GetDwParam(freeSpaceResponse[0], "totalbyteshi", true, out uint hi)) {
                        entry.TotalSize = ((ulong) hi << 32) | lo;
                    }

                    if (ParamUtils.GetDwParam(freeSpaceResponse[0], "totalfreebyteslo", true, out lo) &&
                        ParamUtils.GetDwParam(freeSpaceResponse[0], "totalfreebyteshi", true, out hi)) {
                        entry.FreeBytes = ((ulong) hi << 32) | lo;
                    }
                }

                drives.Add(entry);
            }

            return drives;
        }

        public async Task<List<FileSystemEntry>> GetFileSystemEntries(string fullPath) {
            this.connection.EnsureNotClosed();
            using BusyToken x = this.connection.CreateBusyToken();

            if (string.IsNullOrEmpty(fullPath))
                throw new FileSystemNoSuchDirectoryException(fullPath);

            if (fullPath[fullPath.Length - 1] != '\\')
                fullPath += '\\';

            XbdmResponse response = await this.connection.InternalSendCommand($"dirlist name=\"{fullPath}\"").ConfigureAwait(false);
            if (response.RawMessage.Contains("access denied"))
                throw new FileSystemAccessDeniedException($"Access denied to {fullPath}");
            if (response.ResponseType != XbdmResponseType.MultiResponse)
                throw new FileSystemNoSuchDirectoryException(fullPath);

            List<FileSystemEntry> entries = new List<FileSystemEntry>();
            List<string> list = await this.connection.InternalReadMultiLineResponse();
            await Task.Run(() => {
                foreach (string entryText in list) {
                    if (!ParamUtils.GetStrParam(entryText, "name", true, out string? name))
                        continue;

                    FileSystemEntry entry = new FileSystemEntry() { Name = name };
                    if (ParamUtils.GetDwParam(entryText, "sizelo", true, out uint sizeLo) &&
                        ParamUtils.GetDwParam(entryText, "sizehi", true, out uint sizeHi)) {
                        entry.Size = ((ulong) sizeHi << 32) | sizeLo;
                    }

                    if (ParamUtils.GetDwParam(entryText, "createlo", true, out uint createLo) &&
                        ParamUtils.GetDwParam(entryText, "createhi", true, out uint createHi)) {
                        entry.CreatedTime = DateTime.FromFileTimeUtc((long) (((ulong) createHi << 32) | createLo));
                    }

                    if (ParamUtils.GetDwParam(entryText, "changelo", true, out uint changeLo) &&
                        ParamUtils.GetDwParam(entryText, "changehi", true, out uint changeHi)) {
                        entry.ModifiedTime = DateTime.FromFileTimeUtc((long) (((ulong) changeHi << 32) | changeLo));
                    }

                    entry.IsDirectory = ParamUtils.GetOffsetToValue(entryText, "directory", false, true) != -1;

                    entries.Add(entry);
                }
            });

            return entries;
        }

        public async Task<bool> DeleteFileSystemEntry(string path) {
            XbdmResponse response = await this.connection.SendCommand($"delete name=\"{path}\"");
            if (response.ResponseType == XbdmResponseType.AccessDenied) {
                response = await this.connection.SendCommand($"delete name=\"{path}\" dir");
            }

            return response.ResponseType == XbdmResponseType.SingleResponse;
        }

        public async Task LaunchFile(string path) {
            string[] lines = path.Split('\\');
            StringBuilder dirSb = new StringBuilder();
            for (int i = 0; i < lines.Length - 1; i++)
                dirSb.Append(lines[i]).Append('\\');
            await this.connection.SendCommand($"magicboot title=\"{path}\" directory=\"{dirSb}\"").ConfigureAwait(false);
        }

        public async Task MoveFile(string oldPath, string newPath) {
            await this.connection.SendCommand($"rename name=\"{oldPath}\" newname=\"{newPath}\"").ConfigureAwait(false);
        }

        public async Task CreateDirectory(string path) {
            await this.connection.SendCommand($"mkdir name=\"{path}\"").ConfigureAwait(false);
        }

        public string GetDirectoryPath(string path) {
            int index = path.LastIndexOf('\\');
            if (index == -1)
                return path;
            return path.Substring(0, index);
        }

        public string GetFileName(string path) {
            int index = path.LastIndexOf('\\');
            if (index == -1)
                return path;
            return path.Substring(index + 1);
        }

        public string JoinPaths(params string[] paths) {
            return Path.Join(paths);
        }

        public string[] SplitPath(string filePath) {
            return filePath.Split('\\');
        }

        public bool IsPathValid(string path) {
            string[] parts = path.Split('\\');
            char[] ch1 = Path.GetInvalidPathChars();
            char[] ch2 = Path.GetInvalidFileNameChars();

            int i = 0;
            if (parts.Length > 1 && parts[0].Contains(':'))
                i++;

            for (; i < parts.Length; i++) {
                string part = parts[i];
                if (part == "\\")
                    return false;
                foreach (char ch in ch1)
                    if (part.Contains(ch))
                        return false;
                foreach (char ch in ch2)
                    if (part.Contains(ch))
                        return false;
            }

            return true;
        }

        public Task<string> GetConsoleID() => this.connection.GetConsoleID();

        public Task<string> GetDebugName() => this.connection.GetDebugName();

        public Task<string?> GetXbeInfo(string? executable) => this.connection.GetXbeInfo(executable);

        public Task<List<MemoryRegion>> GetMemoryRegions(bool willRead, bool willWrite) => this.connection.GetMemoryRegions(willRead, willWrite);

        public Task<XboxExecutionState> GetExecutionState() => this.connection.GetExecutionState();

        public Task<XboxHardwareInfo> GetHardwareInfo() => this.connection.GetHardwareInfo();

        public Task<uint> GetProcessID() => this.connection.GetProcessID();

        public Task<IPAddress> GetTitleIPAddress() => this.connection.GetTitleIPAddress();

        public Task SetConsoleColor(ConsoleColor colour) => this.connection.SetConsoleColor(colour);

        public Task SetDebugName(string newName) => this.connection.SetDebugName(newName);

        public Task AddBreakpoint(uint address) => this.SetBreakpoint(address, false);

        public Task AddDataBreakpoint(uint address, XboxBreakpointType type, uint size) => this.SetDataBreakpoint(address, type, size, false);

        public Task RemoveBreakpoint(uint address) => this.SetBreakpoint(address, true);

        public Task RemoveDataBreakpoint(uint address, XboxBreakpointType type, uint size) => this.SetDataBreakpoint(address, type, size, true);

        public async Task SetBreakpoint(uint address, bool clear) {
            await this.connection.SendCommand($"break addr=0x{address:X8}{(clear ? " clear" : "")}");
        }

        public async Task SetDataBreakpoint(uint address, XboxBreakpointType type, uint size, bool clear) {
            string strType;
            switch (type) {
                case XboxBreakpointType.None:
                case XboxBreakpointType.OnWrite:
                    strType = "write";
                    break;
                case XboxBreakpointType.OnReadWrite: strType = "read"; break;
                case XboxBreakpointType.OnExecuteHW:
                case XboxBreakpointType.OnExecute:
                    strType = "execute";
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            await this.connection.SendCommand($"break {strType}=0x{address:X8} size=0x{size:X8}{(clear ? " clear" : "")}");
        }
        
        public async Task<RegisterContext?> GetThreadRegisters(uint threadId) {
            this.connection.EnsureNotClosed();
            using BusyToken x = this.connection.CreateBusyToken();

            XbdmResponse response = await this.connection.InternalSendCommand($"getcontext thread=0x{threadId:X8} control int fp").ConfigureAwait(false); /* full */
            if (response.ResponseType == XbdmResponseType.NoSuchThread) {
                return null;
            }

            VerifyResponse("getcontext", response.ResponseType, XbdmResponseType.MultiResponse);
            RegisterContext ctx = new RegisterContext();
            await Task.Run(async () => {
                List<string> lines = await this.connection.InternalReadMultiLineResponse().ConfigureAwait(false);
                foreach (string line in lines) {
                    int split = line.IndexOf('=');
                    if (split == -1) {
                        continue;
                    }

                    string name = line.Substring(0, split).ToUpperInvariant();
                    string value = line.Substring(split + 1);
                    if (value.StartsWith("0x")) {
                        if (uint.TryParse(value.AsSpan(2), NumberStyles.HexNumber, null, out uint value32)) {
                            ctx.SetUInt32(name, value32);
                        }
                    }
                    else if (value.StartsWith("0q")) {
                        if (ulong.TryParse(value.AsSpan(2), NumberStyles.HexNumber, null, out ulong value64)) {
                            ctx.SetUInt64(name, value64);
                        }
                    }
                }
            });

            return ctx;
        }

        public Task SuspendThread(uint threadId) => this.connection.SendCommand($"suspend thread=0x{threadId:X8}");

        public Task ResumeThread(uint threadId) => this.connection.SendCommand($"resume thread=0x{threadId:X8}");

        public Task StepThread(uint threadId) {
            // TODO: implement
            return Task.CompletedTask;
        }

        public async Task<ConsoleModule?> GetModuleForAddress(uint address, bool bNeedSections) {
            List<string> modules = await this.connection.SendCommandAndReceiveLines("modules");
            using BusyToken x = this.connection.CreateBusyToken();
            
            foreach (string moduleLine in modules) {
                if (!ParamUtils.GetStrParam(moduleLine, "name", true, out string? name) ||
                    !ParamUtils.GetDwParam(moduleLine, "base", true, out uint modBase) ||
                    !ParamUtils.GetDwParam(moduleLine, "size", true, out uint modSize)) {
                    continue;
                }

                if (address < modBase || address >= modBase + modSize) {
                    continue;
                }

                // ParamUtils.GetDwParam(moduleLine, "timestamp", true, out uint modTimestamp);
                // ParamUtils.GetDwParam(moduleLine, "check", true, out uint modChecksum);
                ParamUtils.GetDwParam(moduleLine, "osize", true, out uint modOriginalSize);
                ParamUtils.GetDwParam(moduleLine, "timestamp", true, out uint timestamp);

                ConsoleModule consoleModule = new ConsoleModule() {
                    Name = name,
                    FullName = null, // unavailable until I can figure out how to get xbeinfo to work
                    BaseAddress = modBase,
                    ModuleSize = modSize,
                    OriginalModuleSize = modOriginalSize,
                    Timestamp = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime
                    // EntryPoint = module.GetEntryPointAddress()
                };

                // 0x10100 = entry point for most things, apart from xboxkrnl.exe
                // format: 
                //   fieldsize=0x<size in uint32 hex>
                //   <value>
                // may return ResponseType.XexFieldNotFound
                XbdmResponse entryPointResponse = await this.connection.InternalSendCommand($"xexfield module=\"{name}\" field=0x10100").ConfigureAwait(false);
                if (entryPointResponse.ResponseType == XbdmResponseType.MultiResponse) {
                    List<string> lines = await this.connection.InternalReadMultiLineResponse();
                    if (lines.Count == 2 && uint.TryParse(lines[1], NumberStyles.HexNumber, null, out uint entryPoint)) {
                        consoleModule.EntryPoint = entryPoint;
                    }
                }

                if (bNeedSections) {
                    XbdmResponse response = await this.connection.InternalSendCommand($"modsections name=\"{name}\"").ConfigureAwait(false);
                    if (response.ResponseType != XbdmResponseType.FileNotFound) {
                        List<string> sections = await this.connection.InternalReadMultiLineResponse();
                        foreach (string sectionLine in sections) {
                            ParamUtils.GetStrParam(sectionLine, "name", true, out string? sec_name);
                            ParamUtils.GetDwParam(sectionLine, "base", true, out uint sec_base);
                            ParamUtils.GetDwParam(sectionLine, "size", true, out uint sec_size);
                            ParamUtils.GetDwParam(sectionLine, "index", true, out uint sec_index);
                            ParamUtils.GetDwParam(sectionLine, "flags", true, out uint sec_flags);

                            consoleModule.Sections.Add(new ConsoleModuleSection() {
                                Name = string.IsNullOrWhiteSpace(sec_name) ? null : sec_name,
                                BaseAddress = sec_base,
                                Size = sec_size,
                                Index = sec_index,
                                Flags = (XboxSectionInfoFlags) sec_flags,
                            });
                        }
                    }
                }

                return consoleModule;
            }

            return null;
        }

        public async Task<FunctionCallEntry?[]> FindFunctions(uint[] iar) {
            if (iar.Length < 1) {
                return [];
            }

            int resolvedCount = 0;
            FunctionCallEntry?[] entries = new FunctionCallEntry?[iar.Length];

            List<string> modules = await this.connection.SendCommandAndReceiveLines("modules");
            using BusyToken x = this.connection.CreateBusyToken();
            
            foreach (string moduleLine in modules) {
                if (!ParamUtils.GetStrParam(moduleLine, "name", true, out string? modName)) {
                    continue;
                }

                XbdmResponse response = await this.connection.InternalSendCommand($"modsections name=\"{modName}\"");
                if (response.ResponseType == XbdmResponseType.FileNotFound) {
                    continue;
                }

                List<string> sections = await this.connection.InternalReadMultiLineResponse();
                foreach (string sectionLine in sections) {
                    ParamUtils.GetStrParam(sectionLine, "name", true, out string? sec_name);
                    if (sec_name != ".pdata") {
                        continue;
                    }

                    ParamUtils.GetDwParam(sectionLine, "base", true, out uint sec_base);
                    ParamUtils.GetDwParam(sectionLine, "size", true, out uint sec_size);

                    int count = (int) sec_size;
                    byte[] buffer = new byte[count];
                    await this.connection.InternalReadBytes(sec_base, buffer, 0, count).ConfigureAwait(false);
                    ReadOnlySpan<byte> rosBuffer = new ReadOnlySpan<byte>(buffer);

                    int functionCount = (int) (sec_size / 16);

                    uint startAddress = BinaryPrimitives.ReadUInt32BigEndian(rosBuffer);
                    for (int j = 0, offset = 8; j < functionCount; j++, offset += 16) {
                        uint endAddress = BinaryPrimitives.ReadUInt32BigEndian(rosBuffer.Slice(offset, 4));
                        uint unwindStuff = BinaryPrimitives.ReadUInt32BigEndian(rosBuffer.Slice(offset, 8));
                        for (int k = 0; k < iar.Length; k++) {
                            if (entries[k] == null && RUNTIME_FUNCTION.Contains(iar[k], startAddress, endAddress)) {
                                entries[k] = new FunctionCallEntry(modName, startAddress, endAddress - startAddress, unwindStuff);
                                resolvedCount++;
                            }
                        }

                        if (resolvedCount == iar.Length) {
                            return entries;
                        }

                        startAddress = endAddress;
                    }

                    break;
                }
            }

            return entries;
        }

        public IDisposable SubscribeToEvents(ConsoleSystemEventHandler handler) => this.connection.SubscribeToEvents(handler);
    }
}