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

using AvaloniaHex.Base.Document;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Engine.HexEditing.Commands;

public class SetAutoScanRangeAsSelectionCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        return e.ContextData.ContainsKey(IHexEditorUI.DataKey) ? Executability.Valid : Executability.Invalid;
    }

    protected override Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IHexEditorUI.DataKey.TryGetContext(e.ContextData, out IHexEditorUI? view)) {
            return Task.CompletedTask;
        }

        BitRange selection = view.SelectionRange;

        uint startAddr = (uint) (view.CurrentStartOffset + selection.Start.ByteIndex);
        uint length = (uint) selection.ByteLength;

        view.HexDisplayInfo!.AutoRefreshStartAddress = startAddr;
        view.HexDisplayInfo!.AutoRefreshLength = length;

        if (startAddr != 0 && length != 0)
            view.HexDisplayInfo.RaiseRestartAutoRefresh();

        return Task.CompletedTask;
    }
}