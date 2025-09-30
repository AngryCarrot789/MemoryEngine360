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

using Avalonia.Controls;
using MemEngine360.Sequencing;
using PFXToolKitUI.Avalonia.Bindings.Enums;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Conditions;

public delegate void EditConditionOutputModeWindowEventHandler(EditConditionOutputModeView sender);

public partial class EditConditionOutputModeView : UserControl {
    private readonly EventPropertyEnumBinder<ConditionOutputMode> binder = new EventPropertyEnumBinder<ConditionOutputMode>(typeof(EditConditionOutputModeView), nameof(TriggerModeChanged), (b) => ((EditConditionOutputModeView) b).OutputMode, (b, v) => ((EditConditionOutputModeView) b).OutputMode = v);
    private ConditionOutputMode outputMode;

    public ConditionOutputMode OutputMode {
        get => this.outputMode;
        set => PropertyHelper.SetAndRaiseINE(ref this.outputMode, value, this, static t => t.TriggerModeChanged?.Invoke(t));
    }

    public IWindowBase? Window { get; set; }

    public event EditConditionOutputModeWindowEventHandler? TriggerModeChanged;

    public EditConditionOutputModeView() {
        this.InitializeComponent();
        this.binder.Assign(this.PART_WhileMet, ConditionOutputMode.WhileMet);
        this.binder.Assign(this.PART_WhileNotMet, ConditionOutputMode.WhileNotMet);
        this.binder.Assign(this.PART_ChangeToMet, ConditionOutputMode.ChangeToMet);
        this.binder.Assign(this.PART_ChangeToNotMet, ConditionOutputMode.ChangeToNotMet);
        this.binder.Assign(this.PART_WhileMetOnce, ConditionOutputMode.WhileMetOnce);
        this.binder.Assign(this.PART_WhileNotMetOnce, ConditionOutputMode.WhileNotMetOnce);
        this.binder.Assign(this.PART_ChangeToMetOnce, ConditionOutputMode.ChangeToMetOnce);
        this.binder.Assign(this.PART_ChangeToNotMetOnce, ConditionOutputMode.ChangeToNotMetOnce);
        this.binder.Attach(this);
        this.PART_ConfirmButton.Command = new AsyncRelayCommand(() => this.Window?.RequestCloseAsync(BoolBox.NullableTrue) ?? Task.CompletedTask);
        this.PART_CancelButton.Command = new AsyncRelayCommand(() => this.Window?.RequestCloseAsync(BoolBox.NullableFalse) ?? Task.CompletedTask);
    }

    public EditConditionOutputModeView(ConditionOutputMode initialOutputMode) : this() {
        this.OutputMode = initialOutputMode;
    }
}