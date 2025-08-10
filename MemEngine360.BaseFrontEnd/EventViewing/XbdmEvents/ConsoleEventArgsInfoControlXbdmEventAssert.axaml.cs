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
using MemEngine360.Connections.Features;
using MemEngine360.Engine.Events;
using MemEngine360.Engine.Events.XbdmEvents;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.EventViewing.XbdmEvents;

public partial class ConsoleEventArgsInfoControlXbdmEventAssert : UserControl, IConsoleEventArgsInfoControl {
    private IConnectionLockPair? myConnection;
    private XbdmEventArgsAssert? args;

    public ConsoleEventArgsInfoControlXbdmEventAssert() {
        this.InitializeComponent();
        this.PART_ReadThreadInfo.Command = new AsyncRelayCommand(async () => {
            if (this.myConnection?.Connection != null && !this.myConnection!.BusyLock.IsBusy) {
                await this.myConnection!.BeginBusyOperationActivityAsync(async (tok, con) => {
                    XbdmEventArgsAssert? e = this.args;
                    if (e == null || !con.TryGetFeature(out IFeatureXboxThreads? threadFeature))
                        return;
                    
                    XboxThread tdInfo = await threadFeature.GetThreadInfo(e.Thread);
                    if (tdInfo.readableName == null && tdInfo.nameAddress != 0 && tdInfo.nameLength != 0 && tdInfo.nameLength < int.MaxValue) {
                        tdInfo.readableName = await con.ReadStringASCII(tdInfo.nameAddress, (int) tdInfo.nameLength);
                    }
                    
                    this.PART_Thread.Text = string.IsNullOrWhiteSpace(tdInfo.readableName) 
                        ? e.Thread.ToString("X8")
                        : e.Thread.ToString("X8") + Environment.NewLine + tdInfo.readableName;
                }, "Read thread info");
            }
        });
    }

    public void Connect(IConnectionLockPair _connection, ConsoleSystemEventArgs _args) {
        this.myConnection = _connection;
        this.args = (XbdmEventArgsAssert) _args; 
        this.PART_Thread.Text = this.args.Thread.ToString("X8");
        this.PART_String.Text = this.args.String;
        this.PART_IsPrompt.IsChecked = this.args.IsPrompt;
    }

    public void Disconnect() {
        this.myConnection = null;
        this.args = null;
    }
}