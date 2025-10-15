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
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.BaseFrontEnd.FileConnections;

public class ConnectionTypeBinaryFile : RegisteredConnectionType {
    public const string TheID = "memeng360.binaryfile-coreimpl"; // -coreimpl suffix added to core plugins, not that we need to but eh
    public static readonly RegisteredConnectionType Instance = new ConnectionTypeBinaryFile();

    public override string DisplayName => "Binary File";

    public override string? FooterText => "Semi-stable";

    public override string LongDescription => $"Treats a file as a console connection. Can only read/write data. Be extremely careful you don't allocate a 1 terabyte file or something!";

    public override Icon Icon => SimpleIcons.OpenFileXMLIcon;

    private ConnectionTypeBinaryFile() {
    }

    public override IEnumerable<IMenuEntry> GetRemoteContextOptions() {
        yield break;
    }

    public override UserConnectionInfo? CreateConnectionInfo() {
        return new OpenBinaryFileInfo();
    }

    public override async Task<IConsoleConnection?> OpenConnection(UserConnectionInfo? _info, IContextData additionalContext, CancellationTokenSource cancellation) {
        OpenBinaryFileInfo info = (OpenBinaryFileInfo) _info!;
        if (string.IsNullOrWhiteSpace(info.FilePath)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid input", "File path is empty", icon: MessageBoxIcons.ErrorIcon);
            return null;
        }

        if (!File.Exists(info.FilePath)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid file", "File does not exist", icon: MessageBoxIcons.ErrorIcon);
            return null;
        }

        FileStream? stream;

        try {
            stream = new FileStream(info.FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        }
        catch (Exception e) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Could not open file stream", e.Message, icon: MessageBoxIcons.ErrorIcon);
            return null;
        }

        return new BinaryFileConnection(stream, info.BaseAddress, info.AllowResizingFile, info.IsLittleEndian);
    }
}