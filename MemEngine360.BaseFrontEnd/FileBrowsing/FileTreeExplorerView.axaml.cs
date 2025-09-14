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
using MemEngine360.Engine.FileBrowsing;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Selecting;
using PFXToolKitUI.Interactivity;

namespace MemEngine360.BaseFrontEnd.FileBrowsing;

public partial class FileTreeExplorerView : UserControl, IFileExplorerUI {
    public IListSelectionManager<IFileTreeNodeUI> SelectionManager { get; }

    public FileTreeExplorer FileTreeExplorer => this.PART_FileBrowser.FileTreeManager ?? throw new InvalidOperationException("Invalid window");
    
    public FileTreeExplorerView() {
        this.InitializeComponent();
        this.SelectionManager = new TreeViewSelectionManager<IFileTreeNodeUI>(this.PART_FileBrowser);
        DataManager.GetContextData(this).Set(IFileExplorerUI.DataKey, this);
    }
}