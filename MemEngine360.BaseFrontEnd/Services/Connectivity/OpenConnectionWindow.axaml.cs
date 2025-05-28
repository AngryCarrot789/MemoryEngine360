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

using Avalonia.Input;
using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI.Avalonia.Services.Windowing;

namespace MemEngine360.BaseFrontEnd.Services.Connectivity;

/// <summary>
/// A window which contains an instance of <see cref="OpenConnectionView"/>
/// </summary>
public partial class OpenConnectionWindow : DesktopWindow, IOpenConnectionView {
    private TaskCompletionSource<IConsoleConnection?>? tcs;
    private IConsoleConnection? closedConnection;

    public MemoryEngine360 MemoryEngine360 {
        get => this.PART_ConnectToConsoleView.MemoryEngine360;
        internal set => this.PART_ConnectToConsoleView.MemoryEngine360 = value;
    }

    public string? TypeToFocusOnOpened {
        get => this.PART_ConnectToConsoleView.TypeToFocusOnOpened;
        internal set => this.PART_ConnectToConsoleView.TypeToFocusOnOpened = value;
    }

    public OpenConnectionWindow() {
        this.InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) {
            this.Close();
        }
    }

    protected override void OnOpenedCore() {
        base.OnOpenedCore();
        this.PART_ConnectToConsoleView.OnWindowOpened();
    }

    protected override void OnClosed(EventArgs e) {
        base.OnClosed(e);
        this.PART_ConnectToConsoleView.OnWindowClosed();
        this.closedConnection = this.PART_ConnectToConsoleView.CurrentConnection;
        this.tcs?.TrySetResult(this.closedConnection);
    }

    public Task<IConsoleConnection?> WaitForClose() {
        if (this.IsClosed) {
            return Task.FromResult(this.closedConnection);
        }
        
        this.tcs ??= new TaskCompletionSource<IConsoleConnection?>();
        return this.tcs.Task;
    }
}