// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of PFXToolKitUI.
// 
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3 of the License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with PFXToolKitUI. If not, see <https://www.gnu.org/licenses/>.
// 

using PFXToolKitUI.Interactivity.Selections;

namespace MemEngine360.Sequencing.View;

public class SequenceOperationViewState {
    public BaseSequenceOperation Operation { get; }
    
    /// <summary>
    /// Gets the list of selected conditions for this operation
    /// </summary>
    public ListSelectionModel<BaseSequenceCondition> SelectedConditions { get; }
    
    public SequenceOperationViewState(BaseSequenceOperation operation) {
        this.Operation = operation;
        this.SelectedConditions = new ListSelectionModel<BaseSequenceCondition>(operation.Conditions);
    }
    
    public static SequenceOperationViewState GetInstance(BaseSequenceOperation operation) => operation.internalViewState ??= new SequenceOperationViewState(operation);
}