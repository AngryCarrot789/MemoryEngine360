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

using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Sequencing;

public interface ITaskSequenceManagerUI {
    public static readonly DataKey<ITaskSequenceManagerUI> DataKey = DataKey<ITaskSequenceManagerUI>.Create(nameof(ITaskSequenceManagerUI));

    /// <summary>
    /// Gets the task sequence manager
    /// </summary>
    TaskSequencerManager Manager { get; }
    
    /// <summary>
    /// Gets the selection manager for sequences
    /// </summary>
    IListSelectionManager<ITaskSequenceEntryUI> SequenceSelectionManager { get; }
    
    /// <summary>
    /// Gets the selection manager for the currently selected operations
    /// </summary>
    IListSelectionManager<IOperationItemUI> OperationSelectionManager { get; }
    
    IListSelectionManager<IConditionItemUI> ConditionSelectionManager { get; }
    
    /// <summary>
    /// Gets the primary selected task sequence item, or null, if there's none selected or more than 1
    /// </summary>
    ITaskSequenceEntryUI? PrimarySelectedSequence { get; }
    
    /// <summary>
    /// Gets the primary selected operation, or null, if there's none selected or more than 1
    /// </summary>
    IOperationItemUI? PrimarySelectedOperation { get; }
    
    ITaskSequenceEntryUI GetSequenceControl(TaskSequence sequence);
    
    IOperationItemUI GetOperationControl(BaseSequenceOperation operation);
    
    IConditionItemUI GetConditionControl(BaseSequenceCondition condition);
}