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

using System.Collections.Immutable;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using MemEngine360.Engine.Addressing;
using MemEngine360.PointerScanning;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes.Virtualizing;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.ToolTips;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.PointerScanning;

public class PointerScanResultListBox : VirtualizingModelListBox {
    protected override Type StyleKeyOverride => typeof(ListBox);

    public PointerScanResultListBox() {
    }

    protected override VirtualizingModelListBoxItem CreateListBoxItem() {
        return new PointerScanResultListBoxItem();
    }
}

public class PointerScanResultListBoxItem : VirtualizingModelListBoxItem {
    protected override Type StyleKeyOverride => typeof(ListBoxItem);

    private AsyncRelayCommand? showPointerChainCommand;

    public PointerScanResultListBoxItem() {
        ToolTipEx.SetTipType(this, typeof(PointerScanResultToolTip));
        this.Height = 20;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        base.OnPointerPressed(e);
        if (e.ClickCount > 1) {
            this.showPointerChainCommand ??= new AsyncRelayCommand(this.ShowPointerChainInDialog);
            this.showPointerChainCommand.Execute(null);
        }
    }

    private async Task ShowPointerChainInDialog() {
        if (this.Model == null) return;
        
        IContextData data = DataManager.GetFullContextData(this);
        if (!PointerScanWindow.PointerScannerDataKey.TryGetContext(data, out PointerScanner? scanner)) {
            // I mean we could just access the PointerScannerWindow via TopLevel.GetTopLevel(this)...
            return;
        }

        StringBuilder sb = new StringBuilder();
        DynamicAddress address = (DynamicAddress) this.Model;
        sb.AppendLine(($"{address.BaseAddress:X8} points to {GetPointerValue(scanner, address.BaseAddress, out uint lastValue)}"));
        ImmutableArray<int> offsets = address.Offsets;
        if (offsets.Length > 0) {
            sb.AppendLine();
            
            for (int i = 0; i < offsets.Length - 1; i++) {
                uint actualAddress = (uint) (lastValue + offsets[i]);
                sb.AppendLine(($"{lastValue:X8} + {offsets[i]:X5} (={actualAddress:X8}) points to {GetPointerValue(scanner, actualAddress, out lastValue)}"));
                sb.AppendLine();
            }

            int lastOffset = offsets[offsets.Length - 1];
            sb.AppendLine(($"{lastValue:X8} + {lastOffset:X5} = {(lastValue + lastOffset):X8}"));
            // soft assert (lastValue + lastOffset) == scanner.SearchAddress
        }

        await IMessageDialogService.Instance.ShowMessage("Pointer Chain", sb.ToString(), MessageBoxButton.OK, MessageBoxResult.OK);
    }

    private static string GetPointerValue(PointerScanner scanner, uint address, out uint lastValue) {
        if (scanner.PointerMap.TryGetValue(address, out lastValue)) {
            return lastValue.ToString("X8");
        }
        
        if (address == scanner.SearchAddress)
            return address.ToString("X8");
        
        return "???";
    }
    
    protected override void OnAddingToList() {
    }

    protected override void OnAddedToList() {
    }

    protected override void OnRemovingFromList() {
    }

    protected override void OnRemovedFromList() {
    }

    protected override void OnModelChanged(object? oldModel, object? newModel) {
        base.OnModelChanged(oldModel, newModel);
        this.Content = newModel;
    }
}