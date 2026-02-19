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
using MemEngine360.Ps3Base;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.PS3.ProcessSelector;

[SupportedOSPlatform("windows")]
public sealed class ProcessSelectorUserInputInfo : UserInputInfo {
    public static readonly DataKey<ProcessSelectorUserInputInfo> DataKey = DataKeys.Create<ProcessSelectorUserInputInfo>(nameof(ProcessSelectorUserInputInfo));

    private Ps3Process? selectedProcess;

    public Ps3Process? SelectedProcess {
        get => this.selectedProcess;
        set => PropertyHelper.SetAndRaiseINE(ref this.selectedProcess, value, this, static t => {
            t.SelectedProcessChanged?.Invoke(t, EventArgs.Empty);
            t.RaiseHasErrorsChanged();
        });
    }

    public IPs3ConsoleConnection? Connection {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.ConnectionChanged);
    }

    public ObservableList<Ps3Process> Processes { get; } = new ObservableList<Ps3Process>();

    public event EventHandler? SelectedProcessChanged;
    public event EventHandler? ConnectionChanged;

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