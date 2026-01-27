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
using PFXToolKitUI.Interactivity.Windowing;

namespace MemEngine360.Sequencing.View;

/// <summary>
/// The view-state for a <see cref="BaseSequenceOperation"/>
/// </summary>
public sealed class SequenceOperationViewState {
    /// <summary>
    /// Gets the operation
    /// </summary>
    public BaseSequenceOperation Operation { get; }
    
    /// <summary>
    /// Gets the list of selected conditions for this operation
    /// </summary>
    public ListSelectionModel<BaseSequenceCondition> SelectedConditions { get; }
    
    public TopLevelIdentifier TopLevelIdentifier { get; }
    
    private SequenceOperationViewState(BaseSequenceOperation operation, TopLevelIdentifier topLevelIdentifier) {
        this.Operation = operation;
        this.TopLevelIdentifier = topLevelIdentifier;
        this.SelectedConditions = new ListSelectionModel<BaseSequenceCondition>(operation.Conditions);
    }
    
    public static SequenceOperationViewState GetInstance(BaseSequenceOperation sequence, TopLevelIdentifier topLevelIdentifier) {
        return TopLevelDataMap.GetInstance(sequence).GetOrCreate<SequenceOperationViewState>(topLevelIdentifier, sequence, static (s, i) => new SequenceOperationViewState((BaseSequenceOperation) s!, i));
    }
}