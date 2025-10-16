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
using PFXToolKitUI.Avalonia.Services.UserInputs;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.PS3.ProcessSelector;

public partial class ProcessSelectorView : UserControl, IUserInputContent {
    public ProcessSelectorView() {
        InitializeComponent();
    }

    public void Connect(UserInputDialogView dialog, UserInputInfo info) {
        
    }

    public void Disconnect() {
        
    }

    public bool FocusPrimaryInput() {
        return false;
    }
}