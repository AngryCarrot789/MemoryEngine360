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
using Avalonia.Input;
using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;

namespace MemEngine360.BaseFrontEnd.Services.Connectivity;

/// <summary>
/// A window which contains an instance of <see cref="OpenConnectionView"/>
/// </summary>
public partial class OpenConnectionViewEx : UserControl, IOpenConnectionView {
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

    public IWindow? Window { get; private set; }

    public bool IsClosed => this.Window == null || this.Window.IsClosed;

    public OpenConnectionViewEx() {
        this.InitializeComponent();
    }

    internal void OnWindowOpened(IWindow window) {
        this.PART_ConnectToConsoleView.OnWindowOpened(this.Window = window);
    }

    internal void OnWindowClosed() {
        this.PART_ConnectToConsoleView.OnWindowClosed();
        this.closedConnection = this.PART_ConnectToConsoleView.CurrentConnection;
        this.Window = null;
    }

    public void Close() => this.Window?.Close(null);

    public void Activate() => this.Window?.Activate();

    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) {
            this.PART_ConnectToConsoleView.isClosingWindow = true;
            this.Close();
            this.PART_ConnectToConsoleView.isClosingWindow = false;
        }
    }

    public async Task<IConsoleConnection?> WaitForClose() {
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();
        if (this.Window != null) {
            await this.Window.WaitForClosedAsync();
        }
        
        return this.closedConnection;
    }

    public void SetUserInfoForConnectionType(string registeredId, UserConnectionInfo info) {
        this.PART_ConnectToConsoleView.SetUserInfoForConnectionType(registeredId, info);
    }
}