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
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using MemEngine360.Engine.Addressing;
using MemEngine360.PointerScanning;
using PFXToolKitUI.Avalonia.ToolTips;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.BaseFrontEnd.PointerScanning;

public partial class PointerScanResultToolTip : UserControl, IToolTipControl {
    public PointerScanResultToolTip() {
        this.InitializeComponent();
    }

    public void OnOpened(Control owner, IContextData data) {
        if (!PointerScanWindow.PointerScannerDataKey.TryGetContext(data, out PointerScanner? scanner)) {
            // I mean we could just access the PointerScannerWindow via TopLevel.GetTopLevel(this)...
            return;
        }
        
        InlineCollection inlines = this.PART_TextBlock.Inlines ??= new InlineCollection();
        DynamicAddress address = (DynamicAddress) ((ListBoxItem) owner).Content!;
        inlines.Add(new Run($"{address.BaseAddress:X8} -> {GetPointerValue(scanner, address.BaseAddress, out uint lastValue)}"));
        ImmutableArray<int> offsets = address.Offsets;
        if (offsets.Length > 0) {
            inlines.Add(new LineBreak());
            
            for (int i = 0; i < offsets.Length - 1; i++) {
                uint actualAddress = (uint) (lastValue + offsets[i]);
                inlines.Add(new Run($"{lastValue:X8} + {offsets[i]:X5} (={actualAddress:X8}) -> {GetPointerValue(scanner, actualAddress, out lastValue)}"));
                inlines.Add(new LineBreak());
            }

            int lastOffset = offsets[offsets.Length - 1];
            inlines.Add(new Run($"{lastValue:X8} + {lastOffset:X5} = {(lastValue + lastOffset):X8}"));
            // soft assert (lastValue + lastOffset) == scanner.SearchAddress
        }
    }

    private static string GetPointerValue(PointerScanner scanner, uint address, out uint lastValue) {
        if (scanner.PointerMap.TryGetValue(address, out lastValue)) {
            return lastValue.ToString("X8");
        }
        
        if (address == scanner.SearchAddress)
            return address.ToString("X8");
        
        return "???";
    }

    public void OnClosed(Control owner) {
        this.PART_TextBlock.Inlines?.Clear();
    }
}