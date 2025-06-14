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

using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing;

public delegate void BaseSequenceConditionEventHandler(BaseSequenceCondition sender);

public abstract class BaseSequenceCondition {
    private bool isEnabled = true;

    public bool IsEnabled {
        get => this.isEnabled;
        set => PropertyHelper.SetAndRaiseINE(ref this.isEnabled, value, this, static t => t.IsEnabledChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets the sequence this condition belongs to
    /// </summary>
    public TaskSequence? TaskSequence { get; private set; }

    public event BaseSequenceConditionEventHandler? IsEnabledChanged;

    protected BaseSequenceCondition() {
    }

    /// <summary>
    /// Checks if the condition is currently met for the current running state of the task sequence
    /// </summary>
    /// <param name="ctx">The execution context for the sequence</param>
    /// <param name="dataValues">
    ///     A dictionary of cached data values, keyed by a data type and address. This prevents having to re-reading
    ///     the same value from the console, however, this dictionary can be ignored for maximum safety
    /// </param>
    /// <param name="token">A token which becomes cancelled when the sequence is stopped</param>
    /// <returns>True if the sequence can run (other conditions may stop it running if they return false obviously)</returns>
    public abstract Task<bool> IsConditionMet(SequenceExecutionContext ctx, Dictionary<TypedAddress, IDataValue> dataValues, CancellationToken token);
    
    internal static void InternalSetSequence(BaseSequenceCondition condition, TaskSequence? sequence) => condition.TaskSequence = sequence;
    
    /// <summary>
    /// Creates a clone of this condition, as if the user created a new instance and configured it by hand
    /// </summary>
    /// <returns></returns>
    public abstract BaseSequenceCondition CreateClone();
}