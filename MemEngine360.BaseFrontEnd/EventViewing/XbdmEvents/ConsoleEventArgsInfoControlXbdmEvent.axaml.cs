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

using Avalonia.Controls;
using MemEngine360.Connections;
using MemEngine360.Engine.Events;
using MemEngine360.Engine.Events.XbdmEvents;

namespace MemEngine360.BaseFrontEnd.EventViewing.XbdmEvents;

public partial class ConsoleEventArgsInfoControlXbdmEvent : UserControl, IConsoleEventArgsInfoControl {
    public ConsoleEventArgsInfoControlXbdmEvent() {
        this.InitializeComponent();
    }

    public void Connect(IConnectionLockPair connection, ConsoleSystemEventArgs args) {
        this.PART_RawText.Text = ((XbdmEventArgs) args).RawMessage;
    }

    public void Disconnect() {
    }
}