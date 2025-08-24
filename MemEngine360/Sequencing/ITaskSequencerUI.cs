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

using PFXToolKitUI;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Sequencing;

/// <summary>
/// Represents the task sequencer window
/// </summary>
public interface ITaskSequencerUI {
    public static readonly DataKey<ITaskSequencerUI> DataKey = DataKey<ITaskSequencerUI>.Create(nameof(ITaskSequencerUI));

    bool IsValid { get; }

    /// <summary>
    /// Gets the task sequence manager
    /// </summary>
    TaskSequencerManager Manager { get; }

    /// <summary>Gets the mode-control map for mapping task sequences to and from the UI object</summary>
    IModelControlMap<TaskSequence, ITaskSequenceItemUI> TaskSequenceItemMap { get; }

    /// <summary>Gets the mode-control map for mapping operations to and from the UI object</summary>
    IModelControlMap<BaseSequenceOperation, IOperationItemUI> OperationItemMap { get; }

    /// <summary>Gets the mode-control map for mapping conditions to and from the UI object</summary>
    IModelControlMap<BaseSequenceCondition, IConditionItemUI> ConditionItemMap { get; }

    /// <summary>Gets the selection manager for the sequences list</summary>
    IListSelectionManager<ITaskSequenceItemUI> SequenceSelectionManager { get; }

    /// <summary>Gets the selection manager for the operations list</summary>
    IListSelectionManager<IOperationItemUI> OperationSelectionManager { get; }

    /// <summary>Gets the selection manager for the conditions list</summary>
    IListSelectionManager<IConditionItemUI> ConditionSelectionManager { get; }

    /// <summary>
    /// Gets the primary selected task sequence item, or null, if there's none selected or more than 1
    /// </summary>
    ITaskSequenceItemUI? PrimarySelectedSequence { get; }

    /// <summary>
    /// Gets the primary selected operation, or null, if there's none selected or more than 1
    /// </summary>
    IOperationItemUI? PrimarySelectedOperation { get; }
}