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

using System.Diagnostics;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing;

public delegate void BaseSequenceConditionEventHandler(BaseSequenceCondition sender);

public abstract class BaseSequenceCondition {
    private bool isEnabled = true;
    private bool isCurrentlyMet;
    private ConditionOutputMode outputMode;

    private bool lastMetState;
    private bool? isOnceModeLocked;
    private readonly object lockObject = new object();

    public bool IsEnabled {
        get => this.isEnabled;
        set => PropertyHelper.SetAndRaiseINE(ref this.isEnabled, value, this, static t => t.IsEnabledChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets the current met state of this condition. This is set to false when the sequence is stopped,
    /// and is updated when the sequence queries the conditions
    /// </summary>
    public bool IsCurrentlyMet {
        get => this.isCurrentlyMet;
        private set => PropertyHelper.SetAndRaiseINE(ref this.isCurrentlyMet, value, this, static t => t.IsCurrentlyMetChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets the output mode for this condition
    /// </summary>
    public ConditionOutputMode OutputMode {
        get => this.outputMode;
        set {
            lock (this.lockObject) {
                PropertyHelper.SetAndRaiseINE(ref this.outputMode, value, this, static t => t.OnOutputModeChangedUnderLock());
            }
        }
    }

    /// <summary>
    /// Gets the sequence this condition belongs to
    /// </summary>
    public TaskSequence? TaskSequence { get; private set; }

    public event BaseSequenceConditionEventHandler? IsEnabledChanged;
    public event BaseSequenceConditionEventHandler? IsCurrentlyMetChanged;
    public event BaseSequenceConditionEventHandler? OutputModeChanged;

    protected BaseSequenceCondition() {
    }

    private void OnOutputModeChangedUnderLock() {
        this.lastMetState = false;
        this.isOnceModeLocked = null;
        this.OutputModeChanged?.Invoke(this);
    }

    protected internal void OnSequenceStarted() {
        Debug.Assert(!this.IsCurrentlyMet);
        this.lastMetState = this.outputMode == ConditionOutputMode.ChangeToNotMet || this.outputMode == ConditionOutputMode.ChangeToNotMetOnce;
        this.isOnceModeLocked = null;
    }

    protected internal void OnSequenceStopped() {
        this.IsCurrentlyMet = false;
    }

    /// <summary>
    /// Updates the effective state of <see cref="IsCurrentlyMet"/> based on the enabled state, output mode and the result of <see cref="IsConditionMetCore"/>
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="cache"></param>
    /// <param name="cancellationToken"></param>
    public async Task UpdateCondition(SequenceExecutionContext ctx, CachedConditionData cache, CancellationToken cancellationToken) {
        if (!this.IsEnabled) {
            this.IsCurrentlyMet = false;
            return;
        }
        
        bool isMet = await this.IsConditionMetCore(ctx, cache, cancellationToken);
        bool isMetFinal;
        lock (this.lockObject) {
            bool lastIsMet = this.lastMetState;
            this.lastMetState = isMet;
            isMetFinal = this.isOnceModeLocked != true && ProcessIsConditionMet(isMet, lastIsMet, this.outputMode, ref this.isOnceModeLocked);
        }

        // if (this.IsCurrentlyMet != isMetFinal) {
        //     Debug.WriteLine($"({this.OutputMode}) {nameof(this.IsCurrentlyMet)} changed from {this.IsCurrentlyMet} to {isMetFinal}");
        // }

        this.IsCurrentlyMet = isMetFinal;
    }

    private static bool ProcessIsConditionMet(bool isMet, bool lastIsMet, ConditionOutputMode mode, ref bool? isLocked) {
        switch (mode) {
            case ConditionOutputMode.WhileMet:       return isMet;
            case ConditionOutputMode.WhileNotMet:    return !isMet;
            case ConditionOutputMode.ChangeToMet:    return isMet && !lastIsMet;
            case ConditionOutputMode.ChangeToNotMet: return !isMet && lastIsMet;
            case ConditionOutputMode.WhileMetOnce:
            case ConditionOutputMode.WhileNotMetOnce:
            case ConditionOutputMode.ChangeToMetOnce:
            case ConditionOutputMode.ChangeToNotMetOnce: {
                bool onMet = mode == ConditionOutputMode.WhileMetOnce || mode == ConditionOutputMode.ChangeToMetOnce;
                bool onChange = mode == ConditionOutputMode.ChangeToMetOnce || mode == ConditionOutputMode.ChangeToNotMetOnce;
                bool result = (onMet ? isMet : !isMet) && (!onChange || (onMet ? !lastIsMet : lastIsMet));
                if (!isLocked.HasValue) {
                    if (result) // Met or Not Met for the first time. Begin locking phase
                        isLocked = false;
                    return result;
                }
                else {
                    Debug.Assert(isLocked.Value == false, "IsLocked should be checked before calling " + nameof(ProcessIsConditionMet));
                    if (result)
                        return true; // still met

                    // no longer met, so set to fully locked
                    isLocked = true;
                    return false;
                }
            }
            default: throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Checks if the condition is currently met for the current running state of the task sequence
    /// </summary>
    /// <param name="ctx">The execution context for the sequence</param>
    /// <param name="cache">The cache of resolved dynamic memory addresses and data values</param>
    /// <param name="cancellationToken">A token which becomes cancelled when the task sequence is trying to be stopped</param>
    /// <returns>True if the sequence can run (other conditions may stop it running if they return false obviously)</returns>
    protected abstract Task<bool> IsConditionMetCore(SequenceExecutionContext ctx, CachedConditionData cache, CancellationToken cancellationToken);

    internal static void InternalSetSequence(BaseSequenceCondition condition, TaskSequence? sequence) => condition.TaskSequence = sequence;

    /// <summary>
    /// Creates a clone of this condition, as if the user created a new instance and configured it by hand
    /// </summary>
    /// <returns></returns>
    public abstract BaseSequenceCondition CreateClone();
}