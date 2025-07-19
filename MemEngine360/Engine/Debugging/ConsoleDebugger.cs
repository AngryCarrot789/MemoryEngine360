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
using MemEngine360.Connections;
using MemEngine360.Connections.Traits;
using MemEngine360.Connections.Utils;
using MemEngine360.Engine.Events;
using MemEngine360.Engine.Events.XbdmEvents;
using MemEngine360.XboxBase;
using PFXToolKitUI;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Destroying;
using PFXToolKitUI.Utils.RDA;

namespace MemEngine360.Engine.Debugging;

public delegate void ConsoleDebuggerActiveThreadChangedEventHandler(ConsoleDebugger sender, ThreadEntry? oldActiveThread, ThreadEntry? newActiveThread);

public delegate void ConsoleDebuggerConnectionChangedEventHandler(ConsoleDebugger sender, IConsoleConnection? oldConnection, IConsoleConnection? newConnection);

public delegate void ConsoleDebuggerEventHandler(ConsoleDebugger sender);

/// <summary>
/// The memory engine debugger
/// </summary>
public class ConsoleDebugger {
    public static readonly DataKey<ConsoleDebugger> DataKey = DataKey<ConsoleDebugger>.Create(nameof(ConsoleDebugger));

    private readonly BusyLock busyLocker;
    private ThreadEntry? activeThread;
    private bool refreshRegistersOnActiveThreadChange = true;
    private bool autoAddOrRemoveThreads = true;
    private bool isWindowVisible;
    private bool? isConsoleRunning;
    private string? consoleExecutionState;

    public BusyLock BusyLock => this.busyLocker;

    public ObservableList<RegisterEntry> RegisterEntries { get; }

    public ObservableList<ThreadEntry> ThreadEntries { get; }

    public ObservableList<FunctionCallEntry> FunctionCallEntries { get; }

    /// <summary>
    /// Gets or sets the thread currently selected in the thread entry list
    /// </summary>
    public ThreadEntry? ActiveThread {
        get => this.activeThread;
        set {
            if (value != null && !this.ThreadEntries.Contains(value))
                throw new InvalidOperationException("Attempt to select a thread that wasn't in our thread entries list");
            PropertyHelper.SetAndRaiseINE(ref this.activeThread, value, this, static (t, o, n) => t.ActiveThreadChanged?.Invoke(t, o, n));

            if (!this.ignoreActiveThreadChange) {
                this.rldaUpdateForThreadChanged.InvokeAsync();
                this.rldaUpdateCallFrame.InvokeAsync();
            }
        }
    }

    /// <summary>
    /// Gets or sets if the registers should be re-queried when the active thread changes
    /// </summary>
    public bool RefreshRegistersOnActiveThreadChange {
        get => this.refreshRegistersOnActiveThreadChange;
        set => PropertyHelper.SetAndRaiseINE(ref this.refreshRegistersOnActiveThreadChange, value, this, static t => t.RefreshRegistersOnActiveThreadChangeChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets if threads should be added to or removed from the threads list when we receive thread create/terminate events
    /// </summary>
    public bool AutoAddOrRemoveThreads {
        get => this.autoAddOrRemoveThreads;
        set => PropertyHelper.SetAndRaiseINE(ref this.autoAddOrRemoveThreads, value, this, static t => t.AutoAddOrRemoveThreadsChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets if the console is running. Obviously, this doesn't actually affect the console itself
    /// </summary>
    public bool? IsConsoleRunning {
        get => this.isConsoleRunning;
        set => PropertyHelper.SetAndRaiseINE(ref this.isConsoleRunning, value, this, static t => t.IsConsoleRunningChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets the readable execution state of the console.
    /// </summary>
    public string? ConsoleExecutionState {
        get => this.consoleExecutionState;
        set => PropertyHelper.SetAndRaiseINE(ref this.consoleExecutionState, value, this, static t => t.ConsoleExecutionStateChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets if the debugger view is currently visible
    /// </summary>
    public bool IsWindowVisible {
        get => this.isWindowVisible;
        set => PropertyHelper.SetAndRaiseINE(ref this.isWindowVisible, value, this, static t => t.IsWindowVisibleChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets the connection used to read and write data to the console.
    /// We do not use the engine's connection since we don't want to prevent scanning and more to work when we're refreshing data all the time
    /// </summary>
    public IConsoleConnection? Connection { get; private set; }

    public MemoryEngine Engine { get; }

    public event ConsoleDebuggerConnectionChangedEventHandler? ConnectionChanged;
    public event ConsoleDebuggerActiveThreadChangedEventHandler? ActiveThreadChanged;
    public event ConsoleDebuggerEventHandler? RefreshRegistersOnActiveThreadChangeChanged;
    public event ConsoleDebuggerEventHandler? AutoAddOrRemoveThreadsChanged;
    public event ConsoleDebuggerEventHandler? IsConsoleRunningChanged;
    public event ConsoleDebuggerEventHandler? ConsoleExecutionStateChanged;
    public event ConsoleDebuggerEventHandler? IsWindowVisibleChanged;

    private readonly RateLimitedDispatchAction rldaUpdateForThreadChanged;
    private readonly RateLimitedDispatchAction rldaUpdateCallFrame;
    private bool ignoreActiveThreadChange; // prevent RLDA getting fired
    private IDisposable? eventSubscription;

    public ConsoleDebugger(MemoryEngine engine) {
        this.busyLocker = new BusyLock();
        this.Engine = engine;
        this.ThreadEntries = new ObservableList<ThreadEntry>();
        this.ThreadEntries.CollectionChanged += (sender, e) => {
            if (this.activeThread != null && !this.ThreadEntries.Contains(this.activeThread)) {
                this.ActiveThread = null;
            }
        };

        this.FunctionCallEntries = new ObservableList<FunctionCallEntry>();

        this.RegisterEntries = new ObservableList<RegisterEntry>();

        this.rldaUpdateForThreadChanged = new RateLimitedDispatchAction(this.OnUpdateForActiveThreadChanged, TimeSpan.FromMilliseconds(100));
        this.rldaUpdateCallFrame = new RateLimitedDispatchAction(this.OnUpdateCallFrameForIsRunningChanged, TimeSpan.FromMilliseconds(500));
        this.IsConsoleRunningChanged += sender => this.rldaUpdateCallFrame.InvokeAsync();
    }

    private async Task OnUpdateCallFrameForIsRunningChanged() {
        if (!this.RefreshRegistersOnActiveThreadChange || !this.IsWindowVisible) {
            return;
        }

        if (this.Connection == null || !this.Connection.IsConnected) {
            return;
        }

        using IDisposable? token = await this.busyLocker.BeginBusyOperationAsync(500);
        if (token != null && !this.ignoreActiveThreadChange) {
            ThreadEntry? thread = Volatile.Read(ref this.activeThread);
            IConsoleConnection? connection = this.Connection;
            if (thread != null && connection != null && connection.IsConnected) {
                if (thread.IsSuspended || this.IsConsoleRunning == false) {
                    await this.UpdateCallFrame(connection, thread, null);
                }
                else {
                    await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => this.FunctionCallEntries.Clear(), DispatchPriority.Default, token: CancellationToken.None);
                }
            }
        }
    }

    public void UpdateLaterForSelectedThreadChanged() {
        this.rldaUpdateForThreadChanged.InvokeAsync();
    }

    private Task OnUpdateForActiveThreadChanged() {
        if (this.RefreshRegistersOnActiveThreadChange && this.IsWindowVisible) {
            return this.UpdateRegistersForActiveThread(CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task UpdateAllThreads(CancellationToken busyCancellationToken) {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(busyCancellationToken);
        using IDisposable? token = await this.busyLocker.BeginBusyOperationActivityAsync("", cancellationTokenSource: cts);
        if (token == null || cts.IsCancellationRequested || this.ignoreActiveThreadChange) {
            return;
        }

        IConsoleConnection? connection = this.Connection;
        if (connection != null && connection.IsConnected) {
            await this.UpdateAllThreadsImpl(connection, token);
        }
    }

    private async Task UpdateAllThreadsImpl(IConsoleConnection connection, IDisposable busyToken) {
        IHaveXboxDebugFeatures debug = (IHaveXboxDebugFeatures) connection;
        List<ThreadEntry> threads;
        try {
            List<XboxThread> threadList = await debug.GetThreadDump();
            threads = threadList.Select(x => new ThreadEntry(x.id) {
                ThreadName = x.readableName ?? "",
                BaseAddress = x.baseAddress,
                IsSuspended = x.suspendCount > 0,
                ProcessorNumber = x.currentProcessor
            }).ToList();
        }
        catch (Exception e) when (e is IOException || e is TimeoutException) {
            await IMessageDialogService.Instance.ShowMessage("Network error", e.Message);
            return;
        }
        catch (Exception e) {
            await IMessageDialogService.Instance.ShowMessage("Error", e.Message);
            return;
        }

        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
            uint? selectedThreadId = this.activeThread?.ThreadId;

            this.ignoreActiveThreadChange = true;
            this.ActiveThread = null;
            this.ThreadEntries.Clear();
            this.ThreadEntries.AddRange(threads);
            if (selectedThreadId is uint tId && threads.FirstOrDefault(x => x.ThreadId == tId) is ThreadEntry entry) {
                this.ActiveThread = entry;
            }

            this.ignoreActiveThreadChange = false;
        }, DispatchPriority.Default, token: CancellationToken.None);
    }

    public async Task<ThreadEntry?> UpdateThread(uint threadId, bool createIfDoesntExist = true) {
        if (this.Connection == null || !this.Connection.IsConnected)
            return null;

        using IDisposable? token = await this.busyLocker.BeginBusyOperationActivityAsync("Read Info on Newly Created Thread");
        if (token == null) {
            return null;
        }

        return await this.UpdateThread(token, threadId, createIfDoesntExist);
    }

    public async Task<ThreadEntry?> UpdateThread(IDisposable token, uint threadId, bool createIfDoesntExist = true) {
        this.busyLocker.ValidateToken(token);

        int idx = this.ThreadEntries.FindIndex(x => x.ThreadId == threadId);
        if (idx == -1 && !createIfDoesntExist) {
            return null;
        }

        IConsoleConnection? connection = this.Connection;
        if (connection == null || !connection.IsConnected) {
            return null;
        }

        XboxThread thread = await ((IHaveXboxDebugFeatures) connection).GetThreadInfo(threadId);
        if (idx == -1) {
            if (thread.id == 0) {
                return null;
            }

            this.ThreadEntries.Add(new ThreadEntry(thread.id) {
                ThreadName = thread.readableName ?? "",
                BaseAddress = thread.baseAddress,
                IsSuspended = thread.suspendCount > 0,
                ProcessorNumber = thread.currentProcessor
            });
            
            this.rldaUpdateCallFrame.InvokeAsync();
        }
        else {
            if (thread.id == 0) {
                this.ThreadEntries.RemoveAt(idx);
                return null;
            }

            ThreadEntry newThread = new ThreadEntry(thread.id) {
                ThreadName = thread.readableName ?? "",
                BaseAddress = thread.baseAddress,
                IsSuspended = thread.suspendCount > 0,
                ProcessorNumber = thread.currentProcessor
            };

            this.ignoreActiveThreadChange = true;
            this.ActiveThread = this.ThreadEntries[idx] = newThread;
            this.ignoreActiveThreadChange = false;
            this.rldaUpdateCallFrame.InvokeAsync();
            return newThread;
        }

        return null;
    }

    /// <summary>
    /// Updates the registers for the currently active thread. This has a 1-second timeout,
    /// so the cancellation token isn't required to be cancellable
    /// </summary>
    public async Task UpdateRegistersForActiveThread(CancellationToken busyCancellationToken) {
        if (this.Connection == null || !this.Connection.IsConnected)
            return;

        using IDisposable? token = await this.busyLocker.BeginBusyOperationAsync(500, busyCancellationToken);
        if (token != null && !this.ignoreActiveThreadChange) {
            await this.UpdateRegistersForActiveThread(token);
        }
    }

    public async Task UpdateRegistersForActiveThread(IDisposable token) {
        this.busyLocker.ValidateToken(token);

        ThreadEntry? thread = Volatile.Read(ref this.activeThread); /* just incase caller is not on AMT */
        if (thread == null || this.Connection == null || this.ignoreActiveThreadChange) {
            return;
        }

        IConsoleConnection? connection = this.Connection;
        if (connection != null && connection.IsConnected) {
            IHaveXboxDebugFeatures debug = (IHaveXboxDebugFeatures) connection;
            List<RegisterEntry>? registers;
            try {
                registers = await debug.GetRegisters(thread.ThreadId);
            }
            catch (Exception e) when (e is IOException || e is TimeoutException) {
                await IMessageDialogService.Instance.ShowMessage("Network error", e.Message);
                return;
            }
            catch (Exception e) {
                await IMessageDialogService.Instance.ShowMessage("Error", e.Message);
                return;
            }

            if (registers == null) {
                await this.UpdateAllThreadsImpl(connection, token);
            }
            else {
                await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                    this.RegisterEntries.Clear();
                    this.RegisterEntries.AddRange(registers);
                }, DispatchPriority.Default, token: CancellationToken.None);
            }

            this.rldaUpdateCallFrame.InvokeAsync();
        }
    }

    private async Task UpdateCallFrame(IConsoleConnection connection, ThreadEntry thread, List<RegisterEntry>? registers) {
        if (registers == null) {
            try {
                registers = await ((IHaveXboxDebugFeatures) connection).GetRegisters(thread.ThreadId);
            }
            catch (Exception e) when (e is IOException || e is TimeoutException) {
                await IMessageDialogService.Instance.ShowMessage("Network error", e.Message);
                return;
            }
            catch (Exception e) {
                await IMessageDialogService.Instance.ShowMessage("Error", e.Message);
                return;
            }

            if (registers == null) {
                return;
            }
        }

        RegisterEntry32? iar = registers.FirstOrDefault(x => x.Name.EqualsIgnoreCase("iar")) as RegisterEntry32;
        if (iar == null) {
            return;
        }

        RegisterEntry32? lr = registers.FirstOrDefault(x => x.Name.EqualsIgnoreCase("lr")) as RegisterEntry32;
        if (lr == null) {
            return;
        }

        FunctionCallEntry?[] functions;
        try {
            functions = await ((IHaveXboxDebugFeatures) connection).FindFunctions([iar.Value, lr.Value]);
        }
        catch (Exception e) when (e is IOException || e is TimeoutException) {
            await IMessageDialogService.Instance.ShowMessage("Network error", e.Message);
            return;
        }
        catch (Exception e) {
            await IMessageDialogService.Instance.ShowMessage("Error", e.Message);
            return;
        }

        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
            this.FunctionCallEntries.Clear();
            foreach (FunctionCallEntry? entry in functions) {
                this.FunctionCallEntries.Add(new FunctionCallEntry(entry?.ModuleName ?? "<unknown>", entry?.Address ?? 0, entry?.Size ?? 0));
            }
        }, DispatchPriority.Default, token: CancellationToken.None);
    }

    public void SetConnection(IDisposable busyToken, IConsoleConnection? newConnection) {
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();
        this.busyLocker.ValidateToken(busyToken);
        if (this.ignoreActiveThreadChange)
            throw new InvalidOperationException("Already changing connection... somehow?");

        // we don't necessarily need to access connection under lock since if we have
        // a valid busy token then nothing can modify it
        IConsoleConnection? oldConnection = this.Connection;
        if (ReferenceEquals(oldConnection, newConnection))
            throw new ArgumentException("Cannot set the connection to the same value");

        if (newConnection != null && !(newConnection is IHaveXboxDebugFeatures))
            throw new InvalidOperationException("Connection is not debuggable");

        this.ignoreActiveThreadChange = true;

        this.ThreadEntries.Clear();
        this.RegisterEntries.Clear();
        DisposableUtils.Dispose(ref this.eventSubscription);

        if (oldConnection != null)
            oldConnection.Closed -= this.OnConnectionClosed;
        if (newConnection != null)
            newConnection.Closed += this.OnConnectionClosed;

        // ConnectionChanged is invoked under the lock to enforce busy operation rules
        object theLock = this.busyLocker.CriticalLock;
        lock (theLock) {
            Debug.Assert(this.Connection == oldConnection);

            this.Connection = newConnection;
            this.ConnectionChanged?.Invoke(this, oldConnection, newConnection);
        }

        this.ignoreActiveThreadChange = false;
        this.IsConsoleRunning = null;
        this.ConsoleExecutionState = null;
        if (newConnection is IHaveSystemEvents events) {
            this.eventSubscription = events.SubscribeToEvents(this.OnConsoleEvent);
        }
    }

    public void CheckConnection() {
        IConsoleConnection? conn = this.Connection;
        if (conn != null && !conn.IsConnected) {
            using (IDisposable? token1 = this.BusyLock.BeginBusyOperation()) {
                if (token1 != null && this.TryDisconnectForLostConnection(token1)) {
                    return;
                }
            }

            ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                using IDisposable? token2 = this.BusyLock.BeginBusyOperation();
                if (token2 != null)
                    this.TryDisconnectForLostConnection(token2);
            }, DispatchPriority.Background);
        }
    }

    private bool TryDisconnectForLostConnection(IDisposable token) {
        IConsoleConnection? conn = this.Connection;
        if (conn == null)
            return true;
        if (conn.IsConnected)
            return false;

        this.SetConnection(token, null);
        return true;
    }

    private void OnConnectionClosed(IConsoleConnection sender) {
        if (sender == this.Connection) {
            this.CheckConnection();
        }
    }

    private void OnConsoleEvent(IConsoleConnection sender, ConsoleSystemEventArgs e) {
        if (e is XbdmEventArgsExecutionState stateChanged) {
            this.OnHandleStateChange(stateChanged);
        }

        if (this.autoAddOrRemoveThreads && e is XbdmEventArgsThreadLife threadEvent) {
            this.OnHandleThreadEvent(e, threadEvent);
        }

        // For some reason breakpoints never seem to hit, not even data breakpoints...
        // Maybe there's another debugger command required to activate breaking?
        if (e is XbdmEventArgsBreakpoint) {
        }

        if (e is XbdmEventArgsDataBreakpoint) {
        }
    }


    private void OnHandleThreadEvent(ConsoleSystemEventArgs e, XbdmEventArgsThreadLife threadEvent) {
        bool isCreated = e is XbdmEventArgsCreateThread;
        ApplicationPFX.Instance.Dispatcher.InvokeAsync(async () => {
            if (isCreated) {
                using IDisposable? token = await this.busyLocker.BeginBusyOperationActivityAsync("Read Info on Newly Created Thread");
                if (token == null)
                    return;

                IConsoleConnection? connection = this.Connection;
                if (connection != null && connection.IsConnected) {
                    XboxThread tdInfo = await ((IHaveXboxDebugFeatures) connection).GetThreadInfo(threadEvent.Thread);
                    if (tdInfo.id != 0) {
                        this.ThreadEntries.Add(new ThreadEntry(tdInfo.id) {
                            ThreadName = tdInfo.readableName ?? "",
                            BaseAddress = tdInfo.baseAddress,
                            IsSuspended = tdInfo.suspendCount > 0,
                            ProcessorNumber = tdInfo.currentProcessor
                        });
                    }
                }
            }
            else {
                ObservableList<ThreadEntry> list = this.ThreadEntries;
                for (int i = list.Count - 1; i >= 0; i--) {
                    if (list[i].ThreadId == threadEvent.Thread)
                        list.RemoveAt(i);
                }
            }
        }, DispatchPriority.Background);
    }

    private void OnHandleStateChange(XbdmEventArgsExecutionState stateChanged) {
        bool? newRunState;
        string? stateName;
        switch (stateChanged.ExecutionState) {
            case XbdmExecutionState.Pending:
                newRunState = null;
                stateName = "Pending";
                break;
            case XbdmExecutionState.Reboot:
                newRunState = null;
                stateName = "Rebooting";
                break;
            case XbdmExecutionState.Start:
                newRunState = true;
                stateName = "Running";
                break;
            case XbdmExecutionState.Stop:
                newRunState = false;
                stateName = "Stopped";
                break;
            case XbdmExecutionState.TitlePending:
                newRunState = null;
                stateName = "Title Pending";
                break;
            case XbdmExecutionState.TitleReboot:
                newRunState = null;
                stateName = "Title Rebooting";
                break;
            case XbdmExecutionState.Unknown:
                newRunState = null;
                stateName = null;
                break;
            default: throw new ArgumentOutOfRangeException();
        }

        ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
            this.ConsoleExecutionState = stateName;
            this.IsConsoleRunning = newRunState;
        }, DispatchPriority.Background);
    }
}