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
using Lua.CodeAnalysis.Compilation;
using Lua.CodeAnalysis.Syntax;
using Lua.Runtime;
using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Composition;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.RDA;

namespace MemEngine360.Scripting;

public delegate void ScriptEventHandler(Script sender);

public delegate void ScriptDedicatedConnectionChangedEventHandler(Script sender, IConsoleConnection? oldDedicatedConnection, IConsoleConnection? newDedicatedConnection);

public class Script : IComponentManager, IUserLocalContext {
    public static readonly DataKey<Script> DataKey = DataKeys.Create<Script>(nameof(Script));

    internal ScriptingManager? myManager;
    private string? name;
    private bool isRunning;
    private bool isTryingToStop;
    private bool hasUnsavedChanges;
    private IConsoleConnection? dedicatedConnection;

    // Lua
    private Chunk? myChunk;
    private LuaScriptMachine? myLuaScript;
    private TaskCompletionSource? myDelegateTcs;

    public IMutableContextData UserContext { get; } = new ContextData();

    /// <summary>
    /// Gets or sets the name of this script. This is set as the file name of the opened script file
    /// </summary>
    public string? Name {
        get => this.name;
        set => PropertyHelper.SetAndRaiseINE(ref this.name, value, this, static t => t.NameChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets whether this script is currently running
    /// </summary>
    public bool IsRunning {
        get => this.isRunning;
        private set => PropertyHelper.SetAndRaiseINE(ref this.isRunning, value, this, static t => t.IsRunningChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets whether the user is trying to stop the script from running
    /// </summary>
    public bool IsTryingToStop {
        get => this.isTryingToStop;
        private set => PropertyHelper.SetAndRaiseINE(ref this.isTryingToStop, value, this, static t => t.IsTryingToStopChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets if this script's text has changed since it was created/loaded from file
    /// </summary>
    public bool HasUnsavedChanges {
        get => this.hasUnsavedChanges;
        set => PropertyHelper.SetAndRaiseINE(ref this.hasUnsavedChanges, value, this, static t => t.HasUnsavedChangesChanged?.Invoke(t));
    }

    public IConsoleConnection? DedicatedConnection {
        get => this.dedicatedConnection;
        set {
            ApplicationPFX.Instance.Dispatcher.VerifyAccess();
            this.CheckNotRunning("Cannot change dedicated connection while running");
            PropertyHelper.SetAndRaiseINE(ref this.dedicatedConnection, value, this, static (t, a, b) => t.DedicatedConnectionChanged?.Invoke(t, a, b));
        }
    }

    /// <summary>
    /// Gets the busy lock used to synchronize access to <see cref="DedicatedConnection"/>
    /// </summary>
    public BusyLock BusyLock { get; } = new BusyLock();

    /// <summary>
    /// Gets the source code for this script. Null when unloaded, empty when just empty
    /// </summary>
    public string? SourceCode { get; private set; }

    /// <summary>
    /// Gets the scripting manager that this script exists in
    /// </summary>
    public ScriptingManager? Manager => this.myManager;

    /// <summary>
    /// Gets the task that represents the script. Returns <see cref="Task.CompletedTask"/> when the script is not running.
    /// </summary>
    public Task ScriptTask => this.myDelegateTcs?.Task ?? Task.CompletedTask;

    /// <summary>
    /// Gets the observable list of console lines. Has a limit of 500 lines
    /// </summary>
    public ObservableList<string> ConsoleLines { get; } = new ObservableList<string>();

    public event ScriptEventHandler? NameChanged;
    public event ScriptEventHandler? IsRunningChanged;
    public event ScriptEventHandler? IsTryingToStopChanged;
    public event ScriptEventHandler? HasUnsavedChangesChanged;
    public event ScriptDedicatedConnectionChangedEventHandler? DedicatedConnectionChanged;

    private readonly Lock consoleQueueLock = new Lock();
    private readonly List<string> queuedLines = new List<string>();
    private readonly RateLimitedDispatchAction rldaPrintToConsole;
    private readonly ComponentStorage componentStorage;
    ComponentStorage IComponentManager.ComponentStorage => this.componentStorage;

    public Script() {
        this.componentStorage = new ComponentStorage(this);
        this.rldaPrintToConsole = RateLimitedDispatchActionBase.ForDispatcherSync(() => {
            lock (this.consoleQueueLock) {
                const int EntryLimit = 500;
                int excess = this.ConsoleLines.Count + this.queuedLines.Count;
                if (excess > EntryLimit) {
                    this.ConsoleLines.RemoveRange(0, excess - EntryLimit);
                }

                this.ConsoleLines.AddRange(this.queuedLines);
                this.queuedLines.Clear();
            }
        }, TimeSpan.FromMilliseconds(50));

        this.rldaPrintToConsole.DebugName = "Lua Print to Console";
    }

    public void SetSourceCode(string? sourceCode) {
        this.myChunk = null;
        this.SourceCode = sourceCode;
        this.HasUnsavedChanges = true;
    }

    /// <summary>
    /// Requests the script to stop
    /// </summary>
    /// <param name="force">
    /// Forces the script to stop. The lua thread may still be running even after <see cref="IsRunning"/>
    /// becomes false, but it will no longer be in our control.
    /// </param>
    public void RequestStop(bool force) {
        if (this.myLuaScript != null) {
            this.IsTryingToStop = true;
            this.myLuaScript.RequestStop(force);
        }
    }

    /// <summary>
    /// Compiles the source code, if not already compiled, then runs the script in a new thread
    /// </summary>
    public async Task<Result> StartCommand() {
        Debug.Assert((this.IsRunning && this.myLuaScript != null) || (!this.IsRunning && this.myLuaScript == null));
        this.CheckNotRunning();
        if (this.myChunk == null) {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Result<Chunk> result = await ActivityManager.Instance.RunTask(() => {
                ActivityTask activity = ActivityTask.Current;
                activity.Progress.SetCaptionAndText("Compile Lua");
                activity.Progress.IsIndeterminate = true;
                return CompileSource(this.SourceCode ?? "", activity.Progress, activity.CancellationToken);
            }, cts);

            this.myChunk = result.GetValueOrDefault();
            if (this.myChunk == null) {
                return Result.FromException(result.Exception!);
            }
        }

        this.ConsoleLines.Clear();
        this.myDelegateTcs = new TaskCompletionSource();
        this.myLuaScript = new LuaScriptMachine(this, this.myChunk);
        this.myLuaScript.LinePrinted += this.OnLuaLinePrinted;
        this.IsRunning = true;
        return Result.Success;
    }

    private void OnLuaThreadFinished() {
        Task<LuaValue[]> task = this.myLuaScript!.CompletionTask;
        try {
            LuaValue[] values = task.GetAwaiter().GetResult();
            if (values.Length > 0) {
                foreach (LuaValue value in values) {
                    this.myLuaScript!.Print(value + Environment.NewLine);
                }
            }
        }
        catch (OperationCanceledException) {
            // ignored
        }
        catch (Exception ex) {
            this.myLuaScript!.Print(ex.ToString());
        }

        this.myLuaScript = null;
        this.IsTryingToStop = false;
        this.IsRunning = false;
        this.myDelegateTcs!.SetResult();
        this.myDelegateTcs = null;
    }

    private void OnLuaLinePrinted(string obj) {
        lock (this.consoleQueueLock) {
            this.queuedLines.Add(obj);
            this.rldaPrintToConsole.InvokeAsync();
        }
    }

    public void CheckNotRunning(string errorMessage = "Script is currently running") {
        if (this.IsRunning)
            throw new InvalidOperationException(errorMessage);
    }

    // Invoked when lua thread starts
    internal static void InternalOnLuaStarting(Script script) {
        // ApplicationPFX.Instance.Dispatcher.Post(static s => ((Script) s!).IsRunning = true, script, DispatchPriority.Normal);
    }

    // Invoked when lua machine is fully booted and about to start running code.
    // May not be called if the user somehow manages to stop the script so quickly that
    // it never fully boots up.
    internal static void InternalOnLuaRunning(Script script) {
    }

    // Invoked when the lua thread is about to exit. The exception will originate from the lua machine
    internal static void InternalOnStopped(Script script) {
        ApplicationPFX.Instance.Dispatcher.Post((t) => ((Script) t!).OnLuaThreadFinished(), script, DispatchPriority.Send);
    }

    /// <summary>
    /// Compiles LUA source code into a chunk.
    /// </summary>
    /// <param name="source">The source code</param>
    /// <param name="progress">Optional compilation progress</param>
    /// <param name="cancellationToken">Cancel the compilation process</param>
    /// <returns>The compiled chunk</returns>
    public static async Task<Chunk> CompileSource(string source, IActivityProgress? progress, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        TaskCompletionSource<Chunk> tcs = new TaskCompletionSource<Chunk>();
        Thread threadParseAndCompile = new Thread(() => {
            bool hasResult = false;
            try {
                cancellationToken.ThrowIfCancellationRequested();
                if (progress != null)
                    progress.Text = "[Lexer] Parsing...";

                const string ChunkName = "Source_Code";
                Lexer lexer = new Lexer() {
                    Source = source.AsMemory(),
                    ChunkName = ChunkName
                };

                Parser parser = new Parser() { ChunkName = ChunkName };
                while (lexer.MoveNext()) {
                    cancellationToken.ThrowIfCancellationRequested();
                    parser.Add(lexer.Current);
                }

                if (progress != null)
                    progress.Text = "Creating syntax tree...";

                LuaSyntaxTree syntaxTree = parser.Parse();
                Chunk compilationResult = LuaCompiler.Default.Compile(syntaxTree, ChunkName);
                tcs.TrySetResult(compilationResult);
                hasResult = true;
            }
            catch (OperationCanceledException e) {
                tcs.TrySetCanceled(e.CancellationToken);
                hasResult = true;
            }
            catch (Exception e) {
                tcs.TrySetException(e);
                hasResult = true;
            }
            finally {
                if (!hasResult)
                    tcs.TrySetCanceled(cancellationToken);
            }
        });

        await using (cancellationToken.Register((t, self) => ((TaskCompletionSource<Chunk>) t!).TrySetCanceled(self), tcs)) {
            threadParseAndCompile.Start();
            return await tcs.Task;
        }
    }
}