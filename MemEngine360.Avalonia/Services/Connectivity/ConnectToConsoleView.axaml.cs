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

using System.Linq;
using Avalonia.Controls;
using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Services;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.Avalonia.Services.Connectivity;

/// <summary>
/// The view for the Connect to Console window
/// </summary>
public partial class ConnectToConsoleView : WindowingContentControl {
    public static readonly ModelControlRegistry<UserConnectionInfo, Control> Registry;
    
    public IConsoleConnection? Result { get; private set; }

    public MemoryEngine360 Engine { get; init; }
    
    public string? FocusedTypeId { get; init; }

    private readonly AsyncRelayCommand connectCommand;
    
    public ConnectToConsoleView() {
        this.InitializeComponent();
        this.PART_ListBox.SelectionMode = SelectionMode.Single;
        this.PART_ListBox.SelectionChanged += this.PART_ListBoxOnSelectionChanged;

        this.PART_CancelButton.Click += (sender, args) => {
            this.Window!.Close();
        };

        this.PART_ConfirmButton.Command = this.connectCommand = new AsyncRelayCommand(async () => {
            ConsoleTypeListBoxItem? selection = ((ConsoleTypeListBoxItem?) this.PART_ListBox.SelectedItem);
            if (selection != null && selection.RegisteredConsoleType != null) {
                IConsoleConnection? item = await selection.RegisteredConsoleType.OpenConnection(this.Engine!, selection.UserConnectionInfo);
                if (item != null) {
                    this.Result = item;
                    this.Window!.Close();
                }
            }
        });
    }
    
    static ConnectToConsoleView() {
        Registry = new ModelControlRegistry<UserConnectionInfo, Control>();
    }

    protected override void OnWindowOpened() {
        base.OnWindowOpened();

        this.Window!.Control.MinWidth = 600;
        this.Window!.Control.MinHeight = 350;
        this.Window!.Control.Width = 700;
        this.Window!.Control.Height = 450;

        ConsoleTypeListBoxItem? selected = null;
        ConsoleConnectionManager service = ApplicationPFX.Instance.ServiceManager.GetService<ConsoleConnectionManager>();
        foreach (RegisteredConsoleType type in service.RegisteredConsoleTypes) {
            ConsoleTypeListBoxItem item = new ConsoleTypeListBoxItem(this.Engine, type);
            if (selected != null && this.FocusedTypeId != null && type.RegisteredId == this.FocusedTypeId)
                selected = item;
            this.PART_ListBox.Items.Add(item);
        }

        this.PART_ListBox.SelectedItem = selected ?? this.PART_ListBox.Items.FirstOrDefault();

        this.PART_ConfirmButton.Focus();
    }

    protected override void OnWindowClosed() {
        base.OnWindowClosed();
        foreach (ConsoleTypeListBoxItem itm in this.PART_ListBox.Items)
            itm!.RegisteredConsoleType = null;
        this.PART_ListBox.Items.Clear();
    }

    private void PART_ListBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        ConsoleTypeListBoxItem? selected = this.PART_ListBox.SelectedItem as ConsoleTypeListBoxItem;
        
        this.PART_DisplayName.Text = selected?.RegisteredConsoleType!.DisplayName;
        this.PART_Description.Text = selected?.RegisteredConsoleType!.Description;
        
        if (this.PART_UserConnectionContent.Content is IConsoleConnectivityControl c1)
            c1.OnDisconnected();

        UserConnectionInfo? newInfo = selected?.UserConnectionInfo;
        Control? control = newInfo != null ? Registry.NewInstance(newInfo) : null;
        this.PART_UserConnectionContent.Content = control;
        if (control is IConsoleConnectivityControl c2)
            c2.OnConnected(this, newInfo!);
    }
}