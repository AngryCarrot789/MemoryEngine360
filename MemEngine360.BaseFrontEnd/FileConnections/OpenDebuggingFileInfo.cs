﻿// 
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
using PFXToolKitUI.DataTransfer;
using PFXToolKitUI.Utils.Accessing;

namespace MemEngine360.BaseFrontEnd.FileConnections;

public class OpenDebuggingFileInfo : UserConnectionInfo {
    public static readonly DataParameterString FilePathParameter = DataParameter.Register(new DataParameterString(typeof(OpenDebuggingFileInfo), nameof(FilePath), null, ValueAccessors.Reflective<string?>(typeof(OpenDebuggingFileInfo), nameof(filePath))));

    private string? filePath;

    public string? FilePath {
        get => this.filePath;
        set => DataParameter.SetValueHelper(this, FilePathParameter, ref this.filePath, value);
    }

    public OpenDebuggingFileInfo() : base(ConnectionTypeDebugFile.Instance) {
        this.filePath = FilePathParameter.GetDefaultValue(this);
    }

    protected override void OnShown() {
    }

    protected override void OnHidden() {
    }
}