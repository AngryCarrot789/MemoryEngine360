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
using MemEngine360.XboxInfo;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Avalonia.Services.UserInputs;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.BaseFrontEnd.MemRegions;

public partial class XboxMemoryRegionViewerUIControl : UserControl, IUserInputContent {
    private UserInputDialogView? myDialog;
    public readonly IBinder<MemoryRegionUserInputInfo> selectedItemBinder = new AvaloniaPropertyToEventPropertyBinder<MemoryRegionUserInputInfo>(DataGrid.SelectedItemProperty, nameof(MemoryRegionUserInputInfo.SelectedRegionChanged), (b) => ((DataGrid) b.Control).SelectedItem = b.Model.SelectedRegion, (b) => b.Model.SelectedRegion = (MemoryRegionViewModel?) ((DataGrid) b.Control).SelectedItem);
    
    public MemoryRegionUserInputInfo? Info { get; private set; }

    public XboxMemoryRegionViewerUIControl() {
        this.InitializeComponent();
    }

    public void Connect(UserInputDialogView dialog, UserInputInfo info) {
        this.myDialog = dialog;
        this.Info = (MemoryRegionUserInputInfo) info;
        this.PART_DataGrid.ItemsSource = this.Info.MemoryRegions;
        this.PART_DataGrid.SelectionMode = DataGridSelectionMode.Single;
        this.selectedItemBinder.Attach(this.PART_DataGrid, this.Info);
    }

    public void Disconnect() {
        this.selectedItemBinder.Detach();
        this.PART_DataGrid.ItemsSource = null;
        this.Info = null;
        this.myDialog = null;
    }

    public bool FocusPrimaryInput() {
        if (this.Info!.MemoryRegions.Count > 0) {
            this.PART_DataGrid.SelectedIndex = 0;
            return true;
        }

        return false;
    }

    public void OnWindowOpening() {
        if (this.myDialog!.OwnerWindow is IDesktopWindow window) {
            WindowSizingInfo sizing = window.SizingInfo;
            sizing.SizeToContent = SizeToContent.Manual;
            sizing.CanResize = true;
            sizing.MinHeight = 400;
            sizing.MinWidth = 800;
            sizing.Height = 400;
            sizing.Width = 800;
        }
    }

    public void OnWindowOpened() {
        // TODO: fix vertical scroll bar incorrect estimate of the effective height of the data grid
        // ApplicationPFX.Instance.Dispatcher.InvokeAsync(async () => {
        //     await Task.Delay(250);
        //     
        //     FieldInfo? fieldInfo = typeof(DataGrid).GetField("_hScrollBar", BindingFlags.Instance | BindingFlags.NonPublic);
        //     if (fieldInfo != null) {
        //         ScrollBar viewer = (ScrollBar) fieldInfo.GetValue(this.PART_DataGrid)!;
        //         viewer.InvalidateMeasure();
        //         viewer.InvalidateArrange();
        //         viewer.UpdateLayout();
        //     }
        //     
        //     MethodInfo? info = typeof(DataGrid).GetMethod("ComputeScrollBarsLayout", BindingFlags.Instance | BindingFlags.NonPublic);
        //     if (info != null) {
        //         info.Invoke(this.PART_DataGrid, []);
        //     }
        // }, DispatchPriority.Background);
    }

    public void OnWindowClosed() {
    }
}