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

using System.Globalization;
using Avalonia.Controls;
using MemEngine360.BaseFrontEnd.Services.Connectivity;
using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.TextBoxes;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.FileConnections;

public partial class OpenBinaryFileView : UserControl, IConsoleConnectivityControl {
    private readonly IBinder<OpenBinaryFileInfo> filePathBinder = new TextBoxToEventPropertyBinder<OpenBinaryFileInfo>(nameof(OpenBinaryFileInfo.FilePathChanged), b => b.Model.FilePath ?? "", (binder, s) => {
        binder.Model.FilePath = s;
        return Task.FromResult(true);
    });

    private readonly IBinder<OpenBinaryFileInfo> baseAddressBinder = new TextBoxToEventPropertyBinder<OpenBinaryFileInfo>(nameof(OpenBinaryFileInfo.BaseAddressChanged), b => ((ulong) b.Model.BaseAddress).ToString("X8"), async (binder, s) => {
        if (!AddressParsing.TryParse32(s, out uint value, out string? error)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", error, defaultButton: MessageBoxResult.OK);
            return false;
        }

        binder.Model.BaseAddress = value;
        return true;
    });

    private readonly IBinder<OpenBinaryFileInfo> canResizeBinder = new AvaloniaPropertyToEventPropertyBinder<OpenBinaryFileInfo>(nameof(OpenBinaryFileInfo.AllowResizingFileChanged), b => ((CheckBox) b.Control).IsChecked = b.Model.AllowResizingFile, b => b.Model.AllowResizingFile = ((CheckBox) b.Control).IsChecked == true);
    private readonly IBinder<OpenBinaryFileInfo> isLittleEndianBinder = new AvaloniaPropertyToEventPropertyBinder<OpenBinaryFileInfo>(nameof(OpenBinaryFileInfo.IsLittleEndianChanged), b => ((CheckBox) b.Control).IsChecked = b.Model.IsLittleEndian, b => b.Model.IsLittleEndian = ((CheckBox) b.Control).IsChecked == true);

    private OpenBinaryFileInfo? myInfo;

    public OpenBinaryFileView() {
        this.InitializeComponent();
        this.PART_BrowseFileButton.Command = new AsyncRelayCommand(async () => {
            if (this.myInfo != null) {
                string? path = await IFilePickDialogService.Instance.OpenFile("Open a file as a connection", [Filters.All]);
                if (path != null) {
                    this.myInfo.FilePath = path;
                }
            }
        });
    }

    public void OnConnected(OpenConnectionView dialog, UserConnectionInfo info) {
        this.filePathBinder.Attach(this.PART_FilePathTextBox, this.myInfo = (OpenBinaryFileInfo) info);
        this.baseAddressBinder.Attach(this.PART_BaseAddress, this.myInfo);
        this.canResizeBinder.Attach(this.PART_CanResizeFile, this.myInfo);
        this.isLittleEndianBinder.Attach(this.PART_IsLittleEndian, this.myInfo);
    }

    public void OnDisconnected() {
        this.filePathBinder.Detach();
        this.baseAddressBinder.Detach();
        this.canResizeBinder.Detach();
        this.isLittleEndianBinder.Detach();
        this.myInfo = null;
    }
}