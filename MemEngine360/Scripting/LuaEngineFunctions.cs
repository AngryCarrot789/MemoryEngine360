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
using MemEngine360.Engine.Modes;
using MemEngine360.ValueAbstraction;

namespace MemEngine360.Scripting;

public sealed class LuaEngineFunctions {
    private readonly LuaScriptMachine machine;

    public LuaEngineFunctions(LuaScriptMachine machine) {
        this.machine = machine;
    }

    public void Install(LuaState state) {
        LuaTable luaTable = new LuaTable(0, 12);
        AssignFunction(luaTable, new LuaFunction("readnumber", this.ReadNumber));
        AssignFunction(luaTable, new LuaFunction("writenumber", this.WriteNumber));
        AssignFunction(luaTable, new LuaFunction("readstring", this.ReadString));
        AssignFunction(luaTable, new LuaFunction("writestring", this.WriteString));
        AssignFunction(luaTable, new LuaFunction("setfrozen", this.SetIsFrozen));
        AssignFunction(luaTable, new LuaFunction("isfrozen", this.GetIsFrozen));
        AssignFunction(luaTable, new LuaFunction("sendnotification", this.SendNotification));
        AssignFunction(luaTable, new LuaFunction("drivelist", this.GetDriveList));
        AssignFunction(luaTable, new LuaFunction("getfiles", this.GetFileSystemEntries));
        AssignFunction(luaTable, new LuaFunction("deleterecursive", this.DeleteFileOrFolder));
        AssignFunction(luaTable, new LuaFunction("launchfile", this.LaunchFile));
        AssignFunction(luaTable, new LuaFunction("movefile", this.MoveFile));
        AssignFunction(luaTable, new LuaFunction("mkdir", this.CreateDirectory));
        AssignFunction(luaTable, new LuaFunction("pathseparator", this.GetPathSeparator));
        state.Environment[(LuaValue) "engine"] = (LuaValue) luaTable;
        state.LoadedModules[(LuaValue) "engine"] = (LuaValue) luaTable;
    }

    private static void AssignFunction(LuaTable luaTable, LuaFunction function) {
        luaTable[(LuaValue) function.Name] = (LuaValue) function;
    }

    public async ValueTask<int> ReadNumber(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
        uint address = GetAddressFromValue(ref context, context.GetArgument(0));
        DataType dataType = GetDataTypeFromString(ref context, context.GetArgument<string>(1));
        IConsoleConnection? conn = this.machine.Connection;
        if (conn == null || conn.IsClosed) {
            throw InvalidOperation(context, "Not connected to console");
        }

        using IDisposable? token = await this.machine.BusyLock.BeginBusyOperationAsync(ct);
        if (token == null) {
            ct.ThrowIfCancellationRequested();
            throw InvalidOperation(context, "Failed to obtain network busy token");
        }

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
        uint address = GetAddressFromValue(ref context, context.GetArgument(0));
        DataType dataType = GetDataTypeFromString(ref context, context.GetArgument<string>(1));
        double d = context.GetArgument<double>(2);
        IDataValue theValue;
        switch (dataType) {
            case DataType.Byte:
                if (d < byte.MinValue || d > byte.MaxValue)
                    throw InvalidOperation(context, "Value is out of range for type 'byte': " + d);
                theValue = new DataValueByte((byte) d);
                break;
            case DataType.Int16:
                if (d < short.MinValue || d > short.MaxValue)
                    throw InvalidOperation(context, "Value is out of range for type 'short': " + d);
                theValue = new DataValueInt16((short) d);
                break;
            case DataType.Int32:
                if (d < int.MinValue || d > int.MaxValue)
                    throw InvalidOperation(context, "Value is out of range for type 'int': " + d);
                theValue = new DataValueInt32((int) d);
                break;
            case DataType.Int64:
                if (d < long.MinValue || d > long.MaxValue)
                    throw InvalidOperation(context, "Value is out of range for type 'long': " + d);
                theValue = new DataValueInt64((long) d);
                break;
            case DataType.Float:
                if (d < float.MinValue || d > float.MaxValue)
                    throw InvalidOperation(context, "Value is out of range for type 'float': " + d);
                theValue = new DataValueFloat((float) d);
                break;
            case DataType.Double: theValue = new DataValueDouble(d); break;
            default:              throw new ArgumentOutOfRangeException();
        }

        IConsoleConnection? conn = this.machine.Connection;
        if (conn == null || conn.IsClosed) {
            throw InvalidOperation(context, "Not connected to console");
        }

        using IDisposable? token = await this.machine.BusyLock.BeginBusyOperationAsync(ct);
        if (token == null) {
            ct.ThrowIfCancellationRequested();
            throw InvalidOperation(context, "Failed to obtain network busy token");
        }

        await MemoryEngine.WriteDataValue(conn, address, theValue);
        return 0;
    }

    public async ValueTask<int> ReadString(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
        uint address = GetAddressFromValue(ref context, context.GetArgument(0));
        int count = context.GetArgument<int>(1);
        if (count < 0)
            throw InvalidOperation(context, "Cannot read negative length string: " + count);

        if (count == 0) {
            buffer.Span[0] = (LuaValue) string.Empty;
            return 1;
        }

        IConsoleConnection? conn = this.machine.Connection;
        if (conn == null || conn.IsClosed) {
            throw InvalidOperation(context, "Not connected to console");
        }

        using IDisposable? token = await this.machine.BusyLock.BeginBusyOperationAsync(ct);
        if (token == null) {
            ct.ThrowIfCancellationRequested();
            throw InvalidOperation(context, "Failed to obtain network busy token");
        }

        string result = await conn.ReadStringASCII(address, count, removeNull: true);
        buffer.Span[0] = (LuaValue) result;
        return 1;
    }

    public async ValueTask<int> WriteString(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
        uint address = GetAddressFromValue(ref context, context.GetArgument(0));
        string value = context.GetArgument<string>(1);
        if (string.IsNullOrEmpty(value)) {
            return 0; // no point in writing "", since nothing would change
        }

        IConsoleConnection? conn = this.machine.Connection;
        if (conn == null || conn.IsClosed) {
            throw InvalidOperation(context, "Not connected to console");
        }

        using IDisposable? token = await this.machine.BusyLock.BeginBusyOperationAsync(ct);
        if (token == null) {
            ct.ThrowIfCancellationRequested();
            throw InvalidOperation(context, "Failed to obtain network busy token");
        }

        await conn.WriteString(address, value);
        return 0;
    }

    public async ValueTask<int> SetIsFrozen(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
        bool freeze = context.GetArgument<bool>(0);
        IConsoleConnection? conn = this.machine.Connection;
        if (conn == null || conn.IsClosed) {
            throw InvalidOperation(context, "Not connected to console");
        }

        if (!conn.TryGetFeature(out IFeatureIceCubes? cubes)) {
            throw InvalidOperation(context, "Console does not support freezing and unfreezing");
        }

        using IDisposable? token = await this.machine.BusyLock.BeginBusyOperationAsync(ct);
        if (token == null) {
            ct.ThrowIfCancellationRequested();
            throw InvalidOperation(context, "Failed to obtain network busy token");
        }

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
        IConsoleConnection? conn = this.machine.Connection;
        if (conn == null || conn.IsClosed) {
            throw InvalidOperation(context, "Not connected to console");
        }

        if (!conn.TryGetFeature(out IFeatureIceCubesEx? cubes)) {
            throw InvalidOperation(context, "Console does not support querying the frozen state");
        }

        using IDisposable? token = await this.machine.BusyLock.BeginBusyOperationAsync(ct);
        if (token == null) {
            ct.ThrowIfCancellationRequested();
            throw InvalidOperation(context, "Failed to obtain network busy token");
        }

        bool result = await cubes.IsFrozen();
        buffer.Span[0] = result;
        return 1;
    }

    private async ValueTask<int> SendNotification(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
        string type = context.GetArgument<string>(0);
        if (!Enum.TryParse(type, true, out XNotifyLogo logoType)) {
            throw InvalidOperation(context, "Invalid XNotifyLogo: " + type);
        }

        string? message = context.HasArgument(1) ? context.GetArgument<string>(1) : null;
        IConsoleConnection? conn = this.machine.Connection;
        if (conn == null || conn.IsClosed) {
            throw InvalidOperation(context, "Not connected to console");
        }

        if (!conn.TryGetFeature(out IFeatureXboxNotifications? notifications)) {
            throw InvalidOperation(context, "Connection does not support showing xbox notifications. Is JRPC installed?");
        }

        using IDisposable? token = await this.machine.BusyLock.BeginBusyOperationAsync(ct);
        if (token == null) {
            ct.ThrowIfCancellationRequested();
            throw InvalidOperation(context, "Failed to obtain network busy token");
        }

        await notifications.ShowNotification(logoType, message);
        return 0;
    }

    private async ValueTask<int> GetDriveList(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
        IConsoleConnection? conn = this.machine.Connection;
        if (conn == null || conn.IsClosed)
            throw InvalidOperation(context, "Not connected to console");
        if (!conn.TryGetFeature(out IFeatureFileSystemInfo? fsInfo))
            throw InvalidOperation(context, "Connection does not support remote file system interactivity");

        using IDisposable? token = await this.machine.BusyLock.BeginBusyOperationAsync(ct);
        if (token == null) {
            ct.ThrowIfCancellationRequested();
            throw InvalidOperation(context, "Failed to obtain network busy token");
        }

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

        IConsoleConnection? conn = this.machine.Connection;
        if (conn == null || conn.IsClosed)
            throw InvalidOperation(context, "Not connected to console");
        if (!conn.TryGetFeature(out IFeatureFileSystemInfo? fsInfo))
            throw InvalidOperation(context, "Connection does not support remote file system interactivity");

        using IDisposable? token = await this.machine.BusyLock.BeginBusyOperationAsync(ct);
        if (token == null) {
            ct.ThrowIfCancellationRequested();
            throw InvalidOperation(context, "Failed to obtain network busy token");
        }

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

        IConsoleConnection? conn = this.machine.Connection;
        if (conn == null || conn.IsClosed)
            throw InvalidOperation(context, "Not connected to console");
        if (!conn.TryGetFeature(out IFeatureFileSystemInfo? fsInfo))
            throw InvalidOperation(context, "Connection does not support remote file system interactivity");

        using IDisposable? token = await this.machine.BusyLock.BeginBusyOperationAsync(ct);
        if (token == null) {
            ct.ThrowIfCancellationRequested();
            throw InvalidOperation(context, "Failed to obtain network busy token");
        }

        bool result = await fsInfo.DeleteFileSystemEntryRecursive(path);
        buffer.Span[0] = result;
        return 1;
    }

    private async ValueTask<int> LaunchFile(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
        string path = context.GetArgument<string>(0);
        IConsoleConnection? conn = this.machine.Connection;
        if (conn == null || conn.IsClosed)
            throw InvalidOperation(context, "Not connected to console");
        if (!conn.TryGetFeature(out IFeatureFileSystemInfo? fsInfo))
            throw InvalidOperation(context, "Connection does not support remote file system interactivity");

        using IDisposable? token = await this.machine.BusyLock.BeginBusyOperationAsync(ct);
        if (token == null) {
            ct.ThrowIfCancellationRequested();
            throw InvalidOperation(context, "Failed to obtain network busy token");
        }

        await fsInfo.LaunchFile(path);
        return 0;
    }

    private async ValueTask<int> MoveFile(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
        string oldPath = context.GetArgument<string>(0);
        string newPath = context.GetArgument<string>(1);
        IConsoleConnection? conn = this.machine.Connection;
        if (conn == null || conn.IsClosed)
            throw InvalidOperation(context, "Not connected to console");
        if (!conn.TryGetFeature(out IFeatureFileSystemInfo? fsInfo))
            throw InvalidOperation(context, "Connection does not support remote file system interactivity");

        using IDisposable? token = await this.machine.BusyLock.BeginBusyOperationAsync(ct);
        if (token == null) {
            ct.ThrowIfCancellationRequested();
            throw InvalidOperation(context, "Failed to obtain network busy token");
        }

        await fsInfo.MoveFile(oldPath, newPath);
        return 0;
    }

    private async ValueTask<int> CreateDirectory(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
        string dirPath = context.GetArgument<string>(0);
        IConsoleConnection? conn = this.machine.Connection;
        if (conn == null || conn.IsClosed)
            throw InvalidOperation(context, "Not connected to console");
        if (!conn.TryGetFeature(out IFeatureFileSystemInfo? fsInfo))
            throw InvalidOperation(context, "Connection does not support remote file system interactivity");

        using IDisposable? token = await this.machine.BusyLock.BeginBusyOperationAsync(ct);
        if (token == null) {
            ct.ThrowIfCancellationRequested();
            throw InvalidOperation(context, "Failed to obtain network busy token");
        }

        await fsInfo.CreateDirectory(dirPath);
        return 0;
    }
    
    private ValueTask<int> GetPathSeparator(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
        IConsoleConnection? conn = this.machine.Connection;
        if (conn == null || conn.IsClosed)
            throw InvalidOperation(context, "Not connected to console");
        if (!conn.TryGetFeature(out IFeatureFileSystemInfo? fsInfo))
            throw InvalidOperation(context, "Connection does not support remote file system interactivity");

        buffer.Span[0] = fsInfo.GetPathSeparatorChar().ToString();
        return ValueTask.FromResult(1);
    }

    private static LuaRuntimeException InvalidOperation(LuaFunctionExecutionContext ctx, string message) {
        return new LuaRuntimeException(ctx.State.GetTraceback(), message + " | Function = '" + ctx.Thread.GetCurrentFrame().Function.Name + "'");
    }

    private static uint GetAddressFromValue(ref LuaFunctionExecutionContext context, LuaValue addressArgument) {
        uint address;
        if (addressArgument.Type == LuaValueType.Number) {
            addressArgument.TryRead(out double addr);
            if (addr < 0)
                throw InvalidOperation(context, "Address cannot be a negative number");
            address = (uint) addr;
        }
        else if (addressArgument.Type == LuaValueType.String) {
            addressArgument.TryRead(out string str);
            str ??= "";
            if (str.StartsWith("0x"))
                str = str.Substring(2);

            if (!uint.TryParse(str, NumberStyles.HexNumber, null, out address))
                throw InvalidOperation(context, "Invalid address string: " + str);
        }
        else {
            throw InvalidOperation(context, "Invalid first argument for address: " + addressArgument);
        }

        return address;
    }

    private static DataType GetDataTypeFromString(ref LuaFunctionExecutionContext context, string type) {
        switch (type.ToUpperInvariant()) {
            case "BYTE":
            case "INT8":
                return DataType.Byte;
            case "SHORT":
            case "INT16":
                return DataType.Int16;
            case "INT":
            case "INT32":
                return DataType.Int32;
            case "LONG":
            case "INT64":
                return DataType.Int64;
            case "FLOAT":
            case "SINGLE":
            case "FLOAT32":
                return DataType.Float;
            case "DOUBLE":
            case "DOUBLE64":
            case "FLOAT64":
                return DataType.Double;
            default: throw InvalidOperation(context, "Unknown data type: " + type);
        }
    }
}