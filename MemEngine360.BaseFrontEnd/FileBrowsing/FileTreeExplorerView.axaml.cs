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

using Avalonia;
using Avalonia.Controls;
using MemEngine360.Engine.FileBrowsing;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.SelectingEx2;

namespace MemEngine360.BaseFrontEnd.FileBrowsing;

public partial class FileTreeExplorerView : UserControl {
    public static readonly StyledProperty<FileTreeExplorer?> FileTreeExplorerProperty = AvaloniaProperty.Register<FileTreeExplorerView, FileTreeExplorer?>(nameof(FileTreeExplorer));

    public FileTreeExplorer? FileTreeExplorer {
        get => this.GetValue(FileTreeExplorerProperty);
        set => this.SetValue(FileTreeExplorerProperty, value);
    }

    private TreeViewSelectionModelBinder<BaseFileTreeNode>? selectionBinder;

    public FileTreeExplorerView() {
        this.InitializeComponent();
    }

    static FileTreeExplorerView() {
        FileTreeExplorerProperty.Changed.AddClassHandler<FileTreeExplorerView, FileTreeExplorer?>((s, e) => s.OnFileTreeExplorerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnFileTreeExplorerChanged(FileTreeExplorer? oldValue, FileTreeExplorer? newValue) {
        this.PART_FileBrowser.FileTreeExplorer = newValue;

        if (this.selectionBinder != null) {
            this.selectionBinder.Dispose();
            this.selectionBinder = null;
        }

        if (newValue != null) {
            this.selectionBinder = new TreeViewSelectionModelBinder<BaseFileTreeNode>(
                this.PART_FileBrowser,
                FileTreeExplorerViewState.GetInstance(newValue).TreeSelection,
                tvi => ((FileBrowserTreeViewItem) tvi).EntryObject!,
                model => this.PART_FileBrowser.ItemMap.GetControl(model));
        }
        
        DataManager.GetContextData(this).Set(FileTreeExplorer.DataKey, newValue);
    }
}