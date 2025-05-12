// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemEngine360.
// 
// MemEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using Avalonia.Controls;
using MemEngine360.XboxInfo;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Services.UserInputs;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.BaseFrontEnd.MemRegions;

public partial class XboxMemoryRegionViewerUIControl : UserControl, IUserInputContent {
    private UserInputDialogView? myDialog;
    private MemoryRegionUserInputInfo? myInfo;
    public readonly IBinder<MemoryRegionUserInputInfo> selectedItemBinder = new AvaloniaPropertyToEventPropertyBinder<MemoryRegionUserInputInfo>(DataGrid.SelectedItemProperty, nameof(MemoryRegionUserInputInfo.SelectedRegionChanged), (b) => ((DataGrid) b.Control).SelectedItem = b.Model.SelectedRegion, (b) => b.Model.SelectedRegion = (MemoryRegionViewModel?) ((DataGrid) b.Control).SelectedItem);
    
    public XboxMemoryRegionViewerUIControl() {
        this.InitializeComponent();
    }

    private void PART_DataGridOnSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        this.myInfo?.RaiseHasErrorsChanged();
    }

    public void Connect(UserInputDialogView dialog, UserInputInfo info) {
        this.myDialog = dialog;
        this.myInfo = (MemoryRegionUserInputInfo) info;
        this.PART_DataGrid.ItemsSource = this.myInfo.MemoryRegions;
        this.PART_DataGrid.SelectionMode = DataGridSelectionMode.Single;
        this.selectedItemBinder.Attach(this.PART_DataGrid, this.myInfo);
    }

    public void Disconnect() {
        this.selectedItemBinder.Detach();
        this.PART_DataGrid.ItemsSource = null;
        this.myInfo = null;
        this.myDialog = null;
    }

    public bool FocusPrimaryInput() {
        if (this.myInfo!.MemoryRegions.Count > 0) {
            this.PART_DataGrid.SelectedIndex = 0;
            return true;
        }

        return false;
    }

    public void OnWindowOpened() {
        this.myDialog!.Window!.SizeToContent = SizeToContent.Manual;
        this.myDialog!.Window!.MinHeight = 400;
        this.myDialog!.Window!.MinWidth = 600;
        this.myDialog!.Window!.Height = 50;
        this.myDialog!.Window!.Width = 60;
        this.myDialog!.Window!.CanResize = true;
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