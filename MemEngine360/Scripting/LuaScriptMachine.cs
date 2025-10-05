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
using Lua;
using Lua.Runtime;
using Lua.Standard;
using MemEngine360.Connections;
using MemEngine360.Engine;

namespace MemEngine360.Scripting;

public sealed class LuaScriptMachine {
    private readonly Script ownerScript;
    private readonly Chunk sourceChunk;
    private CancellationTokenSource? initialCts;
    private CancellationTokenSource? killCts;
    private readonly TaskCompletionSource<LuaValue[]> tcsCompletion;
    private LuaState? luaState;
    private int state;

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
    /// Gets the connection this lua machine should use for network operations.
    /// </summary>
    public IConsoleConnection? Connection { get; }
    
    /// <summary>
    /// Gets the busy lock this lua machine should use for guarding <see cref="Connection"/> against concurrent network operations
    /// </summary>
    public BusyLock BusyLock { get; }

    /// <summary>
    /// An event fired when a line is printed from the lua script. 
    /// </summary>
    public event Action<string>? LinePrinted;

    public LuaScriptMachine(Script script, Chunk sourceChunk, IConsoleConnection? connection, BusyLock busyLock) {
        this.ownerScript = script ?? throw new ArgumentNullException(nameof(script));
        this.sourceChunk = sourceChunk ?? throw new ArgumentNullException(nameof(sourceChunk));
        this.BusyLock = busyLock ?? throw new ArgumentNullException(nameof(busyLock));
        this.Connection = connection;
        this.initialCts = new CancellationTokenSource();
        this.killCts = new CancellationTokenSource();
        this.tcsCompletion = new TaskCompletionSource<LuaValue[]>();
        this.CancellationToken = this.initialCts.Token;
        this.KillCancellationToken = this.killCts.Token;
    }

    public void Start() {
        if (Interlocked.CompareExchange(ref this.state, 1, 0) != 0)
            throw new InvalidOperationException("Already started");

        Task.Run(this.RunLuaMachine, CancellationToken.None);
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
        Debug.Assert(this.state == 1);
        this.state = 2;
        
        Script.InternalOnLuaMachineStarting(this.ownerScript);

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

            Script.InternalOnLuaMachineRunning(this.ownerScript);
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
            this.state = 3;
            this.luaState = null;
            Interlocked.Exchange(ref this.initialCts, null)?.Dispose();
            Interlocked.Exchange(ref this.killCts, null)?.Dispose();
            Script.InternalOnLuaMachineStopped(this.ownerScript);
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

    private void OpenConnectionLibrary(LuaState theState) {
        LuaEngineFunctions functions = new LuaEngineFunctions(this);
        functions.Install(theState);
    }

    private static void AssignFunction(LuaTable luaTable, LuaFunction function) {
        luaTable[(LuaValue) function.Name] = (LuaValue) function;
    }

    #endregion
}