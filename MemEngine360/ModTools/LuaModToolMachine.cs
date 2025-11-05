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

using System.Collections.Concurrent;
using System.Diagnostics;
using Lua;
using Lua.Runtime;
using Lua.Standard;
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.ModTools.LuaFeatures;
using MemEngine360.Scripting;
using MemEngine360.Scripting.LuaFeatures;
using PFXToolKitUI;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Utils;
using LuaEngineFunctions = MemEngine360.Scripting.LuaFeatures.LuaEngineFunctions;

namespace MemEngine360.ModTools;

public sealed class LuaModToolMachine : ILuaMachine {
    public delegate ValueTask MessageCallback(LuaFunctionExecutionContext ctx, object? state, CancellationToken ct);

    private readonly ModTool owner;
    private readonly Chunk sourceChunk;
    private CancellationTokenSource? initialCts;
    private CancellationTokenSource? killCts;
    private readonly TaskCompletionSource<LuaValue[]> tcsCompletion;
    private LuaState? luaState;
    private int state;

    private readonly struct MsgPumpEntry(MessageCallback callback, object? state) {
        public readonly MessageCallback callback = callback;
        public readonly object? state = state;
    }

    private readonly Lock msgPumpLock = new Lock();
    private readonly ConcurrentQueue<MsgPumpEntry> messageQueue = new ConcurrentQueue<MsgPumpEntry>();
    private int isProcessingMessagesSYNC;
    private CancellableTaskCompletionSource? tcsMessageAvailable;

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
    /// Gets the mod tool that started this lua machien
    /// </summary>
    public ModTool Owner => this.owner;

    /// <summary>
    /// Gets the list of timers that the user created via create_timer but has not yet destroyed via destroy_timer.
    /// We keep track of timers so that we can dispose them if the script is stopped or throws an error
    /// </summary>
    public List<Timer> UserTimers { get; } = new List<Timer>();

    /// <summary>
    /// An event fired when a line is printed from the lua script. 
    /// </summary>
    public event Action<string>? LinePrinted;

    public LuaModToolMachine(ModTool modTool, Chunk sourceChunk, IConsoleConnection? connection, BusyLock busyLock) {
        this.owner = modTool ?? throw new ArgumentNullException(nameof(modTool));
        this.sourceChunk = sourceChunk ?? throw new ArgumentNullException(nameof(sourceChunk));
        this.BusyLock = busyLock ?? throw new ArgumentNullException(nameof(busyLock));
        this.Connection = connection;
        this.initialCts = new CancellationTokenSource();
        this.killCts = new CancellationTokenSource();
        this.tcsCompletion = new TaskCompletionSource<LuaValue[]>();
        this.CancellationToken = this.initialCts.Token;
        this.KillCancellationToken = this.killCts.Token;
    }

    /// <summary>
    /// Gets or sets the top-level that the script exists in. This is used for showing popups from message boxes.
    /// <para>
    /// This method delegates to the main thread, if not on it
    /// </para>
    /// </summary>
    public Task<ITopLevel?> GetScriptTopLevelAsync() {
        if (ApplicationPFX.Instance.Dispatcher.CheckAccess()) {
            return Task.FromResult(this.InternalGetScriptTopLevel());
        }

        return ApplicationPFX.Instance.Dispatcher.InvokeAsync(this.InternalGetScriptTopLevel, token: this.CancellationToken);
    }

    private ITopLevel? InternalGetScriptTopLevel() {
        IMutableContextData? context = this.owner.Manager?.UserContext;
        return context != null ? ITopLevel.FromContext(context) : null;
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

        ModTool.InternalOnLuaMachineStarting(this.owner);

        try {
            this.CancellationToken.ThrowIfCancellationRequested();
            this.luaState = LuaState.Create();
            this.CancellationToken.ThrowIfCancellationRequested();

            // Use custom basics library, to remove functions that can modify the OS,
            // run files or shutdown the app forcefully
            this.OpenBasicLibrary(this.luaState);

            // coroutines are slightly buggy and can result in the script being unstoppable
            // without killing the native thread, which isn't possible via managed code
            // this.luaState.OpenCoroutineLibrary();

            this.luaState.OpenBitwiseLibrary();
            this.luaState.OpenMathLibrary();
            this.luaState.OpenStringLibrary();
            this.luaState.OpenTableLibrary();

            {
                // Use custom OpenOSLibrary implementation, to remove things like changing OS things
                LuaTable luaTable = new LuaTable(0, 3);
                LuaUtils.AssignFunction(luaTable, new LuaFunction("clock", OperatingSystemLibrary.Instance.Clock));
                LuaUtils.AssignFunction(luaTable, new LuaFunction("date", OperatingSystemLibrary.Instance.Date));
                LuaUtils.AssignFunction(luaTable, new LuaFunction("time", OperatingSystemLibrary.Instance.Time));
                this.luaState.Environment[(LuaValue) "os"] = (LuaValue) luaTable;
                this.luaState.LoadedModules[(LuaValue) "os"] = (LuaValue) luaTable;
            }

            {
                LuaTable luaTable = new LuaTable(0, 2);
                LuaUtils.AssignFunction(luaTable, new LuaFunction("run_messages", this.RunMessages));
                LuaUtils.AssignFunction(luaTable, new LuaFunction("try_run_messages", this.TryRunMessages));
                this.luaState.Environment[(LuaValue) "pump"] = (LuaValue) luaTable;
                this.luaState.LoadedModules[(LuaValue) "pump"] = (LuaValue) luaTable;
            }

            _ = new LuaEngineFunctions(this, this.luaState);
            _ = new LuaGuiFunctions(this, this.luaState);

            this.CancellationToken.ThrowIfCancellationRequested();

            ModTool.InternalOnLuaMachineRunning(this.owner);
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
        catch (LuaRuntimeException e) when (e.InnerException is OperationCanceledException oce) {
            this.tcsCompletion.SetCanceled(oce.CancellationToken);
        }
        catch (Exception e) when (e is TimeoutException || e is IOException) {
            this.tcsCompletion.SetException(new LuaRuntimeException(this.luaState!.GetTraceback(), e.Message));
        }
        catch (Exception e) {
            this.tcsCompletion.SetException(e);
        }
        finally {
            this.state = 3;
            this.luaState = null;
            Interlocked.Exchange(ref this.initialCts, null)?.Dispose();
            Interlocked.Exchange(ref this.killCts, null)?.Dispose();
            lock (this.UserTimers) {
                foreach (Timer timer in this.UserTimers) {
                    // Don't use async overload. Not entirely sure how it works but we don't
                    // want to potentially wait for work that will never complete.
                    // ReSharper disable once MethodHasAsyncOverload
                    timer.Dispose();
                }
                
                this.UserTimers.Clear();
            }
            
            ModTool.InternalOnLuaMachineStopped(this.owner);
        }
    }

    public void PostMessage(Action<LuaFunctionExecutionContext, CancellationToken> callback) {
        this.PostMessage(static (c, state, ct) => {
            ((Action<LuaFunctionExecutionContext, CancellationToken>) state!)(c, ct);
            return ValueTask.CompletedTask;
        }, callback);
    }

    public void PostMessage(Func<LuaFunctionExecutionContext, CancellationToken, ValueTask> callback) {
        this.PostMessage(static (c, state, ct) => ((Func<LuaFunctionExecutionContext, CancellationToken, ValueTask>) state!)(c, ct), callback);
    }

    public Task<T> InvokeAsync<T>(Func<LuaFunctionExecutionContext, CancellationToken, ValueTask<T>> callback) {
        TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();
        this.PostMessage(async (c, _, ct) => {
            try {
                tcs.SetResult(await callback(c, ct));
            }
            catch (OperationCanceledException e) {
                tcs.SetCanceled(e.CancellationToken);
            }
            catch (Exception e) {
                tcs.SetException(e);
            }
        }, null);

        return tcs.Task;
    }

    public void PostMessage(MessageCallback callback, object? callbackState) {
        lock (this.msgPumpLock) {
            this.messageQueue.Enqueue(new MsgPumpEntry(callback, callbackState));
            this.tcsMessageAvailable?.TrySetResult();
        }
    }

    private async ValueTask<int> RunMessages(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        if (Interlocked.CompareExchange(ref this.isProcessingMessagesSYNC, 1, 0) != 0) {
            throw LuaUtils.Exception(in ctx, "Already processing messages");
        }

        try {
            do {
                CancellableTaskCompletionSource? tcs = null;
                lock (this.msgPumpLock) {
                    if (this.messageQueue.IsEmpty) {
                        Debug.Assert(this.tcsMessageAvailable == null);
                        this.tcsMessageAvailable = tcs = new CancellableTaskCompletionSource(ct);
                    }
                }

                if (tcs != null) {
                    try {
                        await tcs.Task;
                    }
                    finally {
                        tcs.Dispose();
                        this.tcsMessageAvailable = null;
                    }
                }
            } while (this.messageQueue.IsEmpty);
            
            while (this.messageQueue.TryDequeue(out MsgPumpEntry entry)) {
                try {
                    await entry.callback(ctx, entry.state, ct);
                }
                catch (LuaRuntimeException) {
                    throw;
                }
                catch (Exception e) {
                    throw new LuaRuntimeException(ctx.State.GetTraceback(), "Error processing message: " + e.Message);
                }
            }
        }
        finally {
            Interlocked.Exchange(ref this.isProcessingMessagesSYNC, 0);
        }

        return 0;
    }

    private async ValueTask<int> TryRunMessages(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        await this.TryRunMessages(ctx, ct);
        return 0;
    }

    public async ValueTask TryRunMessages(LuaFunctionExecutionContext ctx, CancellationToken ct) {
        while (this.messageQueue.TryDequeue(out MsgPumpEntry entry)) {
            try {
                await entry.callback(ctx, entry.state, ct);
            }
            catch (LuaRuntimeException) {
                throw;
            }
            catch (Exception e) {
                throw new LuaRuntimeException(ctx.State.GetTraceback(), "Error processing message: " + e.Message);
            }
        }
    }

    private ValueTask<int> Print(LuaFunctionExecutionContext context, Memory<LuaValue> buffer, CancellationToken cancellation) {
        cancellation.ThrowIfCancellationRequested();
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
        LuaUtils.AssignFunction(theState.Environment, new LuaFunction("assert", BasicLibrary.Instance.Assert));
        LuaUtils.AssignFunction(theState.Environment, new LuaFunction("error", BasicLibrary.Instance.Error));
        LuaUtils.AssignFunction(theState.Environment, new LuaFunction("getmetatable", BasicLibrary.Instance.GetMetatable));
        LuaUtils.AssignFunction(theState.Environment, new LuaFunction("ipairs", BasicLibrary.Instance.IPairs));
        LuaUtils.AssignFunction(theState.Environment, new LuaFunction("next", BasicLibrary.Instance.Next));
        LuaUtils.AssignFunction(theState.Environment, new LuaFunction("pairs", BasicLibrary.Instance.Pairs));
        LuaUtils.AssignFunction(theState.Environment, new LuaFunction("pcall", BasicLibrary.Instance.PCall));
        LuaUtils.AssignFunction(theState.Environment, new LuaFunction("print", this.Print));
        LuaUtils.AssignFunction(theState.Environment, new LuaFunction("rawequal", BasicLibrary.Instance.RawEqual));
        LuaUtils.AssignFunction(theState.Environment, new LuaFunction("rawget", BasicLibrary.Instance.RawGet));
        LuaUtils.AssignFunction(theState.Environment, new LuaFunction("rawlen", BasicLibrary.Instance.RawLen));
        LuaUtils.AssignFunction(theState.Environment, new LuaFunction("rawset", BasicLibrary.Instance.RawSet));
        LuaUtils.AssignFunction(theState.Environment, new LuaFunction("select", BasicLibrary.Instance.Select));
        LuaUtils.AssignFunction(theState.Environment, new LuaFunction("setmetatable", BasicLibrary.Instance.SetMetatable));
        LuaUtils.AssignFunction(theState.Environment, new LuaFunction("tonumber", BasicLibrary.Instance.ToNumber));
        LuaUtils.AssignFunction(theState.Environment, new LuaFunction("tostring", BasicLibrary.Instance.ToString));
        LuaUtils.AssignFunction(theState.Environment, new LuaFunction("type", BasicLibrary.Instance.Type));
        LuaUtils.AssignFunction(theState.Environment, new LuaFunction("xpcall", BasicLibrary.Instance.XPCall));

        theState.Environment[(LuaValue) "sleep"] = new LuaFunction(async (context, buffer, ct) => {
            double sec = context.GetArgument<double>(0);
            await Task.Delay(TimeSpan.FromSeconds(sec), ct);
            return 0;
        });
    }

    #endregion
}