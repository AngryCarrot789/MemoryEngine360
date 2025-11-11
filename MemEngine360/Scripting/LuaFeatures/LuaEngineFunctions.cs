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

using System.Globalization;
using Lua;
using MemEngine360.Connections;
using MemEngine360.Connections.Features;
using MemEngine360.Engine;
using MemEngine360.Engine.Debugging;
using MemEngine360.Engine.Modes;
using MemEngine360.ValueAbstraction;
using MemEngine360.XboxBase;

namespace MemEngine360.Scripting.LuaFeatures;

public sealed class LuaEngineFunctions {
    private readonly ILuaMachine machine;

    public LuaEngineFunctions(ILuaMachine machine, LuaState state) {
        this.machine = machine;

        LuaTable engineTable = new LuaTable(0, 20);
        state.Environment[(LuaValue) "engine"] = (LuaValue) engineTable;
        state.LoadedModules[(LuaValue) "engine"] = (LuaValue) engineTable;

        _ = new EngineFunctions(this, state, engineTable);
        _ = new FileSystemFunctions(this, state, engineTable);
        _ = new JRPCFunctions(this, state, engineTable);
        _ = new DebugFunctions(this, state, engineTable);
        _ = new MessageBoxFunctions(this, state);
    }

    internal static void AssignFunction(LuaTable luaTable, LuaFunction function) {
        luaTable[(LuaValue) function.Name] = (LuaValue) function;
    }

    private IConsoleConnection GetConnection(in LuaFunctionExecutionContext ctx) {
        IConsoleConnection? conn = this.machine.Connection;
        if (conn == null)
            throw new LuaRuntimeException(ctx.State.GetTraceback(), "Not connected to console");
        if (conn.IsClosed)
            throw new LuaRuntimeException(ctx.State.GetTraceback(), "Lost connection to console");
        return conn;
    }

    private Task<IBusyToken> GetBusyToken(in LuaFunctionExecutionContext context, CancellationToken ct) {
        return this.GetBusyToken(context.State, ct);
    }

    private async Task<IBusyToken> GetBusyToken(LuaState context, CancellationToken ct) {
        IBusyToken? token = await this.machine.BusyLock.BeginBusyOperation(ct);
        if (token == null) {
            ct.ThrowIfCancellationRequested();
            throw new LuaRuntimeException(context.GetTraceback(), "Failed to obtain network busy token");
        }

        return token;
    }

    private T GetConsoleFeature<T>(in LuaFunctionExecutionContext context, string errorMessage) where T : class, IConsoleFeature {
        IConsoleConnection conn = this.GetConnection(in context);
        if (!conn.TryGetFeature(out T? feature))
            throw new LuaRuntimeException(context.State.GetTraceback(), errorMessage);
        return feature;
    }

    internal sealed class EngineFunctions {
        private readonly LuaEngineFunctions functions;

        public EngineFunctions(LuaEngineFunctions functions, LuaState state, LuaTable engineTable) {
            this.functions = functions;
            AssignFunction(engineTable, new NetworkHandlingLuaFunction("readnumber", this.ReadNumber));
            AssignFunction(engineTable, new NetworkHandlingLuaFunction("writenumber", this.WriteNumber));
            AssignFunction(engineTable, new NetworkHandlingLuaFunction("readstring", this.ReadString));
            AssignFunction(engineTable, new NetworkHandlingLuaFunction("writestring", this.WriteString));
            AssignFunction(engineTable, new NetworkHandlingLuaFunction("readbytes", this.ReadBytes));
            AssignFunction(engineTable, new NetworkHandlingLuaFunction("writebytes", this.WriteBytes));
            AssignFunction(engineTable, new NetworkHandlingLuaFunction("setfrozen", this.SetIsFrozen));
            AssignFunction(engineTable, new NetworkHandlingLuaFunction("isfrozen", this.GetIsFrozen));
            AssignFunction(engineTable, new NetworkHandlingLuaFunction("sendnotification", this.SendNotification));
            AssignFunction(engineTable, new NetworkHandlingLuaFunction("setleds", this.SetLEDs));
        }

        public async ValueTask<int> ReadNumber(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            uint address = LuaUtils.GetUIntFromValue(in context, 0, "readnumber");
            DataType dataType = LuaUtils.GetDataTypeFromString(in context, context.GetArgument<string>(1));
            IConsoleConnection conn = this.functions.GetConnection(in context);
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            LuaValue value;
            switch (dataType) {
                case DataType.Byte:   value = await conn.ReadByte(address); break;
                case DataType.Int16:  value = await conn.ReadValue<short>(address); break;
                case DataType.Int32:  value = await conn.ReadValue<int>(address); break;
                case DataType.Int64:  value = await conn.ReadValue<long>(address); break;
                case DataType.Float:  value = await conn.ReadValue<float>(address); break;
                case DataType.Double: value = await conn.ReadValue<double>(address); break;
                default:              throw new ArgumentOutOfRangeException();
            }

            buffer.Span[0] = value;
            return 1;
        }

        public async ValueTask<int> WriteNumber(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            uint address = LuaUtils.GetUIntFromValue(in context, 0, "writenumber");
            DataType dataType = LuaUtils.GetDataTypeFromString(in context, context.GetArgument<string>(1));
            double d = context.GetArgument<double>(2);
            IDataValue theValue;
            switch (dataType) {
                case DataType.Byte:
                    if (d < byte.MinValue || d > byte.MaxValue)
                        throw LuaUtils.BadArgument(in context, 2, "writenumber", "Value is out of range for type 'byte': " + d);
                    theValue = new DataValueByte((byte) d);
                    break;
                case DataType.Int16:
                    if (d < short.MinValue || d > short.MaxValue)
                        throw LuaUtils.BadArgument(in context, 2, "writenumber", "Value is out of range for type 'short': " + d);
                    theValue = new DataValueInt16((short) d);
                    break;
                case DataType.Int32:
                    if (d < int.MinValue || d > int.MaxValue)
                        throw LuaUtils.BadArgument(in context, 2, "writenumber", "Value is out of range for type 'int': " + d);
                    theValue = new DataValueInt32((int) d);
                    break;
                case DataType.Int64:
                    if (d < long.MinValue || d > long.MaxValue)
                        throw LuaUtils.BadArgument(in context, 2, "writenumber", "Value is out of range for type 'long': " + d);
                    theValue = new DataValueInt64((long) d);
                    break;
                case DataType.Float:
                    if (d < float.MinValue || d > float.MaxValue)
                        throw LuaUtils.BadArgument(in context, 2, "writenumber", "Value is out of range for type 'float': " + d);
                    theValue = new DataValueFloat((float) d);
                    break;
                case DataType.Double: theValue = new DataValueDouble(d); break;
                default:              throw new ArgumentOutOfRangeException();
            }

            IConsoleConnection conn = this.functions.GetConnection(in context);
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            await MemoryEngine.WriteDataValue(conn, address, theValue);
            return 0;
        }

        public async ValueTask<int> ReadString(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            uint address = LuaUtils.GetUIntFromValue(in context, 0, "readstring");
            int count = context.GetArgument<int>(1);
            if (count < 0)
                throw LuaUtils.BadArgument(in context, 1, "readstring", "Cannot read negative length string: " + count);

            if (count == 0) {
                buffer.Span[0] = (LuaValue) string.Empty;
                return 1;
            }

            IConsoleConnection conn = this.functions.GetConnection(in context);
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            string result = await conn.ReadStringASCII(address, count, removeNull: true);
            buffer.Span[0] = (LuaValue) result;
            return 1;
        }
        
        public async ValueTask<int> ReadCString(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            uint address = LuaUtils.GetUIntFromValue(in context, 0, "readcstr");
            IConsoleConnection conn = this.functions.GetConnection(in context);
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            string result = await conn.ReadCString(address, ct);
            buffer.Span[0] = (LuaValue) result;
            return 1;
        }

        public async ValueTask<int> WriteString(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            uint address = LuaUtils.GetUIntFromValue(in context, 0, "writestring");
            string value = context.GetArgument<string>(1);
            if (string.IsNullOrEmpty(value)) {
                return 0; // no point in writing "", since nothing would change
            }

            IConsoleConnection conn = this.functions.GetConnection(in context);
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            await conn.WriteString(address, value);
            return 0;
        }

        public async ValueTask<int> ReadBytes(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            uint address = LuaUtils.GetUIntFromValue(in context, 0, "readbytes");
            uint count = LuaUtils.GetUIntFromValue(in context, 1, "readbytes");
            if (count > short.MaxValue)
                throw LuaUtils.BadArgument(in context, 1, "readbytes", "Too many bytes to read: " + count + " > " + short.MaxValue);

            IConsoleConnection conn = this.functions.GetConnection(in context);
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            LuaTable table = new LuaTable((int) count, 0);
            byte[] data = await conn.ReadBytes(address, (int) count);

            Span<LuaValue> tabSpan = table.GetArraySpan();
            for (int i = 0; i < data.Length; i++) {
                tabSpan[i] = data[i];
            }

            buffer.Span[0] = table;
            return 1;
        }

        public static byte[] ByteArrayFromArgument(LuaFunctionExecutionContext ctx, int index) {
            LuaValue argument = ctx.GetArgument(index);
            if (argument.TryRead(out string text)) {
                string[] elements = text.Split(',', StringSplitOptions.TrimEntries);
                return elements.Select(x => byte.TryParse(x, out byte v)
                    ? v
                    : x.Length > 2 && byte.TryParse(x.AsSpan(2), NumberStyles.HexNumber, null, out v)
                        ? v
                        : throw new LuaRuntimeException(ctx.State.GetTraceback(), $"Invalid argument at {index + 1}: invalid byte format: " + x)).ToArray();
            }

            if (argument.TryRead(out LuaTable table)) {
                byte[] bytes = new byte[table.ArrayLength];
                Span<LuaValue> span = table.GetArraySpan();
                for (int i = 0; i < bytes.Length; i++) {
                    bytes[i] = (byte) span[i].Read<double>();
                }

                return bytes;
            }

            throw new LuaRuntimeException(ctx.State.GetTraceback(), $"Invalid argument at {index + 1}: invalid argument type: " + argument.Type);
        }

        public async ValueTask<int> WriteBytes(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            uint address = LuaUtils.GetUIntFromValue(in context, 0, "writebytes");
            byte[] data = ByteArrayFromArgument(context, 1);

            IConsoleConnection conn = this.functions.GetConnection(in context);
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            await conn.WriteBytes(address, data);

            return 0;
        }

        public async ValueTask<int> SetIsFrozen(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            bool freeze = context.GetArgument<bool>(0);
            IFeatureIceCubes cubes = this.functions.GetConsoleFeature<IFeatureIceCubes>(in context, "Console does not support changing the frozen state");
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            if (freeze) {
                FreezeResult result = await cubes.DebugFreeze();
                buffer.Span[0] = result == FreezeResult.Success;
            }
            else {
                UnFreezeResult result = await cubes.DebugUnFreeze();
                buffer.Span[0] = result == UnFreezeResult.Success;
            }

            return 1;
        }

        public async ValueTask<int> GetIsFrozen(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            IFeatureIceCubesEx cubes = this.functions.GetConsoleFeature<IFeatureIceCubesEx>(in context, "Console does not support querying the frozen state");
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            bool result = await cubes.IsFrozen();
            buffer.Span[0] = result;
            return 1;
        }

        private async ValueTask<int> SendNotification(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            string type = context.GetArgument<string>(0);
            if (!Enum.TryParse(type, true, out XNotifyLogo logoType)) {
                throw LuaUtils.BadArgument(in context, 0, "sendnotification", "Invalid XNotifyLogo: " + type);
            }

            string? message = context.HasArgument(1) ? context.GetArgument<string>(1) : null;

            IFeatureXboxNotifications feature = this.functions.GetConsoleFeature<IFeatureXboxNotifications>(in context, "JRPC2 is not installed");
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            await feature.ShowNotification(logoType, message);
            return 0;
        }

        private async ValueTask<int> SetLEDs(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            bool p1 = context.GetArgument<bool>(0);
            bool p2 = context.GetArgument<bool>(1);
            bool p3 = context.GetArgument<bool>(2);
            bool p4 = context.GetArgument<bool>(3);

            IFeatureXboxJRPC2 jrpc = this.functions.GetConsoleFeature<IFeatureXboxJRPC2>(in context, "JRPC2 not installed");
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            await jrpc.SetLEDs(p1, p2, p3, p4);
            return 0;
        }
    }

    private sealed class FileSystemFunctions {
        private readonly LuaEngineFunctions functions;

        public FileSystemFunctions(LuaEngineFunctions functions, LuaState state, LuaTable engineTable) {
            this.functions = functions;
            AssignFunction(engineTable, new NetworkHandlingLuaFunction("drivelist", this.GetDriveList));
            AssignFunction(engineTable, new NetworkHandlingLuaFunction("getfiles", this.GetFileSystemEntries));
            AssignFunction(engineTable, new NetworkHandlingLuaFunction("deleterecursive", this.DeleteFileOrFolder));
            AssignFunction(engineTable, new NetworkHandlingLuaFunction("launchfile", this.LaunchFile));
            AssignFunction(engineTable, new NetworkHandlingLuaFunction("movefile", this.MoveFile));
            AssignFunction(engineTable, new NetworkHandlingLuaFunction("mkdir", this.CreateDirectory));
            AssignFunction(engineTable, new NetworkHandlingLuaFunction("pathseparator", this.GetPathSeparator));
        }

        private async ValueTask<int> GetDriveList(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            IFeatureFileSystemInfo fsInfo = this.functions.GetConsoleFeature<IFeatureFileSystemInfo>(in context, "Remote file system not supported by connection");
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            List<DriveEntry> result = await fsInfo.GetDriveList();

            string ch = fsInfo.GetPathSeparatorChar().ToString();
            LuaTable table = new LuaTable(result.Count, 0);
            for (int i = 0; i < result.Count; i++) {
                string name = result[i].Name;
                if (!name.EndsWith(ch)) {
                    name += ch;
                }

                table.Insert(i + 1, name);
            }

            buffer.Span[0] = table;
            return 1;
        }

        private async ValueTask<int> GetFileSystemEntries(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            string path = context.GetArgument<string>(0);
            bool files = context.GetArgument<bool>(1);
            bool directories = context.GetArgument<bool>(2);
            if (!files && !directories) {
                buffer.Span[0] = new LuaTable(0, 0);
                return 1;
            }

            IFeatureFileSystemInfo fsInfo = this.functions.GetConsoleFeature<IFeatureFileSystemInfo>(in context, "Remote file system not supported by connection");
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            List<FileSystemEntry> results = await fsInfo.GetFileSystemEntries(path);
            int count = files && !directories
                ? results.Count(x => !x.IsDirectory)
                : !files && directories
                    ? results.Count(x => x.IsDirectory)
                    : results.Count;

            string ch = fsInfo.GetPathSeparatorChar().ToString();
            LuaTable table = new LuaTable(count, 0);
            for (int i = 0; i < results.Count; i++) {
                FileSystemEntry entry = results[i];
                string name = entry.Name;
                if (entry.IsDirectory && !name.EndsWith(ch)) {
                    name += ch;
                }

                table.Insert(i + 1, name);
            }

            buffer.Span[0] = table;
            return 1;
        }

        private async ValueTask<int> DeleteFileOrFolder(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            string path = context.GetArgument<string>(0);

            IFeatureFileSystemInfo fsInfo = this.functions.GetConsoleFeature<IFeatureFileSystemInfo>(in context, "Remote file system not supported by connection");
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            bool result = await fsInfo.DeleteFileSystemEntryRecursive(path);
            buffer.Span[0] = result;
            return 1;
        }

        private async ValueTask<int> LaunchFile(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            string path = context.GetArgument<string>(0);
            IFeatureFileSystemInfo fsInfo = this.functions.GetConsoleFeature<IFeatureFileSystemInfo>(in context, "Remote file system not supported by connection");
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            await fsInfo.LaunchFile(path);
            return 0;
        }

        private async ValueTask<int> MoveFile(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            string oldPath = context.GetArgument<string>(0);
            string newPath = context.GetArgument<string>(1);
            IFeatureFileSystemInfo fsInfo = this.functions.GetConsoleFeature<IFeatureFileSystemInfo>(in context, "Remote file system not supported by connection");
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            await fsInfo.MoveFile(oldPath, newPath);
            return 0;
        }

        private async ValueTask<int> CreateDirectory(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            string dirPath = context.GetArgument<string>(0);
            IFeatureFileSystemInfo fsInfo = this.functions.GetConsoleFeature<IFeatureFileSystemInfo>(in context, "Remote file system not supported by connection");
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            await fsInfo.CreateDirectory(dirPath);
            return 0;
        }

        private ValueTask<int> GetPathSeparator(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            IFeatureFileSystemInfo fsInfo = this.functions.GetConsoleFeature<IFeatureFileSystemInfo>(in context, "Remote file system not supported by connection");

            buffer.Span[0] = fsInfo.GetPathSeparatorChar().ToString();
            return ValueTask.FromResult(1);
        }
    }

    private sealed class JRPCFunctions {
        private readonly LuaEngineFunctions functions;

        public JRPCFunctions(LuaEngineFunctions functions, LuaState state, LuaTable engineTable) {
            this.functions = functions;

            LuaTable jrpcTable = new LuaTable(0, 17);
            state.Environment[(LuaValue) "jrpc"] = (LuaValue) jrpcTable;
            state.LoadedModules[(LuaValue) "jrpc"] = (LuaValue) jrpcTable;
            AssignFunction(jrpcTable, new NetworkHandlingLuaFunction("getprocaddress", this.GetProcedureAddress));
            AssignFunction(jrpcTable, new NetworkHandlingLuaFunction("callvoidat", this.CallVoidAt));
            AssignFunction(jrpcTable, new NetworkHandlingLuaFunction("callvoidin", this.CallVoidIn));
            AssignFunction(jrpcTable, new NetworkHandlingLuaFunction("callvoidat_vm", this.CallVoidAt_VM));
            AssignFunction(jrpcTable, new NetworkHandlingLuaFunction("callvoidin_vm", this.CallVoidIn_VM));
            AssignFunction(jrpcTable, new NetworkHandlingLuaFunction("callvoidat_sys", this.CallVoidAt_System));
            AssignFunction(jrpcTable, new NetworkHandlingLuaFunction("callvoidin_sys", this.CallVoidIn_System));
            AssignFunction(jrpcTable, new NetworkHandlingLuaFunction("callvoidat_vm_sys", this.CallVoidAt_VM_System));
            AssignFunction(jrpcTable, new NetworkHandlingLuaFunction("callvoidin_vm_sys", this.CallVoidIn_VM_System));
            AssignFunction(jrpcTable, new NetworkHandlingLuaFunction("callat", this.CallAt));
            AssignFunction(jrpcTable, new NetworkHandlingLuaFunction("callin", this.CallIn));
            AssignFunction(jrpcTable, new NetworkHandlingLuaFunction("callat_vm", this.CallAt_VM));
            AssignFunction(jrpcTable, new NetworkHandlingLuaFunction("callin_vm", this.CallIn_VM));
            AssignFunction(jrpcTable, new NetworkHandlingLuaFunction("callat_sys", this.CallAt_System));
            AssignFunction(jrpcTable, new NetworkHandlingLuaFunction("callin_sys", this.CallIn_System));
            AssignFunction(jrpcTable, new NetworkHandlingLuaFunction("callat_vm_sys", this.CallAt_VM_System));
            AssignFunction(jrpcTable, new NetworkHandlingLuaFunction("callin_vm_sys", this.CallIn_VM_System));
        }

        private async ValueTask<int> GetProcedureAddress(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            string modName = context.GetArgument<string>(0);
            uint ordinal = LuaUtils.GetHexNumber(context, 1, "getprocaddress");
            IFeatureXboxJRPC2 jrpc = this.functions.GetConsoleFeature<IFeatureXboxJRPC2>(in context, "JRPC2 not installed");
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            uint address = await jrpc.ResolveFunction(modName, ordinal);
            if (address == 0) {
                buffer.Span[0] = LuaValue.Nil;
            }
            else {
                buffer.Span[0] = address;
            }

            return 1;
        }

        #region Delegates

        private async ValueTask<int> CallVoidAt(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            await this.DoCallVoidAt(context, vm: false, system: false, ct);
            return 0;
        }

        private async ValueTask<int> CallVoidIn(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            await this.DoCallVoidIn(context, vm: false, system: false, ct);
            return 0;
        }

        private async ValueTask<int> CallVoidAt_VM(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            await this.DoCallVoidAt(context, vm: true, system: false, ct);
            return 0;
        }

        private async ValueTask<int> CallVoidIn_VM(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            await this.DoCallVoidIn(context, vm: true, system: false, ct);
            return 0;
        }

        private async ValueTask<int> CallVoidAt_System(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            await this.DoCallVoidAt(context, vm: false, system: true, ct);
            return 0;
        }

        private async ValueTask<int> CallVoidIn_System(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            await this.DoCallVoidIn(context, vm: false, system: true, ct);
            return 0;
        }

        private async ValueTask<int> CallVoidAt_VM_System(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            await this.DoCallVoidAt(context, vm: true, system: true, ct);
            return 0;
        }

        private async ValueTask<int> CallVoidIn_VM_System(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            await this.DoCallVoidIn(context, vm: true, system: true, ct);
            return 0;
        }

        private async ValueTask<int> CallAt(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            return UnwrapValue(await this.DoCallAt(context, vm: false, system: false, ct), buffer);
        }

        private async ValueTask<int> CallIn(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            return UnwrapValue(await this.DoCallIn(context, vm: false, system: false, ct), buffer);
        }

        private async ValueTask<int> CallAt_VM(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            return UnwrapValue(await this.DoCallAt(context, vm: true, system: false, ct), buffer);
        }

        private async ValueTask<int> CallIn_VM(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            return UnwrapValue(await this.DoCallIn(context, vm: true, system: false, ct), buffer);
        }

        private async ValueTask<int> CallAt_System(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            return UnwrapValue(await this.DoCallAt(context, vm: false, system: true, ct), buffer);
        }

        private async ValueTask<int> CallIn_System(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            return UnwrapValue(await this.DoCallIn(context, vm: false, system: true, ct), buffer);
        }

        private async ValueTask<int> CallAt_VM_System(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            return UnwrapValue(await this.DoCallAt(context, vm: true, system: true, ct), buffer);
        }

        private async ValueTask<int> CallIn_VM_System(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            return UnwrapValue(await this.DoCallIn(context, vm: true, system: true, ct), buffer);
        }

        private static int UnwrapValue(object value, Memory<LuaValue> buffer) {
            switch (value) {
                case byte v: {
                    buffer.Span[0] = v;
                    return 1;
                }
                case int v: {
                    buffer.Span[0] = v;
                    return 1;
                }
                case ulong v: {
                    buffer.Span[0] = v;
                    return 1;
                }
                case float v: {
                    buffer.Span[0] = v;
                    return 1;
                }
                case byte[] a: {
                    LuaTable table = new LuaTable(a.Length, 0);
                    Span<LuaValue> tabSpan = table.GetArraySpan();
                    for (int i = 0; i < a.Length; i++)
                        tabSpan[i] = a[i];
                    buffer.Span[0] = table;
                    return 1;
                }
                case int[] a: {
                    LuaTable table = new LuaTable(a.Length, 0);
                    Span<LuaValue> tabSpan = table.GetArraySpan();
                    for (int i = 0; i < a.Length; i++)
                        tabSpan[i] = a[i];
                    buffer.Span[0] = table;
                    return 1;
                }
                case ulong[] a: {
                    LuaTable table = new LuaTable(a.Length, 0);
                    Span<LuaValue> tabSpan = table.GetArraySpan();
                    for (int i = 0; i < a.Length; i++)
                        tabSpan[i] = a[i];
                    buffer.Span[0] = table;
                    return 1;
                }
                case float[] a: {
                    LuaTable table = new LuaTable(a.Length, 0);
                    Span<LuaValue> tabSpan = table.GetArraySpan();
                    for (int i = 0; i < a.Length; i++)
                        tabSpan[i] = a[i];
                    buffer.Span[0] = table;
                    return 1;
                }
                case string a: {
                    buffer.Span[0] = a;
                    return 1;
                }
            }

            // !!! who knows
            buffer.Span[0] = new LuaValue(value);
            return 1;
        }

        #endregion

        private async Task DoCallVoidAt(LuaFunctionExecutionContext context, bool vm, bool system, CancellationToken ct) {
            IFeatureXboxJRPC2 jrpc = this.functions.GetConsoleFeature<IFeatureXboxJRPC2>(in context, "JRPC2 not supported on the console");
            uint address = LuaUtils.GetUIntFromValue(in context, 0, "callvoid<X>");
            object[] args = new object[context.ArgumentCount - 1];
            for (int i = 0; i < args.Length; i++) {
                args[i] = ParseValue(context, i + 1).Item2;
            }

            ThreadType type = system ? ThreadType.System : ThreadType.Title;
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);
            await (vm ? jrpc.CallVMVoid(type, address, args) : jrpc.CallVoid(type, address, args));
        }

        private async Task<object> DoCallAt(LuaFunctionExecutionContext context, bool vm, bool system, CancellationToken ct) {
            IFeatureXboxJRPC2 jrpc = this.functions.GetConsoleFeature<IFeatureXboxJRPC2>(in context, "JRPC2 not supported on the console");
            RPCDataType returnType = ParseDataType(in context, context.GetArgument<string>(0), out int arraySize);
            uint address = LuaUtils.GetUIntFromValue(in context, 1, "call<X>");
            object[] args = new object[context.ArgumentCount - 2];
            for (int i = 0; i < args.Length; i++) {
                args[i] = ParseValue(context, i + 2).Item2;
            }

            ThreadType type = system ? ThreadType.System : ThreadType.Title;
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);
            switch (returnType) {
                case RPCDataType.Byte:        return await (vm ? jrpc.CallVM<byte>(type, address, args) : jrpc.Call<byte>(type, address, args));
                case RPCDataType.Int:         return await (vm ? jrpc.CallVM<int>(type, address, args) : jrpc.Call<int>(type, address, args));
                case RPCDataType.Uint64:      return await (vm ? jrpc.CallVM<ulong>(type, address, args) : jrpc.Call<ulong>(type, address, args));
                case RPCDataType.Float:       return await (vm ? jrpc.CallVM<float>(type, address, args) : jrpc.Call<float>(type, address, args));
                case RPCDataType.ByteArray:   return await (vm ? jrpc.CallVMArray<byte>(type, address, (uint) arraySize, args) : jrpc.CallArray<byte>(type, address, (uint) arraySize, args));
                case RPCDataType.IntArray:    return await (vm ? jrpc.CallVMArray<int>(type, address, (uint) arraySize, args) : jrpc.CallArray<int>(type, address, (uint) arraySize, args));
                case RPCDataType.Uint64Array: return await (vm ? jrpc.CallVMArray<ulong>(type, address, (uint) arraySize, args) : jrpc.CallArray<ulong>(type, address, (uint) arraySize, args));
                case RPCDataType.FloatArray:  return await (vm ? jrpc.CallVMArray<float>(type, address, (uint) arraySize, args) : jrpc.CallArray<float>(type, address, (uint) arraySize, args));
                case RPCDataType.String:      return await (vm ? jrpc.CallVMString(type, address, args) : jrpc.CallString(type, address, args));
                default:                      throw new ArgumentOutOfRangeException();
            }
        }

        private async Task DoCallVoidIn(LuaFunctionExecutionContext context, bool vm, bool system, CancellationToken ct) {
            IFeatureXboxJRPC2 jrpc = this.functions.GetConsoleFeature<IFeatureXboxJRPC2>(in context, "JRPC2 not supported on the console");
            string module = context.GetArgument<string>(0);
            uint ordinal = LuaUtils.GetUIntFromValue(in context, 1, "callvoid<X>");
            object[] args = new object[context.ArgumentCount - 2];
            for (int i = 0; i < args.Length; i++) {
                args[i] = ParseValue(context, i + 2).Item2;
            }

            ThreadType type = system ? ThreadType.System : ThreadType.Title;
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);
            await (vm ? jrpc.CallVMVoid(type, module, (int) ordinal, args) : jrpc.CallVoid(type, module, (int) ordinal, args));
        }

        private async Task<object> DoCallIn(LuaFunctionExecutionContext context, bool vm, bool system, CancellationToken ct) {
            IFeatureXboxJRPC2 jrpc = this.functions.GetConsoleFeature<IFeatureXboxJRPC2>(in context, "JRPC2 not supported on the console");
            RPCDataType returnType = ParseDataType(in context, context.GetArgument<string>(0), out int arraySize);
            string module = context.GetArgument<string>(1);
            uint ordinal = LuaUtils.GetUIntFromValue(in context, 2, "callin<X>");
            object[] args = new object[context.ArgumentCount - 3];
            for (int i = 0; i < args.Length; i++) {
                args[i] = ParseValue(context, i + 3).Item2;
            }

            ThreadType type = system ? ThreadType.System : ThreadType.Title;
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);
            switch (returnType) {
                case RPCDataType.Byte:        return await (vm ? jrpc.CallVM<byte>(type, module, (int) ordinal, args) : jrpc.Call<byte>(type, module, (int) ordinal, args));
                case RPCDataType.Int:         return await (vm ? jrpc.CallVM<int>(type, module, (int) ordinal, args) : jrpc.Call<int>(type, module, (int) ordinal, args));
                case RPCDataType.Uint64:      return await (vm ? jrpc.CallVM<ulong>(type, module, (int) ordinal, args) : jrpc.Call<ulong>(type, module, (int) ordinal, args));
                case RPCDataType.Float:       return await (vm ? jrpc.CallVM<float>(type, module, (int) ordinal, args) : jrpc.Call<float>(type, module, (int) ordinal, args));
                case RPCDataType.ByteArray:   return await (vm ? jrpc.CallVMArray<byte>(type, module, (int) ordinal, (uint) arraySize, args) : jrpc.CallArray<byte>(type, module, (int) ordinal, (uint) arraySize, args));
                case RPCDataType.IntArray:    return await (vm ? jrpc.CallVMArray<int>(type, module, (int) ordinal, (uint) arraySize, args) : jrpc.CallArray<int>(type, module, (int) ordinal, (uint) arraySize, args));
                case RPCDataType.Uint64Array: return await (vm ? jrpc.CallVMArray<ulong>(type, module, (int) ordinal, (uint) arraySize, args) : jrpc.CallArray<ulong>(type, module, (int) ordinal, (uint) arraySize, args));
                case RPCDataType.FloatArray:  return await (vm ? jrpc.CallVMArray<float>(type, module, (int) ordinal, (uint) arraySize, args) : jrpc.CallArray<float>(type, module, (int) ordinal, (uint) arraySize, args));
                case RPCDataType.String:      return await (vm ? jrpc.CallVMString(type, module, (int) ordinal, args) : jrpc.CallString(type, module, (int) ordinal, args));
                default:                      throw new ArgumentOutOfRangeException();
            }
        }

        private static RPCDataType ParseDataType(in LuaFunctionExecutionContext ctx, string value, out int arraySize) {
            arraySize = 0;
            switch (value.ToUpperInvariant()) {
                case "BYTE":   return RPCDataType.Byte;
                case "INT":    return RPCDataType.Int;
                case "ULONG":  return RPCDataType.Uint64;
                case "FLOAT":  return RPCDataType.Float;
                case "STRING": return RPCDataType.String;
            }

            int idx1 = value.IndexOf('[');
            if (idx1 == -1)
                throw new LuaRuntimeException(ctx.State.GetTraceback(), $"Unknown data type: " + value + ". Must be one of byte, int, ulong, float, byte[], int[], ulong[], float[], string");

            int idx2 = value.IndexOf(']', idx1);
            if (idx2 == -1)
                throw new LuaRuntimeException(ctx.State.GetTraceback(), $"Array data type '" + value + "' is missing a closed square bracket");

            ReadOnlySpan<char> lengthText = value.AsSpan(idx1, idx2 - idx1);
            if (lengthText.Length < 1)
                throw new LuaRuntimeException(ctx.State.GetTraceback(), $"Array data type '" + value + "' is missing the array length between the brackets");
            if (!int.TryParse(lengthText, out arraySize))
                throw new LuaRuntimeException(ctx.State.GetTraceback(), $"Array data type '" + value + "' contains an invalid length between the square brackets: " + lengthText.ToString());

            string actualType = value.Substring(0, idx1);
            switch (actualType.ToUpperInvariant()) {
                case "BYTE":  return RPCDataType.ByteArray;
                case "INT":   return RPCDataType.IntArray;
                case "ULONG": return RPCDataType.Uint64Array;
                case "FLOAT": return RPCDataType.FloatArray;
            }

            throw new LuaRuntimeException(ctx.State.GetTraceback(), $"Unknown array data type: " + actualType + ". Must be one of byte[], int[], ulong[], float[]");
        }

        private static (RPCDataType, object) ParseValue(LuaFunctionExecutionContext ctx, int index) {
            LuaTable table = ctx.GetArgument<LuaTable>(index);
            if (table.ArrayLength != 2) {
                throw new LuaRuntimeException(ctx.State.GetTraceback(), "Expected array table as JRPC argument with two values: 'type : string', 'value : any'");
            }

            LuaValue value1 = table.GetArraySpan()[0];
            LuaValue value2 = table.GetArraySpan()[1];
            if (!value1.TryRead(out string type)) {
                throw new LuaRuntimeException(ctx.State.GetTraceback(), "JRPC argument table must contain string as the type. Got " + value1 + " instead");
            }

            string typeUpper = type.ToUpperInvariant();
            switch (typeUpper) {
                case "BYTE":
                    if (value2.TryRead(out int val1)) {
                        if (val1 < byte.MinValue || val1 > byte.MaxValue)
                            throw new LuaRuntimeException(ctx.State.GetTraceback(), $"JRPC argument table {index + 1} is out of range for byte: " + val1);
                        return (RPCDataType.Byte, (byte) val1);
                    }
                    else if (value2.TryRead(out string strVal1))
                        if (byte.TryParse(strVal1, out byte bVal1) || (strVal1.Length > 2 && byte.TryParse(strVal1.AsSpan(2), NumberStyles.HexNumber, null, out bVal1)))
                            return (RPCDataType.Byte, bVal1);

                    throw new LuaRuntimeException(ctx.State.GetTraceback(), $"JRPC argument table {index + 1} does not contain a byte. Got " + value2 + " instead");
                case "INT":
                    if (value2.TryRead(out long val2)) {
                        // Might be signed or unsigned so accept full int range
                        if (val2 < int.MinValue || val2 > uint.MaxValue)
                            throw new LuaRuntimeException(ctx.State.GetTraceback(), $"JRPC argument table {index + 1} is out of range for byte: " + val2);
                        return (RPCDataType.Int, (int) val2);
                    }
                    else if (value2.TryRead(out string strVal2))
                        if (int.TryParse(strVal2, out int lVal2) || (strVal2.Length > 2 && int.TryParse(strVal2.AsSpan(2), NumberStyles.HexNumber, null, out lVal2)))
                            return (RPCDataType.Int, lVal2);

                    throw new LuaRuntimeException(ctx.State.GetTraceback(), $"JRPC argument table {index + 1} does not contain an int. Got " + value2 + " instead");
                case "ULONG":
                    if (value2.TryRead(out long val3))
                        return (RPCDataType.Uint64, (ulong) val3);
                    else if (value2.TryRead(out string strVal3))
                        if (ulong.TryParse(strVal3, out ulong ulVal3) || (strVal3.Length > 2 && ulong.TryParse(strVal3.AsSpan(2), NumberStyles.HexNumber, null, out ulVal3)))
                            return (RPCDataType.Uint64, ulVal3);
                    throw new LuaRuntimeException(ctx.State.GetTraceback(), $"JRPC argument table {index + 1} does not contain a ulong. Got " + value2 + " instead");
                case "FLOAT":
                    if (value2.TryRead(out float val4))
                        return (RPCDataType.Float, val4);
                    else if (value2.TryRead(out string strVal4) && float.TryParse(strVal4, out float flVal4))
                        return (RPCDataType.Float, flVal4);
                    throw new LuaRuntimeException(ctx.State.GetTraceback(), $"JRPC argument table {index + 1} does not contain a float. Got " + value2 + " instead");
                case "BYTE[]":
                case "INT[]":
                case "ULONG[]":
                case "FLOAT[]":
                    if (value2.TryRead(out string val5)) {
                        string[] elements = val5.Split(',', StringSplitOptions.TrimEntries);
                        switch (typeUpper) {
                            case "BYTE[]":  return (RPCDataType.ByteArray, elements.Select(x => byte.TryParse(x, out byte v) ? v : (x.Length > 2 && byte.TryParse(x.AsSpan(2), NumberStyles.HexNumber, null, out v)) ? v : throw new LuaRuntimeException(ctx.State.GetTraceback(), $"JRPC argument table {index + 1}'s value contains an invalid byte: " + x)).ToArray());
                            case "INT[]":   return (RPCDataType.IntArray, elements.Select(x => int.TryParse(x, out int v) ? v : (x.Length > 2 && int.TryParse(x.AsSpan(2), NumberStyles.HexNumber, null, out v)) ? v : throw new LuaRuntimeException(ctx.State.GetTraceback(), $"JRPC argument table {index + 1}'s value contains an invalid int: " + x)).ToArray());
                            case "ULONG[]": return (RPCDataType.Uint64Array, elements.Select(x => ulong.TryParse(x, out ulong v) ? v : (x.Length > 2 && ulong.TryParse(x.AsSpan(2), NumberStyles.HexNumber, null, out v)) ? v : throw new LuaRuntimeException(ctx.State.GetTraceback(), $"JRPC argument table {index + 1}'s value contains an invalid ulong: " + x)).ToArray());
                            case "FLOAT[]": return (RPCDataType.FloatArray, elements.Select(x => float.TryParse(x, out float v) ? v : throw new LuaRuntimeException(ctx.State.GetTraceback(), $"JRPC argument table {index + 1}'s value contains an invalid float: " + x)).ToArray());
                            default:        throw new Exception("Fatal error");
                        }
                    }
                    else if (value2.TryRead(out LuaTable val5tab)) {
                        LuaValue[] values = val5tab.GetArraySpan().Slice(val5tab.ArrayLength).ToArray();
                        switch (typeUpper) {
                            case "BYTE[]":
                                return (RPCDataType.ByteArray, values.Select(x => {
                                    long d = x.Read<long>();
                                    if (d < 0 || d > 255)
                                        throw new LuaRuntimeException(ctx.State.GetTraceback(), "");
                                    return (byte) d;
                                }).ToArray());
                            case "INT[]":
                                return (RPCDataType.IntArray, values.Select(x => {
                                    long d = x.Read<long>();
                                    if (d < int.MinValue || d > uint.MaxValue)
                                        throw new LuaRuntimeException(ctx.State.GetTraceback(), "");
                                    return (int) d;
                                }).ToArray());
                            case "ULONG[]": return (RPCDataType.Uint64Array, values.Select(x => (ulong) x.Read<long>()).ToArray());
                            case "FLOAT[]": return (RPCDataType.FloatArray, values.Select(x => x.Read<float>()).ToArray());
                            default:        throw new Exception("Fatal error");
                        }
                    }

                    throw new LuaRuntimeException(ctx.State.GetTraceback(), $"JRPC argument table {index + 1} does not contain a comma separated string containing the array elements. Got " + value2 + " instead");
                case "STRING":
                    if (!value2.TryRead(out string val6))
                        throw new LuaRuntimeException(ctx.State.GetTraceback(), $"JRPC argument table {index + 1} does not contain a string. Got " + value2 + " instead");
                    return (RPCDataType.String, val6);
                default: throw new LuaRuntimeException(ctx.State.GetTraceback(), $"JRPC argument table {index + 1} contains an unknown data type: " + type + ". Must be one of byte, int, ulong, float, byte[], int[], ulong[], float[], string");
            }
        }
    }

    private sealed class DebugFunctions {
        private readonly LuaEngineFunctions functions;

        public DebugFunctions(LuaEngineFunctions functions, LuaState state, LuaTable engineTable) {
            this.functions = functions;

            LuaTable debugTable = new LuaTable(0, 17);
            state.Environment[(LuaValue) "debug"] = (LuaValue) debugTable;
            state.LoadedModules[(LuaValue) "debug"] = (LuaValue) debugTable;
            AssignFunction(debugTable, new NetworkHandlingLuaFunction("add_breakpoint", this.AddBreakpoint));
            AssignFunction(debugTable, new NetworkHandlingLuaFunction("remove_breakpoint", this.RemoveBreakpoint));
            AssignFunction(debugTable, new NetworkHandlingLuaFunction("set_data_breakpoint", this.SetDataBreakpoint));
            AssignFunction(debugTable, new NetworkHandlingLuaFunction("incr_suspend", this.SuspendThread));
            AssignFunction(debugTable, new NetworkHandlingLuaFunction("decr_suspend", this.ResumeThread));
            AssignFunction(debugTable, new NetworkHandlingLuaFunction("find_functions", this.FindFunctions));
            /*

             */
        }

        private async ValueTask<int> AddBreakpoint(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            IFeatureXboxDebugging debug = this.functions.GetConsoleFeature<IFeatureXboxDebugging>(in context, "Debugging not supported by connection");
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            uint address = LuaUtils.GetUIntFromValue(in context, 0, "add_breakpoint");
            await debug.AddBreakpoint(address);
            return 0;
        }

        private async ValueTask<int> RemoveBreakpoint(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            IFeatureXboxDebugging debug = this.functions.GetConsoleFeature<IFeatureXboxDebugging>(in context, "Debugging not supported by connection");
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            uint address = LuaUtils.GetUIntFromValue(in context, 0, "remove_breakpoint");
            await debug.RemoveBreakpoint(address);
            return 0;
        }

        private async ValueTask<int> SetDataBreakpoint(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            IFeatureXboxDebugging debug = this.functions.GetConsoleFeature<IFeatureXboxDebugging>(in context, "Debugging not supported by connection");
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            uint address = LuaUtils.GetUIntFromValue(in context, 0, "set_data_breakpoint");
            string type = context.GetArgument<string>(1);
            XboxBreakpointType breakType = type switch {
                "w" => XboxBreakpointType.OnWrite,
                "rw" => XboxBreakpointType.OnReadOrWrite,
                "exec" => XboxBreakpointType.OnExecute,
                _ => XboxBreakpointType.None
            };

            uint size = LuaUtils.GetUIntFromValue(in context, 2, "set_data_breakpoint");
            if (size != 1 && size != 2 && size != 4) {
                throw LuaUtils.BadArgument(in context, 2, "set_data_breakpoint", "Size must be 1, 2 or 4");
            }

            await debug.SetDataBreakpoint(address, breakType, size);
            return 0;
        }

        private async ValueTask<int> SuspendThread(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            IFeatureXboxDebugging debug = this.functions.GetConsoleFeature<IFeatureXboxDebugging>(in context, "Debugging not supported by connection");
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            uint threadId = LuaUtils.GetUIntFromValue(in context, 0, "incr_suspend");
            await debug.SuspendThread(threadId);
            return 0;
        }

        private async ValueTask<int> ResumeThread(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            IFeatureXboxDebugging debug = this.functions.GetConsoleFeature<IFeatureXboxDebugging>(in context, "Debugging not supported by connection");
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            uint threadId = LuaUtils.GetUIntFromValue(in context, 0, "decr_suspend");
            await debug.ResumeThread(threadId);
            return 0;
        }

        private async ValueTask<int> FindFunctions(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
            IFeatureXboxDebugging debug = this.functions.GetConsoleFeature<IFeatureXboxDebugging>(in context, "Debugging not supported by connection");
            using IBusyToken token = await this.functions.GetBusyToken(in context, ct);

            List<uint> args = new List<uint>();
            for (int i = 0; i < context.ArgumentCount; i++) {
                args.Add(LuaUtils.GetUIntFromValue(in context, i, "find_functions"));
            }

            FunctionCallEntry?[] results = await debug.FindFunctions(args.ToArray());
            LuaTable table = new LuaTable(results.Length, 0);
            buffer.Span[0] = table;
            Span<LuaValue> tableSpan = table.GetArraySpan();

            for (int i = 0; i < results.Length; i++) {
                FunctionCallEntry? entry = results[i];
                if (entry == null) {
                    buffer.Span[i] = LuaValue.Nil;
                }
                else {
                    LuaTable subTable = new LuaTable(0, 4);
                    subTable["module"] = entry.ModuleName;
                    subTable["address"] = entry.Address;
                    subTable["size"] = entry.Size;
                    subTable["unwind_info"] = entry.unwindInfoAddressOrData;
                    tableSpan[i] = subTable;
                }
            }

            return 1;
        }
    }

    private sealed class MessageBoxFunctions {
        private readonly LuaEngineFunctions functions;


        // Coming soon!
        public MessageBoxFunctions(LuaEngineFunctions functions, LuaState state) {
            this.functions = functions;

            // LuaTable fileSystemTable = new LuaTable(0, 7);
            // state.Environment[(LuaValue) "msgbox"] = (LuaValue) fileSystemTable;
            // state.LoadedModules[(LuaValue) "msgbox"] = (LuaValue) fileSystemTable;
            // AssignFunction(fileSystemTable, new IOTimeoutHandlingLuaFunction("showOkCancel", this.ShowOkCancel));
            // AssignFunction(fileSystemTable, new IOTimeoutHandlingLuaFunction("showYesNo", this.ShowOkCancel));
            // AssignFunction(fileSystemTable, new IOTimeoutHandlingLuaFunction("showYesNoCancel", this.ShowOkCancel));
        }

        // private ValueTask<int> ShowOkCancel(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
        //     IFeatureFileSystemInfo fsInfo = this.functions.GetConsoleFeature<IFeatureFileSystemInfo>(ref context, "Remote file system not supported by connection");
        //     buffer.Span[0] = fsInfo.GetPathSeparatorChar().ToString();
        //     return ValueTask.FromResult(1);
        // }
    }

    private sealed class NetworkHandlingLuaFunction : LuaFunction {
        public NetworkHandlingLuaFunction(string name, Func<LuaFunctionExecutionContext, Memory<LuaValue>, CancellationToken, ValueTask<int>> func) : base(name, async (ctx, buffer, ct) => {
            try {
                return await func(ctx, buffer, ct);
            }
            catch (Exception e) when (e is TimeoutException || e is IOException) {
                throw new LuaRuntimeException(ctx.State.GetTraceback(), e.Message);
            }
        }) {
        }
    }
}