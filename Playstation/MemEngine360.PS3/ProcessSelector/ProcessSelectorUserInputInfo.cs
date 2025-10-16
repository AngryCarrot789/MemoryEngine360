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

using System.Runtime.Versioning;
using MemEngine360.PS3.CC;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.PS3.ProcessSelector;

public delegate void ProcessSelectorUserInputInfoEventHandler(ProcessSelectorUserInputInfo sender);

public delegate void ProcessSelectorUserInputInfoConnectionChangedEventHandler(ProcessSelectorUserInputInfo sender, ConsoleConnectionCCAPI? oldConnection, ConsoleConnectionCCAPI? newConnection);

[SupportedOSPlatform("windows")]
public sealed class ProcessSelectorUserInputInfo : UserInputInfo {
    public static readonly DataKey<ProcessSelectorUserInputInfo> DataKey = DataKeys.Create<ProcessSelectorUserInputInfo>(nameof(ProcessSelectorUserInputInfo));

    private Ps3Process? selectedProcess;
    private ConsoleConnectionCCAPI? connection;

    public Ps3Process? SelectedProcess {
        get => this.selectedProcess;
        set => PropertyHelper.SetAndRaiseINE(ref this.selectedProcess, value, this, static t => {
            t.SelectedProcessChanged?.Invoke(t);
            t.RaiseHasErrorsChanged();
        });
    }

    public ConsoleConnectionCCAPI? Connection {
        get => this.connection;
        set => PropertyHelper.SetAndRaiseINE(ref this.connection, value, this, static (t, o, n) => t.ConnectionChanged?.Invoke(t, o, n));
    }

    public event ProcessSelectorUserInputInfoConnectionChangedEventHandler? ConnectionChanged;

    public ObservableList<Ps3Process> Processes { get; } = new ObservableList<Ps3Process>();

    public event ProcessSelectorUserInputInfoEventHandler? SelectedProcessChanged;

    public ProcessSelectorUserInputInfo() {
        this.Processes.ItemsRemoved += (list, index, items) => {
            if (this.selectedProcess != null && items.Contains(this.selectedProcess)) {
                this.SelectedProcess = null;
            }
        };

        this.Processes.ItemReplaced += (list, index, oldItem, newItem) => {
            if (this.SelectedProcess == newItem) {
                this.SelectedProcess = null;
            }
        };
    }

    public async Task RefreshProcesses() {
        this.SelectedProcess = null;
        this.Processes.Clear();

        if (this.Connection == null) {
            await IMessageDialogService.Instance.ShowMessage("Not connect", "Not connected to a console");
            return;
        }
        
        // todo
    }

    public override bool HasErrors() {
        return this.selectedProcess == null;
    }

    public override void UpdateAllErrors() {
    }
}