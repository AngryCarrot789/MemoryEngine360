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
using Avalonia.Data;
using MemEngine360.BaseFrontEnd.Services.Connectivity;
using MemEngine360.Connections;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.FileConnections;

public partial class OpenDebuggingFileView : UserControl, IConsoleConnectivityControl {
    private readonly IBinder<OpenDebuggingFileInfo> filePathBinder = new TextBoxToDataParameterBinder<OpenDebuggingFileInfo, string?>(OpenDebuggingFileInfo.FilePathParameter, null, (t, s) => Task.FromResult<Optional<string?>>(s));
    private OpenDebuggingFileInfo? myInfo;
    private readonly AsyncRelayCommand openFileCommand;

    public OpenDebuggingFileView() {
        this.InitializeComponent();
        this.openFileCommand = new AsyncRelayCommand(async () => {
            if (this.myInfo != null) {
                string? path = await IFilePickDialogService.Instance.OpenFile("Open a file as the debug file", [Filters.All]);
                if (path != null) {
                    this.myInfo.FilePath = path;
                }
            }
        });

        this.PART_BrowseFileButton.Command = this.openFileCommand;
    }

    public void OnConnected(OpenConnectionView dialog, UserConnectionInfo info) {
        this.filePathBinder.Attach(this.PART_FilePathTextBox, this.myInfo = (OpenDebuggingFileInfo) info);
    }

    public void OnDisconnected() {
        this.filePathBinder.Detach();
        this.myInfo = null;
    }
    
}