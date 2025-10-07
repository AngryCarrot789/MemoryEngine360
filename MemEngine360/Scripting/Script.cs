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
using Lua.CodeAnalysis;
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
using PFXToolKitUI.Logging;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.RDA;

namespace MemEngine360.Scripting;

public delegate void ScriptEventHandler(Script sender);

public delegate void ScriptDedicatedConnectionChangedEventHandler(Script sender, IConsoleConnection? oldDedicatedConnection, IConsoleConnection? newDedicatedConnection);

public delegate void ScriptSourceCodeChangedEventHandler(Script sender, string oldSourceCode, string newSourceCode);

public delegate void ScriptCompilationFailureEventHandler(Script sender, string? chunkName, SourcePosition sourcePosition);

public class Script : IComponentManager, IUserLocalContext {
    public static readonly DataKey<Script> DataKey = DataKeys.Create<Script>(nameof(Script));

    internal ScriptingManager? myManager;
    private bool isTryingToStop;
    private bool hasUnsavedChanges;
    private bool clearConsoleOnRun = true;
    private IConsoleConnection? dedicatedConnection;
    private string sourceCode = "";
    private CancellationTokenSource? ctsCompile;

    // Lua
    private Chunk? myChunk;
    private LuaScriptMachine? myRunningMachine;
    private TaskCompletionSource? myRunningTcs;

    public IMutableContextData UserContext { get; } = new ContextData();
    
    /// <summary>
    /// Gets the name of this script. This is set as the file name of the opened script file, or can be set as custom when no file path is present.
    /// <para>
    /// This will either be null or contain actual characters that are not just whitespaces
    /// </para>
    /// </summary>
    public string? Name { get; private set; }

    /// <summary>
    /// Gets the file path for this script.
    /// <para>
    /// This will either be null or contain actual characters that are not just whitespaces
    /// </para>
    /// </summary>
    public string? FilePath { get; private set; }

    /// <summary>
    /// Gets whether this script is currently running. This checks whether <see cref="ScriptTask"/> is running or not.
    /// This value only changes on the main application thread, and <see cref="IsRunningChanged"/>
    /// will be fired shortly after it actually does change
    /// </summary>
    public bool IsRunning => !this.ScriptTask.IsCompleted;

    /// <summary>
    /// Gets whether we are currently compiling the lua script. <see cref="IsRunning"/> will not be true at this time.
    /// </summary>
    public bool IsCompiling => this.ctsCompile != null;

    /// <summary>
    /// Gets whether the user is trying to stop the script from running, rather than the script stopping on its own
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

    public bool ClearConsoleOnRun {
        get => this.clearConsoleOnRun;
        set => PropertyHelper.SetAndRaiseINE(ref this.clearConsoleOnRun, value, this, static t => t.ClearConsoleOnRunChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets the dedicated connection that this script will use
    /// </summary>
    public IConsoleConnection? DedicatedConnection {
        get => this.dedicatedConnection;
        set => PropertyHelper.SetAndRaiseINE(ref this.dedicatedConnection, value, this, static (t, a, b) => t.DedicatedConnectionChanged?.Invoke(t, a, b));
    }

    /// <summary>
    /// Gets or sets the source code of the script. This may not be immediately set when the user types text into the UI
    /// </summary>
    public string SourceCode {
        get => this.sourceCode;
        set {
            ArgumentNullException.ThrowIfNull(value);

            string oldValue = this.sourceCode;
            if (!ReferenceEquals(oldValue, value) /* we don't really care about true equality */) {
                this.myChunk = null;
                this.sourceCode = value;
                this.SourceCodeChanged?.Invoke(this, oldValue, value);
                this.HasUnsavedChanges = true;
            }
        }
    }

    /// <summary>
    /// Gets the busy lock used to synchronize access to <see cref="DedicatedConnection"/>
    /// </summary>
    public BusyLock DedicatedBusyLock { get; } = new BusyLock();

    /// <summary>
    /// Gets the scripting manager that this script exists in
    /// </summary>
    public ScriptingManager? Manager => this.myManager;

    /// <summary>
    /// Gets the task that represents the script. Returns <see cref="Task.CompletedTask"/> when the script is not running.
    /// </summary>
    public Task ScriptTask => this.myRunningTcs?.Task ?? Task.CompletedTask;

    /// <summary>
    /// Gets the observable list of console lines. Has a limit of 500 lines
    /// </summary>
    public ObservableList<string> ConsoleLines { get; } = new ObservableList<string>();

    /// <summary>
    /// Gets the task that represents the compile operation.
    /// When not compiling, <see cref="CompilationCancellation"/> will be null and this returns a completed task.
    /// <para>
    /// This may represent a cancelled task when compilation is cancelled.
    /// </para>
    /// </summary>
    public Task CompileTask { get; private set; }

    /// <summary>
    /// Fired when <see cref="Name"/> or <see cref="FilePath"/> changes
    /// </summary>
    public event ScriptEventHandler? FilePathChanged;

    public event ScriptEventHandler? IsRunningChanged;
    public event ScriptEventHandler? IsTryingToStopChanged;
    public event ScriptEventHandler? HasUnsavedChangesChanged;
    public event ScriptEventHandler? ClearConsoleOnRunChanged;
    public event ScriptDedicatedConnectionChangedEventHandler? DedicatedConnectionChanged;
    public event ScriptSourceCodeChangedEventHandler? SourceCodeChanged;
    public event ScriptCompilationFailureEventHandler? CompilationFailure;

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

    public void SetFilePath(string? newFilePath) {
        if (string.IsNullOrWhiteSpace(newFilePath))
            newFilePath = null;

        this.Name = Path.GetFileName(newFilePath);
        if (string.IsNullOrWhiteSpace(this.Name))
            this.Name = null;

        this.FilePath = newFilePath;
        this.FilePathChanged?.Invoke(this);
    }

    public void SetCustomNameWithoutPath(string? customName) {
        if (string.IsNullOrWhiteSpace(customName))
            customName = null;

        this.Name = customName;
        this.FilePath = null;
        this.FilePathChanged?.Invoke(this);
    }

    /// <summary>
    /// Requests the script to stop
    /// </summary>
    /// <param name="force">
    /// Forces the script to stop. The lua thread may still be running even after <see cref="IsRunning"/>
    /// becomes false, but it will no longer be in our control.
    /// </param>
    public void RequestStop(bool force) {
        if (this.myRunningMachine != null) {
            this.IsTryingToStop = true;
            this.myRunningMachine.RequestStop(force);
        }
    }

    /// <summary>
    /// Requests to cancel the current compilation operation. This will most likely make <see cref="StartCommand"/> returns false.
    /// </summary>
    public void RequestCancelCompilation() {
        this.ctsCompile?.Cancel();
    }
    
    /// <summary>
    /// Compiles the source code, if not already compiled, then runs the script in a new thread
    /// </summary>
    public async Task<bool> StartCommand() {
        Debug.Assert((this.IsRunning && this.myRunningMachine != null) || (!this.IsRunning && this.myRunningMachine == null));
        this.CheckNotRunning();
        if (this.Manager == null)
            throw new InvalidOperationException("Cannot start script not associated with a manager");
        if (this.ctsCompile != null)
            throw new InvalidOperationException("Currently compiling");
        
        if (this.ClearConsoleOnRun)
            this.ConsoleLines.Clear();

        if (this.myChunk == null) {
            string code = this.SourceCode;
            TaskCompletionSource tcs = new TaskCompletionSource();
            this.ctsCompile = new CancellationTokenSource();
            this.CompileTask = tcs.Task;
            
            try {
                Result<Chunk> result = await ActivityManager.Instance.RunTask(() => {
                    ActivityTask activity = ActivityTask.Current;
                    activity.Progress.SetCaptionAndText("Compile Lua");
                    activity.Progress.IsIndeterminate = true;
                    return CompileSource(code, this.Name ?? "Script", activity.Progress, activity.CancellationToken);
                }, this.ctsCompile);

                this.myChunk = result.GetValueOrDefault();
                if (this.myChunk == null) {
                    if (result.Exception is OperationCanceledException) {
                        this.PrintToConsole("Compilation cancelled");
                    }
                    else {
                        this.PrintToConsole("Failed to compile script");
                        if (result.Exception is LuaParseException lpe) {
                            this.PrintToConsole(lpe.Message);
                            if (lpe.Position is SourcePosition pos) {
                                this.CompilationFailure?.Invoke(this, lpe.ChunkName, pos);
                            }
                        }
                        else {
                            this.PrintToConsole(result.Exception!.ToString());
                        }
                    }

                    return false;
                }
            }
            finally {
                this.CompileTask = Task.CompletedTask;
                CancellationTokenSource cts = this.ctsCompile!;
                this.ctsCompile = null;
                tcs.SetResult();
                cts.Dispose();
            }

            if (this.Manager == null) {
                AppLogger.Instance.WriteLine("Script was deleted during compilation");
                Debugger.Break();
                return false;
            }
        }

        this.myRunningTcs = new TaskCompletionSource();

        IConsoleConnection? connection = this.dedicatedConnection ?? this.Manager!.MemoryEngine.Connection;
        BusyLock busyLock = this.dedicatedConnection != null ? this.DedicatedBusyLock : this.Manager!.MemoryEngine.BusyLocker;
        
        this.myRunningMachine = new LuaScriptMachine(this, this.myChunk, connection, busyLock);
        this.myRunningMachine.LinePrinted += this.OnLuaLinePrinted;
        this.myRunningMachine.Start();
        this.IsRunningChanged?.Invoke(this);
        return true;
    }

    private void OnLuaScriptCompleted() {
        Task<LuaValue[]> task = this.myRunningMachine!.CompletionTask;
        try {
            LuaValue[] values = task.GetAwaiter().GetResult();
            if (values.Length > 0) {
                this.myRunningMachine!.Print(string.Join(", ", values));
            }
        }
        catch (OperationCanceledException) {
            // ignored
        }
        catch (LuaRuntimeException ex) {
            string message = ex.Message;
            string traceback = ex.LuaTraceback.StackFrames.Length > 0 ? ex.LuaTraceback.ToString() : "";
            this.myRunningMachine!.Print(message + (string.IsNullOrEmpty(traceback) ? "" : Environment.NewLine + traceback));
        }
        catch (Exception ex) {
            this.myRunningMachine!.Print(ex.ToString());
        }

        this.myRunningMachine = null;
        this.myRunningTcs!.SetResult(); // notify continuations
        this.myRunningTcs = null;
        this.IsRunningChanged?.Invoke(this);
        this.IsTryingToStop = false;
    }

    private void OnLuaLinePrinted(string obj) {
        lock (this.consoleQueueLock) {
            if (this.queuedLines.Count > 500) {
                // remove some lines. Lost data, but it's not like they'll see it anyway
                this.queuedLines.RemoveRange(0, 200);
            }

            this.queuedLines.Add(obj);
            this.rldaPrintToConsole.InvokeAsync();
        }
    }

    public void PrintToConsole(string line) {
        string[] subLines = line.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        foreach (string subLine in subLines) {
            this.OnLuaLinePrinted(subLine);
        }
    }

    public void CheckNotRunning(string errorMessage = "Script is currently running") {
        if (this.IsRunning)
            throw new InvalidOperationException(errorMessage);
    }

    // Invoked when lua thread starts
    internal static void InternalOnLuaMachineStarting(Script script) {
    }

    // Invoked when lua machine is fully booted and about to start running code.
    // May not be called if the user somehow manages to stop the script so quickly that
    // it never fully boots up.
    internal static void InternalOnLuaMachineRunning(Script script) {
    }

    // Invoked when the lua thread is about to exit.
    internal static void InternalOnLuaMachineStopped(Script script) {
        ApplicationPFX.Instance.Dispatcher.Post(t => ((Script) t!).OnLuaScriptCompleted(), script, DispatchPriority.Send);
    }

    /// <summary>
    /// Compiles LUA source code into a chunk.
    /// </summary>
    /// <param name="source">The source code</param>
    /// <param name="progress">Optional compilation progress</param>
    /// <param name="cancellationToken">Cancel the compilation process</param>
    /// <returns>The compiled chunk</returns>
    public static async Task<Chunk> CompileSource(string source, string chunkName, IActivityProgress? progress, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        TaskCompletionSource<Chunk> tcs = new TaskCompletionSource<Chunk>();
        Thread threadParseAndCompile = new Thread(() => {
            bool hasResult = false;
            try {
                cancellationToken.ThrowIfCancellationRequested();
                if (progress != null)
                    progress.Text = "[Lexer] Parsing...";

                Lexer lexer = new Lexer() {
                    Source = source.AsMemory(),
                    ChunkName = chunkName
                };

                Parser parser = new Parser() { ChunkName = chunkName };
                while (lexer.MoveNext()) {
                    cancellationToken.ThrowIfCancellationRequested();
                    parser.Add(lexer.Current);
                }

                if (progress != null)
                    progress.Text = "Creating syntax tree...";

                LuaSyntaxTree syntaxTree = parser.Parse();
                Chunk compilationResult = LuaCompiler.Default.Compile(syntaxTree, chunkName);
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