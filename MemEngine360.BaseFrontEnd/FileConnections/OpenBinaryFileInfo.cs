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
using PFXToolKitUI.Utils;

namespace MemEngine360.BaseFrontEnd.FileConnections;

public delegate void OpenDebuggingFileInfoEventHandler(OpenBinaryFileInfo sender);

public class OpenBinaryFileInfo : UserConnectionInfo {
    private string? filePath;
    private uint baseAddress;
    private bool allowResizingFile;

    public string? FilePath {
        get => this.filePath;
        set => PropertyHelper.SetAndRaiseINE(ref this.filePath, value, this, static t => t.FilePathChanged?.Invoke(t));
    }

    public uint BaseAddress {
        get => this.baseAddress;
        set => PropertyHelper.SetAndRaiseINE(ref this.baseAddress, value, this, static t => t.BaseAddressChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets if resizing the file is allowed by the connection
    /// </summary>
    public bool AllowResizingFile {
        get => this.allowResizingFile;
        set => PropertyHelper.SetAndRaiseINE(ref this.allowResizingFile, value, this, static t => t.AllowResizingFileChanged?.Invoke(t));
    }

    public event OpenDebuggingFileInfoEventHandler? FilePathChanged;
    public event OpenDebuggingFileInfoEventHandler? BaseAddressChanged;
    public event OpenDebuggingFileInfoEventHandler? AllowResizingFileChanged;

    public OpenBinaryFileInfo() : base(ConnectionTypeBinaryFile.Instance) {
    }

    protected override void OnShown() {
    }

    protected override void OnHidden() {
    }
}