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
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Engine.Debugging;
using MemEngine360.Engine.FileBrowsing;
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.SavedAddressing;
using MemEngine360.ModTools;
using MemEngine360.PointerScanning;
using MemEngine360.Scripting;
using MemEngine360.Sequencing;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI;
using PFXToolKitUI.Activities;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Composition;
using PFXToolKitUI.History;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Shortcuts;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Engine;

/// <summary>
/// The main manager class for a the memory engine window. Also provides utilities for reading/writing data values
/// </summary>
[DebuggerDisplay("IsBusy = {IsConnectionBusy}, Connection = {Connection}")]
public class MemoryEngine : IComponentManager, IUserLocalContext {
    public static readonly DataKey<MemoryEngine> EngineDataKey = DataKeys.Create<MemoryEngine>("MemoryEngine");

    /// <summary>
    /// A data key used by the connection change notification to tell whether a disconnection originated from the notification's "Disconnect" command
    /// </summary>
    public static readonly DataKey<bool> IsDisconnectFromNotification = DataKeys.Create<bool>("IsDisconnectFromNotification");

    private IConsoleConnection? connection;
    private ulong currentConnectionAboutToChangeFrame;

    /// <summary>
    /// Gets memory engine's history manager
    /// </summary>
    public HistoryManager HistoryManager { get; } = new HistoryManager();

    /// <summary>
    /// Gets this engine's busy lock, which is used to synchronize our connection
    /// </summary>
    public BusyLock BusyLock { get; }

    /// <summary>
    /// Gets the current console connection. This can only change on the main thread
    /// <para>
    /// It's crucial that when using any command that requires sending/receiving data from the console that
    /// it is synchronized with <see cref="TryBeginBusyOperation"/> or any of the async overloads, because,
    /// connections may not be thread-safe (but may implement fail-safety when trying to read/write concurrently)
    /// </para>
    /// <para>
    /// There are two ways to interact with a connection. The first is try get lock, otherwise do nothing
    /// </para>
    /// <code>
    /// <![CDATA[
    /// using IBusyToken? token = engine.BeginBusyOperation();
    /// if (token != null && engine.Connection != null) {
    ///     // do work involving connection
    /// }
    /// ]]>
    /// </code>
    /// <para>
    /// Alternatively, you can use <see cref="BeginBusyOperationAsync"/> which waits until you get the
    /// token and accepts a cancellation token, or use <see cref="BeginBusyOperationActivityAsync"/> which
    /// creates an activity to show the user you're waiting for the busy operations to complete
    /// </para>
    /// </summary>
    public IConsoleConnection? Connection => this.connection;

    /// <summary>
    /// Gets the <see cref="Connections.UserConnectionInfo"/> that was used to connect to a console. It is set
    /// by calling <see cref="SetConnection"/> with a non-null connection and non-null UCInfo, before <see cref="ConnectionChanged"/> is fired.
    /// <para>
    /// This is used to support reconnecting to the console when the connection was lost, without having to reconfigure all the options
    /// </para>
    /// </summary>
    public UserConnectionInfo? LastUserConnectionInfo { get; private set; }

    /// <summary>
    /// Gets or sets if the memory engine is currently busy, e.g. reading or writing data.
    /// This will never be true when <see cref="Connection"/> is null
    /// </summary>
    public bool IsConnectionBusy => this.BusyLock.IsBusy;

    public ScanningProcessor ScanningProcessor { get; }

    public TaskSequenceManager TaskSequenceManager { get; }

    public AddressTableManager AddressTableManager { get; }

    public FileTreeExplorer FileTreeExplorer { get; }

    public PointerScanner PointerScanner { get; }

    public ConsoleDebugger ConsoleDebugger { get; }

    public ScriptingManager ScriptingManager { get; }

    public ModToolManager ModToolManager { get; }

    public IMutableContextData UserContext { get; } = new ContextData();

    /// <summary>
    /// Gets or sets if the memory engine is in the process of shutting down. Prevents scanning working
    /// </summary>
    public bool IsShuttingDown {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.IsShuttingDownChanged);
    }

    /// <summary>
    /// Gets the tools menu for memory engine
    /// </summary>
    public MenuEntryGroup ToolsMenu { get; }

    /// <summary>
    /// Gets the Remote Controls menu for memory engine
    /// </summary>
    public MenuEntryGroup RemoteControlsMenu { get; }

    /// <summary>
    /// An async event fired when a connection is most likely about to change. This can be used by custom activities
    /// to cancel operations that are using the connection. There is no guarantee that the connection will actually
    /// change, and this event may get fired multiple times before the connection really changes
    /// <para>
    /// Beware, handlers are invoked in a background task (via <see cref="Task.Run(Func{Task})"/>), not on the
    /// main thread, so you must jump back to the main thread if required. <see cref="OperationCanceledException"/> exceptions are swallowed
    /// and regular exceptions are posted back to the main thread which will crash the application
    /// </para>
    /// <para>
    /// Note! This may not be called at all; maybe the connection is changing in a non-async context and the nature of the
    /// context makes it unable to wait for handlers to stop using the connection. So this event is just a hint to prevent
    /// potential timeout/IO exceptions from popping up in the UI and inconveniencing the user
    /// </para>
    /// <para>
    /// Also note, this event is fired while the busy token is acquired, so handlers cannot obtain it. However, if you absolutely
    /// need to do busy operations in this event, then do them on the main thread (see <see cref="IDispatcher.InvokeAsync"/>) and do
    /// not await any code, otherwise you risk multiple handlers invoking busy operation code concurrently and if you await on the
    /// main thread then you risk another handler's code being invoked between your await usage
    /// </para>
    /// </summary>
    public event AsyncEventHandler<ConnectionChangingEventArgs>? ConnectionAboutToChange;

    /// <summary>
    /// Fired when <see cref="Connection"/> changes. It is crucial that no 'busy' operations
    /// are performed in the event handlers, otherwise, a deadlock could occur.
    /// </summary>
    public event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;

    /// <summary>
    /// An event fired when <see cref="IsShuttingDown"/> changes. Ideally this should only be fired once per instance of <see cref="MemoryEngine"/>
    /// </summary>
    public event EventHandler? IsShuttingDownChanged;

    /// <summary>
    /// Fired when the <see cref="IsConnectionBusy"/> state changes. It is crucial that no 'busy' operations are performed
    /// in the event handlers, otherwise, a deadlock could occur.
    /// <para>
    /// It's also important that exceptions are not thrown in the handlers, because they will be swallowed and never see
    /// the light of day, and the next handlers in the list will not be invoked, potentially leading to application wide corruption
    /// </para>
    /// </summary>
    public event EventHandler? IsBusyChanged;

    ComponentStorage IComponentManager.ComponentStorage => field ??= new ComponentStorage(this);

    public MemoryEngine() {
        this.BusyLock = new BusyLock();
        this.BusyLock.IsBusyChanged += (s, _) => this.IsBusyChanged?.Invoke(this, EventArgs.Empty);
        this.ScanningProcessor = new ScanningProcessor(this);
        this.AddressTableManager = new AddressTableManager(this);
        this.FileTreeExplorer = new FileTreeExplorer(this);
        this.TaskSequenceManager = new TaskSequenceManager(this);
        this.PointerScanner = new PointerScanner(this);
        this.ConsoleDebugger = new ConsoleDebugger(this);
        this.ScriptingManager = new ScriptingManager(this);

        MenuEntryGroup modToolMenu = new MenuEntryGroup("Mod Tools") {
            UniqueID = "memoryengine.tools.modtools",
            Items = {
                new CommandMenuEntry("commands.modtools.ShowModToolsWindowCommand", "Mod Tools Manager"),
                new SeparatorEntry()
            }
        };

        this.ModToolManager = new ModToolManager(this, modToolMenu);

        this.ToolsMenu = new MenuEntryGroup("_Tools") {
            UniqueID = "memoryengine.tools",
            Items = {
                new CommandMenuEntry("commands.memengine.ShowMemoryViewCommand", "_Memory View", "Opens the memory viewer/hex editor"),
                new CommandMenuEntry("commands.memengine.ShowTaskSequencerCommand", "_Task Sequencer", "Opens the task sequencer"),
                new CommandMenuEntry("commands.memengine.ShowDebuggerCommand", "_Debugger"),
                new CommandMenuEntry("commands.memengine.ShowPointerScannerCommand", "_Pointer Scanner"),
                new CommandMenuEntry("commands.memengine.ShowConsoleEventViewerCommand", "_Event Viewer", "Shows the event viewer window for viewing console system events"),
                new CommandMenuEntry("commands.scripting.ShowScriptingWindowCommand", "_Scripting"),
                // new CommandMenuEntry("commands.structviewer.ShowStructViewerWindowCommand", "Struct Viewer"),
                new SeparatorEntry(),
                new CommandMenuEntry("commands.memengine.ShowModulesCommand", "Module E_xplorer", "Opens a window which presents the modules"),
                new CommandMenuEntry("commands.memengine.remote.ShowMemoryRegionsCommand", "Memory Region Explorer", "Opens a window which presents all memory regions"),
                new CommandMenuEntry("commands.memengine.ShowFileBrowserCommand", "File Explorer"),
                new SeparatorEntry(),
                modToolMenu
            }
        };

        // update all tools when connection changes, since most if not all tools rely on a connection
        this.ToolsMenu.AddCanExecuteChangeUpdaterForEvent(EngineDataKey, nameof(this.ConnectionChanged));

        this.RemoteControlsMenu = new MenuEntryGroup("_Remote Controls") {
            ProvideDisabledHint = (ctx, registry) => {
                if (!EngineDataKey.TryGetContext(ctx, out MemoryEngine? engine))
                    return null;

                if (engine.Connection == null) {
                    IReadOnlyCollection<ShortcutEntry> scList = ShortcutManager.Instance.GetShortcutsByCommandId("commands.memengine.OpenConsoleConnectionDialogCommand");
                    string shortcuts = scList.Select(x => x.Shortcut.ToString()!).JoinString(", ", " or ");
                    if (!string.IsNullOrEmpty(shortcuts))
                        shortcuts = ". Use the shortcut(s) to connect: " + shortcuts;
                    return new SimpleDisabledHintInfo("Not connected", "Connect to a console to use remote commands" + shortcuts);
                }

                return null;
            }
        };

        this.ConnectionChanged += this.OnConnectionChanged;

        Task.Run(async () => {
            long timeSinceRefreshedAddresses = DateTime.Now.Ticks;
            BasicApplicationConfiguration cfg = BasicApplicationConfiguration.Instance;

            while (!this.IsShuttingDown) {
                IConsoleConnection? conn = this.connection;
                if (conn != null && conn.IsClosed) {
                    await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                        // rarely not the case (depending on how quickly the callback runs on the main thread)
                        if (conn == this.Connection) {
                            this.CheckConnection();
                        }
                    });
                }

                await Task.Delay(250);
                if (cfg.IsAutoRefreshResultsEnabled && !this.IsShuttingDown) {
                    if ((DateTime.Now.Ticks - timeSinceRefreshedAddresses) >= (cfg.RefreshRateMillis * TimeSpan.TicksPerMillisecond)) {
                        await await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => this.ScanningProcessor.RefreshSavedAddressesAsync());
                        timeSinceRefreshedAddresses = DateTime.Now.Ticks;
                    }
                }
            }
        });
    }

    private void OnConnectionChanged(object? o, ConnectionChangedEventArgs args) {
        this.RemoteControlsMenu.Items.Clear();
        if (args.NewConnection != null) {
            this.RemoteControlsMenu.Items.AddRange(args.NewConnection.ConnectionType.GetRemoteContextOptions());
        }
    }

    /// <summary>
    /// Fires our <see cref="ConnectionAboutToChange"/> event and waits for all handlers to complete.
    /// This method will not throw any exceptions encountered during the event handlers
    /// </summary>
    /// <param name="topLevel"></param>
    /// <param name="frame">The connection changing frame. See docs for <see cref="GetNextConnectionChangeFrame"/> for more info</param>
    /// <exception cref="Exception"></exception>
    public async Task BroadcastConnectionAboutToChange(ITopLevel topLevel, ulong frame) {
        Delegate[]? list = this.ConnectionAboutToChange?.GetInvocationList();
        if (list == null || list.Length <= 0) {
            return;
        }

        List<SubActivity> progressions = new List<SubActivity>(list.Length);
        Task[] tasks = new Task[list.Length];
        for (int i = 0; i < list.Length; i++) {
            AsyncEventHandler<ConnectionChangingEventArgs> handler = (AsyncEventHandler<ConnectionChangingEventArgs>) list[i];
            DispatcherActivityProgress progress = new DispatcherActivityProgress();
            TaskCompletionSource tcs = new TaskCompletionSource();
            progressions.Add(new SubActivity(progress, tcs.Task, null));

            tasks[i] = Task.Run(async () => {
                progress.IsIndeterminate = true;
                progress.Caption = "Disconnection Handler";
                progress.Text = "Waiting";

                try {
                    await handler(this, new ConnectionChangingEventArgs(frame, progress));
                }
                catch (OperationCanceledException) {
                    // ignored
                }
                catch (Exception e) {
                    AppLogger.Instance.WriteLine("Exception invoking connection changing handler: " + e.GetToString());
                }

                tcs.SetResult();
            });
        }

        Task whenAllHandlersDoneTask = Task.WhenAll(tasks);
        using (CancellationTokenSource cts = new CancellationTokenSource()) {
            // Grace period for all activities to become cancelled
            try {
                await Task.WhenAny(Task.Delay(250, cts.Token), whenAllHandlersDoneTask);
                await cts.CancelAsync();
            }
            catch (OperationCanceledException) {
                // ignored
            }
        }

        if (!whenAllHandlersDoneTask.IsCompleted) {
            if (IForegroundActivityService.TryGetInstance(out IForegroundActivityService? service)) {
                await service.WaitForSubActivities(topLevel, progressions, CancellationToken.None);
            }
            else {
                await whenAllHandlersDoneTask;
            }
        }
    }

    /// <summary>
    /// Sets the current connection, with the given cause. Must be called on main thread
    /// </summary>
    /// <param name="token">The busy operation token that is valid</param>
    /// <param name="frame">The connection change frame. Set to 0 if you have no idea what this is used for</param>
    /// <param name="newConnection">The new connection object</param>
    /// <param name="cause">The cause for connection change</param>
    /// <exception cref="InvalidOperationException">Token is invalid</exception>
    /// <exception cref="ArgumentException">New connection is non-null when cause is <see cref="ConnectionChangeCause.LostConnection"/></exception>
    public void SetConnection(IBusyToken busyToken, ulong frame, IConsoleConnection? newConnection, ConnectionChangeCause cause, UserConnectionInfo? userConnectionInfo = null) {
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();
        this.BusyLock.ValidateToken(busyToken);
        if (newConnection != null && (cause == ConnectionChangeCause.LostConnection || cause == ConnectionChangeCause.ConnectionError))
            throw new ArgumentException($"Cause cannot be {cause} when setting connection to a non-null value");

        if (newConnection == null && userConnectionInfo != null)
            throw new ArgumentException(nameof(userConnectionInfo) + " is non-null when " + nameof(newConnection) + " is null");

        // we don't necessarily need to access connection under lock since if
        // we have a valid busy token then nothing can modify it
        IConsoleConnection? oldConnection = this.connection;
        if (ReferenceEquals(oldConnection, newConnection))
            throw new ArgumentException("Cannot set the connection to the same value");

        if (oldConnection != null)
            oldConnection.Closed -= this.OnConnectionClosed;

        // Even if Closed is called right as we add the handler, unless Closed is maliciously implemented,
        // it cannot be invoked in this call frame. But it could be invoked on a background thread just before
        // we obtain the lock, but it would have to go through the dispatcher to call SetConnection
        if (newConnection != null)
            newConnection.Closed += this.OnConnectionClosed;

        // ConnectionChanged is invoked under the lock to enforce busy operation rules
        lock (this.BusyLock.CriticalLock) {
            this.connection = newConnection;
            if (newConnection != null)
                this.LastUserConnectionInfo = userConnectionInfo;

            this.ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs(frame, oldConnection, newConnection, cause));
        }

        // Try to disconnect in the future, just in case the connection was immediately closed.
        // It's generally safer to do it this way than to not actually set the connection in SetConnection
        ApplicationPFX.Instance.Dispatcher.Post(static en => ((MemoryEngine) en!).CheckConnection(), this, DispatchPriority.Background);
    }

    /// <summary>
    /// Increments and gets the next frame used by the connection changing/changed mechanism. Frames are used to
    /// determine if the connection is changing and actual change happened within the same context, which can
    /// be used to then resume an operation.
    /// <para>
    /// To use this system, you would handle <see cref="ConnectionAboutToChange"/> and stop your operation(s) and store the frame
    /// in a field. Then handle <see cref="ConnectionChanged"/>, and check if the frame given equals your stored frame.
    /// If they're equal, you can resume your operation
    /// </para>
    /// </summary>
    /// <returns>The next frame. Will never return 0</returns>
    public ulong GetNextConnectionChangeFrame() {
        ulong frame;
        do {
            frame = Interlocked.Increment(ref this.currentConnectionAboutToChangeFrame);
        } while (frame == 0); // extra safe

        return frame;
    }

    /// <summary>
    /// Tries to begin a busy operation that uses the <see cref="Connection"/>. Dispose to finish the busy operation.
    /// <para>
    /// It is vital to use the busy-operation system when reading from or writing to the <see cref="Connection"/>,
    /// because the connection may not support concurrent commands, which may result in corruption in the pipeline
    /// </para>
    /// </summary>
    /// <returns>A token to dispose when the operation is completed. Returns null if currently busy</returns>
    public IBusyToken? TryBeginBusyOperation() => this.BusyLock.TryBeginBusyOperation();

    /// <summary>
    /// Begins a busy operation that uses the <see cref="Connection"/>, by waiting for existing busy operations to finish 
    /// </summary>
    /// <param name="cancellationToken">Used to cancel the operation, causing the task to return a null busy token</param>
    /// <param name="timeoutMilliseconds">An optional timeout value. When the amount of time elapses, we return null</param>
    /// <returns>The acquired token, or null if the task was cancelled. Dispose to finish the busy operation</returns>
    public Task<IBusyToken?> BeginBusyOperationAsync(CancellationToken cancellationToken) {
        return this.BusyLock.BeginBusyOperation(cancellationToken);
    }

    /// <summary>
    /// Begins a busy operation that uses the <see cref="Connection"/>. Waits for existing busy operations to finish,
    /// or for the timeout period to elapse or the cancellation token to become cancelled, in which case this method returns null
    /// </summary>
    /// <param name="timeoutMilliseconds">The maximum amount of time to wait to try and begin the operations</param>
    /// <param name="cancellationToken">Used to cancel the operation, causing the task to return a null busy token</param>
    /// <returns>The token, or null, if the timeout elapsed or the cancellation token becomes cancelled</returns>
    public Task<IBusyToken?> BeginBusyOperationAsync(int timeoutMilliseconds, CancellationToken cancellationToken = default) {
        return this.BusyLock.BeginBusyOperation(timeoutMilliseconds, cancellationToken);
    }

    /// <summary>
    /// Tries to begin a busy operation. If we could not get the token immediately, we start
    /// a new activity and try to get it asynchronously with the text 'waiting for busy operations' 
    /// </summary>
    /// <param name="caption">The caption set as the <see cref="IActivityProgress.Caption"/> property</param>
    /// <param name="message">The message set as the <see cref="IActivityProgress.Text"/> property</param>
    /// <param name="cancellationToken">Additional cancellation source for the activity</param>
    /// <returns>
    /// A task with the token, or null if the user cancelled the operation or some other weird error occurred
    /// </returns>
    public Task<IBusyToken?> BeginBusyOperationUsingActivityAsync(string caption = "New Operation", string message = BusyLock.WaitingMessage, CancellationToken cancellationToken = default) {
        return this.BusyLock.BeginBusyOperationUsingActivity(caption, message, cancellationToken);
    }

    /// <summary>
    /// Gets a busy token via <see cref="BeginBusyOperationActivityAsync(string)"/> and invokes a callback if the connection is available
    /// </summary>
    /// <param name="action">The callback to invoke when we have the token</param>
    /// <param name="message">A message to pass to the <see cref="BeginBusyOperationActivityAsync(string)"/> method</param>
    /// <param name="cancellationToken">Additional cancellation source for the activity</param>
    /// <returns>True if the callback action was run, otherwise False meaning we couldn't get the token or the connection was null/closed</returns>
    public async Task<bool> BeginBusyOperationUsingActivityAsync(Func<IBusyToken, IConsoleConnection, Task> action, string caption = "New Operation", string message = BusyLock.WaitingMessage, CancellationToken cancellationToken = default) {
        if (this.connection == null) {
            return false;
        }

        using IBusyToken? token = await this.BeginBusyOperationUsingActivityAsync(caption, message, cancellationToken);
        IConsoleConnection c;
        if (token != null && (c = this.connection) != null && !c.IsClosed) {
            await action(token, c);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Begins a busy operation from within an already running activity, and only runs the callback function
    /// when we have a connection after the busy token is acquired
    /// </summary>
    /// <param name="function">The callback to run with the token and open connection</param>
    /// <param name="busyCancellation">A cancellation token used to stop trying to acquire the busy token</param>
    public async Task<bool> BeginBusyOperationFromActivityAsync(Func<IBusyToken, IConsoleConnection, Task> function, CancellationToken busyCancellation = default) {
        if (this.connection == null) {
            return false;
        }

        using IBusyToken? token = await this.BusyLock.BeginBusyOperationFromActivity(busyCancellation);
        IConsoleConnection c;
        if (token != null && (c = this.connection) != null && !c.IsClosed) {
            await function(token, c);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Begins a busy operation from within an already running activity, and only runs the callback function
    /// when we have a connection after the busy token is acquired
    /// </summary>
    /// <param name="function">The callback to run with the token and open connection</param>
    /// <param name="busyCancellation">A cancellation token used to stop trying to acquire the busy token</param>
    /// <typeparam name="T">The type of value the callback returns</typeparam>
    /// <returns>
    /// A task containing the result of the function, or <see cref="Optional{T}.Empty"/> if
    /// it could not be called (i.e. not connected or could not begin busy operation)
    /// #</returns>
    public async Task<Optional<T>> BeginBusyOperationFromActivityAsync<T>(Func<IBusyToken, IConsoleConnection, Task<T>> function, CancellationToken busyCancellation = default) {
        if (this.connection == null) {
            return default;
        }

        using IBusyToken? token = await this.BusyLock.BeginBusyOperationFromActivity(busyCancellation);
        IConsoleConnection c;
        if (token != null && (c = this.connection) != null && !c.IsClosed) {
            return await function(token, c);
        }

        return default;
    }

    public void CheckConnection(ConnectionChangeCause likelyCause = ConnectionChangeCause.LostConnection) {
        // CheckConnection is just a helpful method to clear connection if it's 
        // disconnected internally, therefore, we don't need over the top synchronization,
        // because any code that actually tries to read/write will be async and can handle
        // the timeout exceptions
        IConsoleConnection? c = this.connection;
        if (c != null && c.IsClosed) {
            if (ApplicationPFX.Instance.Dispatcher.CheckAccess()) {
                using IBusyToken? t = this.TryBeginBusyOperation();
                if (t != null && this.TryDisconnectForLostConnection(t, likelyCause)) {
                    return;
                }
            }

            ApplicationPFX.Instance.Dispatcher.Post(() => {
                using IBusyToken? t = this.TryBeginBusyOperation();
                if (t != null) {
                    this.TryDisconnectForLostConnection(t, likelyCause);
                }
            });
        }
    }

    private void OnConnectionClosed(object? sender, EventArgs e) {
        if (sender == this.connection) {
            this.CheckConnection();
        }
    }

    /// <summary>
    /// Attempts to auto-disconnect the connection immediately if it is no longer actually connected (<see cref="IConsoleConnection.IsClosed"/> is true)
    /// </summary>
    /// <param name="token"></param>
    public void CheckConnection(IBusyToken token, ConnectionChangeCause likelyCause = ConnectionChangeCause.LostConnection) {
        this.BusyLock.ValidateToken(token);
        this.TryDisconnectForLostConnection(token, likelyCause);
    }

    private bool TryDisconnectForLostConnection(IBusyToken token, ConnectionChangeCause cause) {
        IConsoleConnection? conn = this.connection;
        if (conn == null)
            return true;
        if (!conn.IsClosed)
            return false;

        this.SetConnection(token, 0, null, cause);
        return true;
    }

    /// <summary>
    /// Returns the maximum number of bytes a data value can take up.
    /// </summary>
    /// <param name="dataType"></param>
    /// <param name="stringType"></param>
    /// <param name="stringOrArrayLength"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static int GetMaximumDataValueSize(DataType dataType, StringType stringType, int stringOrArrayLength, bool isLittleEndian) {
        switch (dataType) {
            case DataType.Byte:      return 1;
            case DataType.Int16:     return 2;
            case DataType.Int32:     return 4;
            case DataType.Int64:     return 8;
            case DataType.Float:     return 4;
            case DataType.Double:    return 8;
            case DataType.String:    return stringType.ToEncoding(isLittleEndian).GetMaxByteCount(stringOrArrayLength);
            case DataType.ByteArray: return stringOrArrayLength;
            default:                 throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
        }
    }

    public static int GetMaximumDataValueSize(AddressTableEntry entry, bool isLittleEndian) {
        return GetMaximumDataValueSize(entry.DataType, entry.StringType, entry.DataType == DataType.String ? entry.StringLength : entry.ArrayLength, isLittleEndian);
    }
    
    public static int GetMaximumDataValueSize(ScanResultViewModel result, bool isLittleEndian) {
        return GetMaximumDataValueSize(result.DataType, result.StringType, result.DataType == DataType.String ? result.CurrentStringLength : result.CurrentArrayLength, isLittleEndian);
    }
    
    /// <summary>
    /// Reads a data value from the console
    /// </summary>
    /// <param name="connection">The connection to read the value from</param>
    /// <param name="address">The address to read at</param>
    /// <param name="dataType">The type of value we want to read</param>
    /// <param name="stringType"></param>
    /// <param name="strlen">The length of the string. Only used when the data type is <see cref="DataType.String"/></param>
    /// <param name="arrlen">The amount of array elements. Only used when the data type is <see cref="DataType.ByteArray"/> (or any array type, if we support that in the future)</param>
    /// <returns>The data value</returns>
    /// <exception cref="ArgumentOutOfRangeException">Invalid data type</exception>
    public static async Task<IDataValue> ReadDataValue(IConsoleConnection connection, uint address, DataType dataType, StringType stringType, int strlen, int arrlen) {
        switch (dataType) {
            case DataType.Byte:      return new DataValueByte(await connection.ReadByte(address).ConfigureAwait(false));
            case DataType.Int16:     return new DataValueInt16(await connection.ReadValue<short>(address).ConfigureAwait(false));
            case DataType.Int32:     return new DataValueInt32(await connection.ReadValue<int>(address).ConfigureAwait(false));
            case DataType.Int64:     return new DataValueInt64(await connection.ReadValue<long>(address).ConfigureAwait(false));
            case DataType.Float:     return new DataValueFloat(await connection.ReadValue<float>(address).ConfigureAwait(false));
            case DataType.Double:    return new DataValueDouble(await connection.ReadValue<double>(address).ConfigureAwait(false));
            case DataType.String:    return new DataValueString(await connection.ReadString(address, strlen, stringType.ToEncoding(connection.IsLittleEndian)).ConfigureAwait(false), stringType);
            case DataType.ByteArray: return new DataValueByteArray(await connection.ReadBytes(address, arrlen).ConfigureAwait(false));
            default:                 throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
        }
    }

    /// <summary>
    /// Same as <see cref="ReadAsDataValue(IConsoleConnection,uint,MemEngine360.Engine.Modes.DataType,StringType,uint,uint)"/>
    /// except this method uses the information from the given <see cref="IDataValue"/> to read the value in the exact same format.
    /// This basically just reads the latest version of the given data value
    /// </summary>
    /// <param name="connection">The connection to read the value from</param>
    /// <param name="address">The address to read at</param>
    /// <param name="value">The value we use things from like string type, and strlen/arrlen length</param>
    /// <returns>The latest version of <see cref="value"/></returns>
    /// <exception cref="Exception">Invalid data type</exception>
    public static async Task<IDataValue> ReadDataValue(IConsoleConnection connection, uint address, IDataValue value) {
        switch (value.DataType) {
            case DataType.Byte:   return new DataValueByte(await connection.ReadByte(address).ConfigureAwait(false));
            case DataType.Int16:  return new DataValueInt16(await connection.ReadValue<short>(address).ConfigureAwait(false));
            case DataType.Int32:  return new DataValueInt32(await connection.ReadValue<int>(address).ConfigureAwait(false));
            case DataType.Int64:  return new DataValueInt64(await connection.ReadValue<long>(address).ConfigureAwait(false));
            case DataType.Float:  return new DataValueFloat(await connection.ReadValue<float>(address).ConfigureAwait(false));
            case DataType.Double: return new DataValueDouble(await connection.ReadValue<double>(address).ConfigureAwait(false));
            case DataType.String:
                StringType sType = ((DataValueString) value).StringType;
                return new DataValueString(await connection.ReadString(address, ((DataValueString) value).Value.Length, sType.ToEncoding(connection.IsLittleEndian)).ConfigureAwait(false), sType);
            case DataType.ByteArray: return new DataValueByteArray(await connection.ReadBytes(address, ((DataValueByteArray) value).Value.Length).ConfigureAwait(false));
            default:                 throw new Exception("Value contains an invalid data type");
        }
    }

    /// <summary>
    /// Writes a data value to the given address
    /// </summary>
    /// <param name="connection">The connection</param>
    /// <param name="address">The address to write the value at</param>
    /// <param name="dt"></param>
    /// <param name="value"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static async Task WriteDataValue(IConsoleConnection connection, uint address, IDataValue value, bool appendNullCharForString = true) {
        switch (value.DataType) {
            case DataType.Byte:   await connection.WriteByte(address, ((DataValueByte) value).Value).ConfigureAwait(false); break;
            case DataType.Int16:  await connection.WriteValue(address, ((DataValueInt16) value).Value).ConfigureAwait(false); break;
            case DataType.Int32:  await connection.WriteValue(address, ((DataValueInt32) value).Value).ConfigureAwait(false); break;
            case DataType.Int64:  await connection.WriteValue(address, ((DataValueInt64) value).Value).ConfigureAwait(false); break;
            case DataType.Float:  await connection.WriteValue(address, ((DataValueFloat) value).Value).ConfigureAwait(false); break;
            case DataType.Double: await connection.WriteValue(address, ((DataValueDouble) value).Value).ConfigureAwait(false); break;
            case DataType.String: {
                byte[] array = ((DataValueString) value).GetBytes(connection.IsLittleEndian, appendNullCharForString, out int length);
                await connection.WriteBytes(address, array, 0, length).ConfigureAwait(false);
                break;
            }
            case DataType.ByteArray: await connection.WriteBytes(address, ((DataValueByteArray) value).Value).ConfigureAwait(false); break;
            default:                 throw new InvalidOperationException("Data value's data type is invalid: " + value.DataType);
        }
    }
}

public enum NumericDisplayType {
    /// <summary>
    /// The normal representation for the data types. Integers (except byte) are signed,
    /// and floating point numbers are displayed as decimal numbers
    /// </summary>
    Normal,

    /// <summary>
    /// Displays integers as unsigned. Does not affect floating point types nor byte (since it's always unsigned)
    /// </summary>
    Unsigned,

    /// <summary>
    /// Displays numbers as hexadecimal. Floating point numbers have their binary data reinterpreted as an
    /// integer and that is shown as hexadecimal (<see cref="BitConverter.Int32BitsToSingle"/>)
    /// </summary>
    Hexadecimal,
}

public static class NumericDisplayTypeExtensions {
    /// <summary>
    /// Displays the value as a string using the given numeric display type. Is it assumed that integer values are signed except for <see cref="byte"/>
    /// </summary>
    /// <param name="dt">The numeric display type</param>
    /// <param name="type">The data type, for performance reasons</param>
    /// <param name="value">The value. Must be byte, short, int, long, float, double or string. Only byte can be an unsigned integer type</param>
    /// <returns>The value as a string</returns>
    /// <exception cref="ArgumentOutOfRangeException">Unknown data type</exception>
    public static string AsString(this NumericDisplayType dt, DataType type, object value) {
        bool hex = dt == NumericDisplayType.Hexadecimal, unsigned = dt == NumericDisplayType.Unsigned;
        switch (type) {
            case DataType.Byte:      return hex ? ((byte) value).ToString("X2") : value.ToString()!;
            case DataType.Int16:     return hex ? ((short) value).ToString("X4") : (unsigned ? ((ushort) (short) value).ToString() : value.ToString()!);
            case DataType.Int32:     return hex ? ((int) value).ToString("X8") : (unsigned ? ((uint) (int) value).ToString() : value.ToString()!);
            case DataType.Int64:     return hex ? ((long) value).ToString("X16") : (unsigned ? ((ulong) (long) value).ToString() : value.ToString()!);
            case DataType.Float:     return hex ? BitConverter.SingleToUInt32Bits((float) value).ToString("X4") : ((float) value).ToString("F8");
            case DataType.Double:    return hex ? BitConverter.DoubleToUInt64Bits((double) value).ToString("X8") : ((double) value).ToString("F16");
            case DataType.String:    return value.ToString()!;
            case DataType.ByteArray: return NumberUtils.BytesToHexAscii((byte[]) value);
            default:                 throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }
}

public readonly struct ConnectionChangingEventArgs(ulong frame, IActivityProgress progress) {
    /// <summary>
    /// Gets the changing frame. This is unique for each call to <see cref="MemoryEngine.BroadcastConnectionAboutToChange"/>,
    /// and can be used to track which call was followed by <see cref="MemoryEngine.ConnectionChanged"/>
    /// </summary>
    public ulong Frame { get; } = frame;

    /// <summary>
    /// Gets the progress for the specific handler to <see cref="MemoryEngine.ConnectionAboutToChange"/>.
    /// This is used to show the user the possible reason for why the app is not responding
    /// </summary>
    public IActivityProgress Progress { get; } = progress;
}

public readonly struct ConnectionChangedEventArgs(ulong frame, IConsoleConnection? oldConnection, IConsoleConnection? newConnection, ConnectionChangeCause cause) {
    /// <summary>
    /// Gets the changing frame.
    /// </summary>
    public ulong Frame { get; } = frame;

    /// <summary>
    /// Gets the old connection
    /// </summary>
    public IConsoleConnection? OldConnection { get; } = oldConnection;

    /// <summary>
    /// Gets the new connection
    /// </summary>
    public IConsoleConnection? NewConnection { get; } = newConnection;

    /// <summary>
    /// Gets the cause for the connection changing (e.g. user disconnected, we lost connection, etc.)
    /// </summary>
    public ConnectionChangeCause Cause { get; } = cause;
}