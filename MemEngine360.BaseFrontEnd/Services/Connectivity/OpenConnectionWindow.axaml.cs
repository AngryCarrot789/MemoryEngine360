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

using System.Diagnostics;
using Avalonia.Input;
using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Services.Windowing;

namespace MemEngine360.BaseFrontEnd.Services.Connectivity;

/// <summary>
/// A window which contains an instance of <see cref="OpenConnectionView"/>
/// </summary>
public partial class OpenConnectionWindow : DesktopWindow, IOpenConnectionView {
    private TaskCompletionSource<IConsoleConnection?>? tcs;
    private IConsoleConnection? closedConnection;

    public MemoryEngine? MemoryEngine {
        get => this.PART_ConnectToConsoleView.MemoryEngine;
        internal set => this.PART_ConnectToConsoleView.MemoryEngine = value;
    }

    public string? TypeToFocusOnOpened {
        get => this.PART_ConnectToConsoleView.TypeToFocusOnOpened;
        internal set => this.PART_ConnectToConsoleView.TypeToFocusOnOpened = value;
    }

    public UserConnectionInfo? UserConnectionInfoForConnection => this.PART_ConnectToConsoleView.CurrentConnection == null ? null : this.PART_ConnectToConsoleView.UserConnectionInfoForCurrentConnection;

    public OpenConnectionWindow() {
        this.InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) {
            this.PART_ConnectToConsoleView.isClosingWindow = true;
            this.Close();
            this.PART_ConnectToConsoleView.isClosingWindow = false;
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
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();
        if (this.IsClosed) {
            return Task.FromResult(this.closedConnection);
        }
        else if (!this.IsOpen || !this.IsLoaded) {
            Debugger.Break();
            return Task.FromResult(this.closedConnection);
        }
        
        this.tcs ??= new TaskCompletionSource<IConsoleConnection?>();
        return this.tcs.Task;
    }

    public void SetUserInfoForConnectionType(string registeredId, UserConnectionInfo info) {
        this.PART_ConnectToConsoleView.SetUserInfoForConnectionType(registeredId, info);
    }
}