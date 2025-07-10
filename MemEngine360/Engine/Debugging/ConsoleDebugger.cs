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
    private bool refreshRegistersOnThreadChange = true;
    private bool isWindowVisible;
    private bool? isConsoleRunning;

    public BusyLock BusyLock => this.busyLocker;

    public ObservableList<RegisterEntry> RegisterEntries { get; }

    public ObservableList<ThreadEntry> ThreadEntries { get; }

    /// <summary>
    /// Gets or sets the thread currently selected in the thread entry list
    /// </summary>
    public ThreadEntry? ActiveThread {
        get => this.activeThread;
        set {
            if (value != null && !this.ThreadEntries.Contains(value))
                throw new InvalidOperationException("Attempt to select a thread that wasn't in our thread entries list");
            PropertyHelper.SetAndRaiseINE(ref this.activeThread, value, this, static (t, o, n) => t.ActiveThreadChanged?.Invoke(t, o, n));

            if (!this.ignoreActiveThreadChange)
                this.rldaUpdateForThreadChanged.InvokeAsync();
        }
    }

    public bool RefreshRegistersOnThreadChange {
        get => this.refreshRegistersOnThreadChange;
        set => PropertyHelper.SetAndRaiseINE(ref this.refreshRegistersOnThreadChange, value, this, static t => t.RefreshRegistersOnThreadChangeChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets if the console is running. Obviously, this doesn't actually affect the console itself
    /// </summary>
    public bool? IsConsoleRunning {
        get => this.isConsoleRunning;
        set => PropertyHelper.SetAndRaiseINE(ref this.isConsoleRunning, value, this, static t => t.IsConsoleRunningChanged?.Invoke(t));
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
    public event ConsoleDebuggerEventHandler? RefreshRegistersOnThreadChangeChanged;
    public event ConsoleDebuggerEventHandler? IsConsoleRunningChanged;
    public event ConsoleDebuggerEventHandler? IsWindowVisibleChanged;

    private readonly RateLimitedDispatchAction rldaUpdateForThreadChanged;
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

        this.RegisterEntries = new ObservableList<RegisterEntry>();

        this.rldaUpdateForThreadChanged = new RateLimitedDispatchAction(this.OnUpdateForActiveThreadChanged, TimeSpan.FromMilliseconds(100));
    }

    public void UpdateLaterForSelectedThreadChanged() {
        this.rldaUpdateForThreadChanged.InvokeAsync();
    }

    private Task OnUpdateForActiveThreadChanged() {
        if (this.RefreshRegistersOnThreadChange && this.IsWindowVisible) {
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

    /// <summary>
    /// Updates the registers for the currently active thread. This has a 1-second timeout,
    /// so the cancellation token isn't required to be cancellable
    /// </summary>
    public async Task UpdateRegistersForActiveThread(CancellationToken busyCancellationToken) {
        ThreadEntry? thread = Volatile.Read(ref this.activeThread); /* just incase caller is not on AMT */
        if (thread == null || this.Connection == null || this.ignoreActiveThreadChange) {
            return;
        }

        using IDisposable? token = await this.busyLocker.BeginBusyOperationAsync(1000, busyCancellationToken);
        if (token == null || this.ignoreActiveThreadChange) {
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
        }
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

        // ConnectionChanged is invoked under the lock to enforce busy operation rules
        object theLock = this.busyLocker.CriticalLock;
        lock (theLock) {
            Debug.Assert(this.Connection == oldConnection);

            this.Connection = newConnection;
            this.ConnectionChanged?.Invoke(this, oldConnection, newConnection);
        }

        this.ignoreActiveThreadChange = false;
        this.IsConsoleRunning = null;
        if (newConnection is IHaveSystemEvents events) {
            this.eventSubscription = events.SubscribeToEvents(this.OnConsoleEvent);
        }
    }

    private void OnConsoleEvent(IConsoleConnection sender, ConsoleSystemEventArgs e) {
        if (e is XbdmEventArgsExecutionState stateChanged) {
            bool? newRunState;
            switch (stateChanged.ExecutionState) {
                case XbdmExecutionState.Pending:
                case XbdmExecutionState.Reboot: newRunState = null; break;
                case XbdmExecutionState.Start: newRunState = true; break;
                case XbdmExecutionState.Stop:  newRunState = false; break;
                case XbdmExecutionState.TitlePending:
                case XbdmExecutionState.TitleReboot:
                case XbdmExecutionState.Unknown: newRunState = null; break;
                default: throw new ArgumentOutOfRangeException();
            }

            ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                this.IsConsoleRunning = newRunState;
            }, DispatchPriority.Background);
        }
    }
}