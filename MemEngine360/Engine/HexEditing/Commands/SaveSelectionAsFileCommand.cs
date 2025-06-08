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

using AvaloniaHex.Core.Document;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.HexEditing.Commands;

public class SaveSelectionAsFileCommand : Command {
    public static readonly FileFilter BinaryType = FileFilter.Builder("Binary File").Patterns("*.bin").AppleUniformTypeIds("public.binary").Build();
    public static readonly IReadOnlyList<FileFilter> BinaryTypeAndAll = [BinaryType, Filters.All];
    
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IHexEditorUI.DataKey.TryGetContext(e.ContextData, out IHexEditorUI? view)) {
            return Executability.Invalid;
        }

        HexEditorInfo info = view.HexDisplayInfo!;
        if (info.Document == null) {
            return Executability.ValidButCannotExecute;
        }
        
        BitRange selection = view.SelectionRange;
        if (selection.ByteLength < 1 || selection.Start.ByteIndex >= info.Length) {
            return Executability.ValidButCannotExecute;
        }
        
        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IHexEditorUI.DataKey.TryGetContext(e.ContextData, out IHexEditorUI? view)) {
            return;
        }

        HexEditorInfo info = view.HexDisplayInfo!;
        if (info.Document == null) {
            return;
        }
        
        BitRange selection = view.SelectionRange;
        if (selection.ByteLength < 1 || selection.Start.ByteIndex >= info.Length) {
            return;
        }

        string? filePath = await IFilePickDialogService.Instance.SaveFile("Save binary data", BinaryTypeAndAll);
        if (filePath == null) {
            return;
        }
        
        byte[] buffer = new byte[selection.ByteLength]; 
        info.Document.ReadBytes(selection.Start.ByteIndex, buffer);

        try {
            await File.WriteAllBytesAsync(filePath, buffer);
        }
        catch (Exception ex) {
            await IMessageDialogService.Instance.ShowMessage("Error", $"Error writing bytes to {filePath}", ex.GetToString());
            return;
        }

        await IMessageDialogService.Instance.ShowMessage("Bytes written", $"Wrote {buffer.Length} bytes (from {(selection.Start.ByteIndex + view.CurrentStartOffset):X8} to {((selection.End.ByteIndex + view.CurrentStartOffset) - 1):X8}) to {filePath}");
    }
}