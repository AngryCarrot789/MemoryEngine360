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

public partial class XboxModuleViewerUIControl : UserControl, IUserInputContent {
    private UserInputDialogView? myDialog;
    private ModuleUserInputInfo? myInfo;
    public readonly IBinder<ModuleUserInputInfo> selectedItemBinder = new AvaloniaPropertyToEventPropertyBinder<ModuleUserInputInfo>(ListBox.SelectedItemProperty, nameof(ModuleUserInputInfo.SelectedRegionChanged), (b) => ((ListBox) b.Control).SelectedItem = b.Model.SelectedRegion, (b) => b.Model.SelectedRegion = (string?) ((ListBox) b.Control).SelectedItem);

    public XboxModuleViewerUIControl() {
        this.InitializeComponent();
    }

    public void Connect(UserInputDialogView dialog, UserInputInfo info) {
        this.myDialog = dialog;
        this.myInfo = (ModuleUserInputInfo) info;
        this.PART_ListBox.ItemsSource = this.myInfo.MemoryRegions;
        this.PART_ListBox.SelectionMode = SelectionMode.Single;
        this.selectedItemBinder.Attach(this.PART_ListBox, this.myInfo);
    }

    public void Disconnect() {
        this.selectedItemBinder.Detach();
        this.PART_ListBox.ItemsSource = null;
        this.myInfo = null;
        this.myDialog = null;
    }

    public bool FocusPrimaryInput() {
        if (this.myInfo!.MemoryRegions.Count > 0) {
            this.PART_ListBox.SelectedIndex = 0;
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
    }

    public void OnWindowClosed() {
    }
}