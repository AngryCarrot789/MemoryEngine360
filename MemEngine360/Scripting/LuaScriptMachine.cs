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

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Lua;
using Lua.Runtime;
using Lua.Standard;
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using MemEngine360.ValueAbstraction;

namespace MemEngine360.Scripting;

public sealed class LuaScriptMachine {
    private readonly Script ownerScript;
    private readonly Chunk sourceChunk;
    private CancellationTokenSource? initialCts;
    private CancellationTokenSource? killCts;
    private readonly TaskCompletionSource<LuaValue[]> tcsCompletion;
    private LuaState? luaState;

    /// <summary>
    /// Gets the cancellation token that becomes cancelled when stopping the script is requested
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the cancellation token that becomes cancelled when we want to force the lua thread to stop
    /// </summary>
    public CancellationToken KillCancellationToken { get; }

    /// <summary>
    /// Gets a task that represents the completion of the script. May be faulted with the exception
    /// </summary>
    public Task<LuaValue[]> CompletionTask => this.tcsCompletion.Task;

    /// <summary>
    /// An event fired when a line is printed from the lua script. 
    /// </summary>
    public event Action<string>? LinePrinted;

    public LuaScriptMachine(Script script, Chunk sourceChunk) {
        this.ownerScript = script;
        this.sourceChunk = sourceChunk;
        this.initialCts = new CancellationTokenSource();
        this.killCts = new CancellationTokenSource();
        this.tcsCompletion = new TaskCompletionSource<LuaValue[]>();
        this.CancellationToken = this.initialCts.Token;
        this.KillCancellationToken = this.killCts.Token;
        Task.Run(this.RunLuaMachine);

        // this.thread = new Thread(static t => ((ThreadedLuaScript) t!).ScriptMain());
        // this.thread.Start(this);
    }

    /// <summary>
    /// Requests the script to stop
    /// </summary>
    /// <param name="force">Try to force kill the lua thread (making <see cref="KillCancellationToken"/> become cancelled)</param>
    public void RequestStop(bool force) {
        this.initialCts?.Cancel();
        if (force)
            this.killCts?.Cancel();
    }

    private async Task RunLuaMachine() {
        Script.InternalOnLuaStarting(this.ownerScript);

        try {
            this.CancellationToken.ThrowIfCancellationRequested();
            this.luaState = LuaState.Create();
            this.CancellationToken.ThrowIfCancellationRequested();

            this.OpenBasicLibrary(this.luaState);
            this.luaState.OpenBitwiseLibrary();
            this.luaState.OpenCoroutineLibrary();
            this.luaState.OpenMathLibrary();
            this.luaState.OpenStringLibrary();
            this.luaState.OpenTableLibrary();
            OpenOSLibrary(this.luaState);
            this.OpenConnectionLibrary(this.luaState);
            this.CancellationToken.ThrowIfCancellationRequested();

            Script.InternalOnLuaRunning(this.ownerScript);
            LuaValue[] values = new LuaValue[1024];

            Task<int> op = this.luaState!.RunAsync(this.sourceChunk, values, this.CancellationToken).AsTask();
            _ = op.ContinueWith(t => t.Exception?.GetType(), CancellationToken.None);

            Task<int> wait = op.WaitAsync(this.KillCancellationToken);
            _ = wait.ContinueWith(t => t.Exception?.GetType(), CancellationToken.None);

            await Task.WhenAny(op, wait);
            int result = op.IsCompleted ? await op : await wait;
            LuaValue[] results = new LuaValue[result];
            values.AsSpan(0, result).CopyTo(results);
            this.tcsCompletion.SetResult(results);
        }
        catch (OperationCanceledException e) {
            this.tcsCompletion.SetCanceled(e.CancellationToken);
        }
        catch (Exception e) {
            this.tcsCompletion.SetException(e);
        }
        finally {
            this.luaState = null;
            Interlocked.Exchange(ref this.initialCts, null)?.Dispose();
            Interlocked.Exchange(ref this.killCts, null)?.Dispose();
            Script.InternalOnStopped(this.ownerScript);
        }
    }

    private ValueTask<int> Print(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken cancellation) {
        if (this.LinePrinted != null) {
            for (int i = 0; i < context.ArgumentCount; ++i) {
                this.Print(context.Arguments[i].ToString());
            }
        }

        return ValueTask.FromResult(0);
    }

    public void Print(string text) {
        string[] subLines = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        foreach (string line in subLines) {
            this.LinePrinted?.Invoke(line);
        }
    }

    #region State Setup

    private void OpenBasicLibrary(LuaState theState) {
        theState.Environment[(LuaValue) "_G"] = (LuaValue) theState.Environment;
        theState.Environment[(LuaValue) "_VERSION"] = (LuaValue) "Lua 5.2";
        AssignFunction(theState.Environment, new LuaFunction("assert", BasicLibrary.Instance.Assert));
        AssignFunction(theState.Environment, new LuaFunction("error", BasicLibrary.Instance.Error));
        AssignFunction(theState.Environment, new LuaFunction("getmetatable", BasicLibrary.Instance.GetMetatable));
        AssignFunction(theState.Environment, new LuaFunction("ipairs", BasicLibrary.Instance.IPairs));
        AssignFunction(theState.Environment, new LuaFunction("next", BasicLibrary.Instance.Next));
        AssignFunction(theState.Environment, new LuaFunction("pairs", BasicLibrary.Instance.Pairs));
        AssignFunction(theState.Environment, new LuaFunction("pcall", BasicLibrary.Instance.PCall));
        AssignFunction(theState.Environment, new LuaFunction("print", this.Print));
        AssignFunction(theState.Environment, new LuaFunction("rawequal", BasicLibrary.Instance.RawEqual));
        AssignFunction(theState.Environment, new LuaFunction("rawget", BasicLibrary.Instance.RawGet));
        AssignFunction(theState.Environment, new LuaFunction("rawlen", BasicLibrary.Instance.RawLen));
        AssignFunction(theState.Environment, new LuaFunction("rawset", BasicLibrary.Instance.RawSet));
        AssignFunction(theState.Environment, new LuaFunction("select", BasicLibrary.Instance.Select));
        AssignFunction(theState.Environment, new LuaFunction("setmetatable", BasicLibrary.Instance.SetMetatable));
        AssignFunction(theState.Environment, new LuaFunction("tonumber", BasicLibrary.Instance.ToNumber));
        AssignFunction(theState.Environment, new LuaFunction("tostring", BasicLibrary.Instance.ToString));
        AssignFunction(theState.Environment, new LuaFunction("type", BasicLibrary.Instance.Type));
        AssignFunction(theState.Environment, new LuaFunction("xpcall", BasicLibrary.Instance.XPCall));

        theState.Environment[(LuaValue) "sleep"] = new LuaFunction(async (context, buffer, ct) => {
            double sec = context.GetArgument<double>(0);
            await Task.Delay(TimeSpan.FromSeconds(sec), ct);
            return 0;
        });
    }

    private static void OpenOSLibrary(LuaState theState) {
        LuaTable luaTable = new LuaTable(0, 3);
        AssignFunction(luaTable, new LuaFunction("clock", OperatingSystemLibrary.Instance.Clock));
        AssignFunction(luaTable, new LuaFunction("date", OperatingSystemLibrary.Instance.Date));
        AssignFunction(luaTable, new LuaFunction("time", OperatingSystemLibrary.Instance.Time));
        theState.Environment[(LuaValue) "os"] = (LuaValue) luaTable;
        theState.LoadedModules[(LuaValue) "os"] = (LuaValue) luaTable;
    }

    private void OpenConnectionLibrary(LuaState state) {
        LuaTable luaTable = new LuaTable(0, 3);
        AssignFunction(luaTable, new LuaFunction("readnumber", this.ReadNumber));
        AssignFunction(luaTable, new LuaFunction("writenumber", this.WriteNumber));
        state.Environment[(LuaValue) "engine"] = (LuaValue) luaTable;
        state.LoadedModules[(LuaValue) "engine"] = (LuaValue) luaTable;
    }

    private static LuaRuntimeException InvalidOperation(LuaFunctionExecutionContext ctx, string message) {
        return new LuaRuntimeException(ctx.State.GetTraceback(), message + " | Function = '" + ctx.Thread.GetCurrentFrame().Function.Name + "'");
    }

    private async ValueTask<int> ReadNumber(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
        uint address = GetAddressFromValue(ref context, context.GetArgument(0));
        DataType dataType = GetDataTypeFromString(ref context, context.GetArgument<string>(1));
        IConsoleConnection? conn = this.ownerScript.DedicatedConnection;
        if (conn == null || conn.IsClosed) {
            throw InvalidOperation(context, "Connection disconnected.");
        }

        using IDisposable? token = await this.ownerScript.BusyLock.BeginBusyOperationAsync(ct);
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

    private async ValueTask<int> WriteNumber(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken ct) {
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

        IConsoleConnection? conn = this.ownerScript.DedicatedConnection;
        if (conn == null || conn.IsClosed) {
            throw InvalidOperation(context, "Connection disconnected.");
        }

        using IDisposable? token = await this.ownerScript.BusyLock.BeginBusyOperationAsync(ct);
        if (token == null) {
            ct.ThrowIfCancellationRequested();
            throw InvalidOperation(context, "Failed to obtain network busy token");
        }

        await MemoryEngine.WriteDataValue(conn, address, theValue);
        return 0;
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

    private static void AssignFunction(LuaTable luaTable, LuaFunction function) {
        luaTable[(LuaValue) function.Name] = (LuaValue) function;
    }

    #endregion
}