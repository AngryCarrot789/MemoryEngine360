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

using MemEngine360.Connections;
using MemEngine360.Engine.Debugging;
using MemEngine360.Engine.Debugging.Commands;
using PFXToolKitUI.Avalonia.CommandUsages;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.BaseFrontEnd.Debugging;

public abstract class DebuggerCommandUsage : SimpleButtonCommandUsage {
    public ConsoleDebugger? Debugger { get; private set; }

    protected DebuggerCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnContextChanged() {
        base.OnContextChanged();
        ConsoleDebugger? oldDebugger = this.Debugger;
        ConsoleDebugger? newDebugger = null;
        if (this.GetContextData() is IContextData data) {
            ConsoleDebugger.DataKey.TryGetContext(data, out newDebugger);
        }

        if (oldDebugger != newDebugger) {
            this.Debugger = newDebugger;
            this.OnDebuggerChanged(oldDebugger, newDebugger);
        }
    }

    protected virtual void OnDebuggerChanged(ConsoleDebugger? oldDebugger, ConsoleDebugger? newDebugger) {
        this.UpdateCanExecuteLater();
    }
}

public abstract class DebuggerConnectionDependentCommandUsage : DebuggerCommandUsage {
    protected DebuggerConnectionDependentCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnDebuggerChanged(ConsoleDebugger? oldDebugger, ConsoleDebugger? newDebugger) {
        base.OnDebuggerChanged(oldDebugger, newDebugger);

        if (oldDebugger != null) {
            oldDebugger.ConnectionChanged -= this.OnConnectionChanged;
        }

        if (newDebugger != null) {
            newDebugger.ConnectionChanged += this.OnConnectionChanged;
        }
    }

    private void OnConnectionChanged(object? o, ValueChangedEventArgs<IConsoleConnection?> args) {
        this.UpdateCanExecuteLater();
    }
}

public abstract class DebuggerConsoleExecStateCommandUsage : DebuggerConnectionDependentCommandUsage {
    protected DebuggerConsoleExecStateCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnDebuggerChanged(ConsoleDebugger? oldDebugger, ConsoleDebugger? newDebugger) {
        base.OnDebuggerChanged(oldDebugger, newDebugger);

        if (oldDebugger != null) {
            oldDebugger.IsConsoleRunningChanged -= this.OnIsRunningChanged;
        }

        if (newDebugger != null) {
            newDebugger.IsConsoleRunningChanged += this.OnIsRunningChanged;
        }
    }

    private void OnIsRunningChanged(object? o, EventArgs args) {
        this.UpdateCanExecuteLater();
    }
}

public abstract class DebugThreadCommandUsage : DebuggerCommandUsage {
    protected DebugThreadCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnDebuggerChanged(ConsoleDebugger? oldDebugger, ConsoleDebugger? newDebugger) {
        base.OnDebuggerChanged(oldDebugger, newDebugger);

        if (oldDebugger != null)
            oldDebugger.ActiveThreadChanged -= this.OnActiveThreadChanged;
        if (newDebugger != null)
            newDebugger.ActiveThreadChanged += this.OnActiveThreadChanged;
    }

    private void OnActiveThreadChanged(object? o, ValueChangedEventArgs<ThreadEntry?> args) {
        this.UpdateCanExecuteLater();
    }
}

public class FreezeConsoleCommandCommandUsage() : DebuggerConsoleExecStateCommandUsage("commands.debugger.FreezeConsoleCommand");

public class UnfreezeConsoleCommandCommandUsage() : DebuggerConsoleExecStateCommandUsage("commands.debugger.UnfreezeConsoleCommand");

public class SuspendThreadCommandCommandUsage() : DebugThreadCommandUsage("commands.debugger.SuspendThreadCommand");

public class ResumeThreadCommandCommandUsage() : DebugThreadCommandUsage("commands.debugger.ResumeThreadCommand");

public class OpenDebuggerConnectionCommandUsage : DebuggerConnectionDependentCommandUsage {
    private CommandUsageSignal? mySignal;

    public OpenDebuggerConnectionCommandUsage() : base("commands.debugger.OpenDebuggerConnectionCommand") {
    }

    protected override void OnDebuggerChanged(ConsoleDebugger? oldDebugger, ConsoleDebugger? newDebugger) {
        base.OnDebuggerChanged(oldDebugger, newDebugger);
        if (this.mySignal != null) {
            this.mySignal.CanExecuteChanged -= this.SignalOnCanExecuteChanged;
            this.mySignal = null;
        }
        
        if (newDebugger != null) {
            this.mySignal = CommandUsageSignal.GetOrCreate(newDebugger.UserContext, OpenDebuggerConnectionCommand.CommandUsageSignalDataKey);
            this.mySignal.CanExecuteChanged += this.SignalOnCanExecuteChanged;
        }
    }

    private void SignalOnCanExecuteChanged(object? o, EventArgs eventArgs) {
        this.UpdateCanExecuteLater();
    }
}