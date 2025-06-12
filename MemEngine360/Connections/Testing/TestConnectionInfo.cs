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

using PFXToolKitUI.Utils;

namespace MemEngine360.Connections.Testing;

public delegate void TestConnectionInfoModeChangedEventHandler(TestConnectionInfo sender);

public class TestConnectionInfo : UserConnectionInfo {
    private TestConnectionMode mode = TestConnectionMode.TimeoutError;

    public TestConnectionMode Mode {
        get => this.mode;
        set => PropertyHelper.SetAndRaiseINE(ref this.mode, value, this, static t => t.ModeChanged?.Invoke(t));
    }

    public event TestConnectionInfoModeChangedEventHandler? ModeChanged;

    public TestConnectionInfo(RegisteredConnectionType connectionType) : base(connectionType) {
    }

    protected override void OnShown() {
    }

    protected override void OnHidden() {
    }
}