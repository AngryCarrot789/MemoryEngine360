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
using MemEngine360.Connections.Traits;
using MemEngine360.Engine.Events;
using MemEngine360.Engine.Events.XbdmEvents;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.EventViewing.XbdmEvents;

public partial class ConsoleEventArgsInfoControlXbdmEventDebugString : UserControl, IConsoleEventArgsInfoControl {
    private IConnectionLockPair? myConnection;
    private XbdmEventArgsDebugString? args;

    public ConsoleEventArgsInfoControlXbdmEventDebugString() {
        this.InitializeComponent();
        this.PART_ReadThreadInfo.Command = new AsyncRelayCommand(async () => {
            if (this.myConnection?.Connection != null && !this.myConnection!.BusyLock.IsBusy) {
                await this.myConnection!.BeginBusyOperationActivityAsync(async (tok, con) => {
                    XbdmEventArgsDebugString? e = this.args;
                    if (e == null || !(con is IHaveXboxThreadInfo xbdm)) 
                        return;
                    
                    XboxThread tdInfo = await xbdm.GetThreadInfo(e.Thread);
                    if (tdInfo.nameAddress != 0 && tdInfo.nameLength != 0 && tdInfo.nameLength < int.MaxValue) {
                        tdInfo.readableName = await con.ReadStringASCII(tdInfo.nameAddress, (int) tdInfo.nameLength);
                    }
                    
                    this.PART_Thread.Text = string.IsNullOrWhiteSpace(tdInfo.readableName) 
                        ? $"{e.Thread:X8} (no thread name available)" 
                        : $"{e.Thread:X8} ({tdInfo.readableName})";
                }, "Read thread info");
            }
        });
    }

    public void Connect(IConnectionLockPair _connection, ConsoleSystemEventArgs _args) {
        this.myConnection = _connection;
        this.args = (XbdmEventArgsDebugString) _args; 
        this.PART_Thread.Text = this.args.Thread.ToString("X8");
        this.PART_String.Text = this.args.DebugString;
        this.PART_IsThreadStop.IsChecked = this.args.IsThreadStop;
    }

    public void Disconnect() {
        this.myConnection = null;
        this.args = null;
    }
}