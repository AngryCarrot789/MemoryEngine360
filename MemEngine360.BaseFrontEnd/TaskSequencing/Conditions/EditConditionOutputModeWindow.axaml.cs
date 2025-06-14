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

using Avalonia.Interactivity;
using MemEngine360.Sequencing;
using PFXToolKitUI.Avalonia.Bindings.Enums;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.Utils;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Conditions;

public delegate void EditConditionOutputModeWindowEventHandler(EditConditionOutputModeWindow sender);

public partial class EditConditionOutputModeWindow : DesktopWindow {
    private readonly EventPropertyEnumBinder<ConditionOutputMode> binder = new EventPropertyEnumBinder<ConditionOutputMode>(typeof(EditConditionOutputModeWindow), nameof(TriggerModeChanged), (b) => ((EditConditionOutputModeWindow) b).OutputMode, (b, v) => ((EditConditionOutputModeWindow) b).OutputMode = v);
    private ConditionOutputMode outputMode;

    public ConditionOutputMode OutputMode {
        get => this.outputMode;
        set => PropertyHelper.SetAndRaiseINE(ref this.outputMode, value, this, static t => t.TriggerModeChanged?.Invoke(t));
    }

    public event EditConditionOutputModeWindowEventHandler? TriggerModeChanged;

    public EditConditionOutputModeWindow() {
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

        this.PART_ConfirmButton.Click += this.OnConfirmButtonClicked;
        this.PART_CancelButton.Click += this.OnCancelButtonClicked;
    }

    public EditConditionOutputModeWindow(ConditionOutputMode initialOutputMode) : this() {
        this.OutputMode = initialOutputMode;
    }

    private void OnConfirmButtonClicked(object? sender, RoutedEventArgs e) => this.Close(BoolBox.NullableTrue);

    private void OnCancelButtonClicked(object? sender, RoutedEventArgs e) => this.Close(BoolBox.NullableFalse);
}