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
using MemEngine360.Connections;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.ToolTips;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.BaseFrontEnd.Services.Connectivity;

public partial class ConsoleTypeToolTip : UserControl, IToolTipControl {
    private readonly IBinder<RegisteredConnectionType> connectionTypeBinder = new ManualBinder<RegisteredConnectionType>(b => {
        ConsoleTypeToolTip tip = (ConsoleTypeToolTip) b.Control;
        tip.PART_Header.Text = b.Model.DisplayName;

        string? f = b.Model.FooterText;
        tip.PART_Id.Text = b.Model.RegisteredId + (!string.IsNullOrWhiteSpace(f) ? $" ({f})" : "");
        tip.PART_Description.Text = b.Model.LongDescription;
    });

    public ConsoleTypeToolTip() {
        this.InitializeComponent();
    }

    public void OnOpened(Control owner, IContextData data) {
        this.connectionTypeBinder.Attach(this, ((ConsoleTypeListBoxItem) owner).RegisteredConsoleType);
    }

    public void OnClosed(Control owner) {
        this.connectionTypeBinder.Detach();
    }
}