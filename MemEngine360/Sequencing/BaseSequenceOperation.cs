// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemEngine360.
// 
// MemEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemEngine360. If not, see <https://www.gnu.org/licenses/>.
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
        private set {
            if (this.isRunning == value)
                return;

            this.isRunning = value;
            this.IsRunningChanged?.Invoke(this);
        }
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
    public async Task Run(SequenceExecutionContext ctx, CancellationToken token) {
        if (this.IsRunning) {
            throw new InvalidOperationException("Already running");
        }

        if (!await this.RandomTriggerHelper.TryTrigger(token)) {
            return;
        }

        this.IsRunning = true;
        
        OperationCanceledException? cancellation = null;
        using (ErrorList list = new ErrorList("One or more errors occurred", true, true)) {
            try {
                await this.RunOperation(ctx, token);
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
    /// Runs this operation
    /// </summary>
    /// <param name="ctx">Sequence execution information (containing progress, connection and more)</param>
    /// <param name="token">A token used to indicate whether the user wants to stop the sequence executing</param>
    /// <returns>A task that represents the operation</returns>
    protected abstract Task RunOperation(SequenceExecutionContext ctx, CancellationToken token);

    internal static void InternalSetSequence(BaseSequenceOperation operation, TaskSequence? sequence) => operation.Sequence = sequence;
}