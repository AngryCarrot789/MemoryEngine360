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
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.BaseFrontEnd.FileConnections;

public class OpenBinaryFileInfo : UserConnectionInfo {
    public string? FilePath {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.FilePathChanged);
    }

    public uint BaseAddress {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.BaseAddressChanged);
    }

    /// <summary>
    /// Gets or sets if resizing the file is allowed by the connection
    /// </summary>
    public bool AllowResizingFile {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.AllowResizingFileChanged);
    }

    /// <summary>
    /// Gets or sets if the data file should be treated as little endian. Default is false
    /// </summary>
    public bool IsLittleEndian {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.IsLittleEndianChanged);
    }

    public event EventHandler? FilePathChanged;
    public event EventHandler? BaseAddressChanged;
    public event EventHandler? AllowResizingFileChanged;
    public event EventHandler? IsLittleEndianChanged;

    public OpenBinaryFileInfo() : base(ConnectionTypeBinaryFile.Instance) {
    }

    protected override void OnShown() {
    }

    protected override void OnHidden() {
    }
}