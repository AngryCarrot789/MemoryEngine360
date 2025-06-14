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

using PFXToolKitUI.DataTransfer;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing;

public delegate void BaseSequenceOperationEventHandler(BaseSequenceOperation sender);

/// <summary>
/// The base class for a sequencer operation
/// </summary>
public abstract class BaseSequenceOperation : ITransferableData {
    private bool isRunning;
    private bool isEnabled = true;

    public TransferableData TransferableData { get; }

    /// <summary>
    /// Gets the sequence that owns this operation
    /// </summary>
    public TaskSequence? Sequence { get; private set; }

    /// <summary>
    /// Gets whether this operation is currently running. This may be changed from any thread
    /// </summary>
    public bool IsRunning {
        get => this.isRunning;
        private set => PropertyHelper.SetAndRaiseINE(ref this.isRunning, value, this, static t => t.IsRunningChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets whether this operation is enabled, meaning can it actually run when the sequence is running
    /// </summary>
    public bool IsEnabled {
        get => this.isEnabled;
        set => PropertyHelper.SetAndRaiseINE(ref this.isEnabled, value, this, static t => t.IsEnabledChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets a short readable description of this operation, e.g. "Set Memory"
    /// </summary>
    public abstract string DisplayName { get; }

    /// <summary>
    /// Gets the random trigger helper for this operation
    /// </summary>
    public RandomTriggerHelper RandomTriggerHelper { get; }

    /// <summary>
    /// Fired when <see cref="IsRunning"/> changes. This may be fired on any thread
    /// </summary>
    public event BaseSequenceOperationEventHandler? IsRunningChanged;

    public event BaseSequenceOperationEventHandler? IsEnabledChanged;

    protected BaseSequenceOperation() {
        this.TransferableData = new TransferableData(this);
        this.RandomTriggerHelper = new RandomTriggerHelper();
    }

    /// <summary>
    /// Runs this operation. Sets <see cref="IsRunning"/> to true before calling <see cref="RunOperation"/>
    /// and then false once completed, even after an exception
    /// </summary>
    /// <param name="ctx">Sequence execution information (containing progress, connection and more)</param>
    /// <param name="token">A token used to indicate whether the user wants to stop the sequence executing</param>
    /// <exception cref="InvalidOperationException">Already running</exception>
    /// <exception cref="OperationCanceledException">Operation cancelled</exception>
    /// <returns>A task that represents the operation</returns>
    internal async Task Run(SequenceExecutionContext ctx, CancellationToken token) {
        if (this.IsRunning) {
            throw new InvalidOperationException("Already running");
        }

        if (!this.IsEnabled || !await this.RandomTriggerHelper.TryTrigger(token)) {
            return;
        }

        this.IsRunning = true;

        OperationCanceledException? cancellation = null;
        using (ErrorList list = new ErrorList("One or more errors occurred", true, true)) {
            try {
                Task task = this.RunOperation(ctx, token);
                bool wasCompletedSync = task.IsCompleted;
                await task;

                if (wasCompletedSync) {
                    // Yielding here prevents this operation from running about 1 million times a second,
                    // which may be battering the UI thread with IsRunning being switched so often.
                    // Yielding brings it down to about 15,000 times a second.
                    // Of course, may be different for faster/slower systems than a ryzen 5
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException e) {
                cancellation = e;
            }
            catch (Exception e) {
                list.Add(e);
            }

            try {
                this.IsRunning = false;
            }
            catch (Exception e) {
                list.Add(e);
            }
        }

        if (cancellation != null) {
            throw cancellation;
        }
    }

    /// <summary>
    /// Invoked on all operations before any operation is run. 
    /// </summary>
    protected internal virtual void OnSequenceStarted() {
        
    }
    
    /// <summary>
    /// Invoked on all operations after the sequence has finished. <see cref="RunOperation"/> may
    /// not have actually been called (maybe sequence was stopped mid-way or conditions not met)
    /// </summary>
    protected internal virtual void OnSequenceStopped() {
        
    }
    
    /// <summary>
    /// Runs this operation
    /// </summary>
    /// <param name="ctx">Sequence execution information (containing progress, connection and more)</param>
    /// <param name="token">A token used to indicate whether the user wants to stop the sequence executing</param>
    /// <returns>A task that represents the operation</returns>
    protected abstract Task RunOperation(SequenceExecutionContext ctx, CancellationToken token);

    internal static void InternalSetSequence(BaseSequenceOperation operation, TaskSequence? sequence) => operation.Sequence = sequence;

    /// <summary>
    /// Creates a clone of this operation as if the user created it by hand.
    /// </summary>
    /// <returns></returns>
    public abstract BaseSequenceOperation CreateClone();
}