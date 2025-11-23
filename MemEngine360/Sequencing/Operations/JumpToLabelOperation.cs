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

using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Sequencing.Operations;

/// <summary>
/// An operation that just jumps to a <see cref="LabelOperation"/> when it runs
/// </summary>
public class JumpToLabelOperation : BaseSequenceOperation {
    public override string DisplayName => "Jump to";

    /// <summary>
    /// Gets the currently located target label
    /// </summary>
    public LabelOperation? CurrentTarget { get; private set; }

    /// <summary>
    /// Gets the target label's name
    /// </summary>
    public string? TargetLabel {
        get => field;
        private set {
            if (string.IsNullOrWhiteSpace(value))
                value = null;
            PropertyHelper.SetAndRaiseINE(ref field, value, this, this.TargetLabelChanged);
        }
    }

    public event EventHandler? TargetLabelChanged;
    
    public event EventHandler<ValueChangedEventArgs<LabelOperation?>>? CurrentTargetChanged;

    public JumpToLabelOperation() {
    }

    protected override Task RunOperation(SequenceExecutionContext ctx, CancellationToken token) {
        // rely on TaskSequencer to implement jumping logic
        return Task.CompletedTask;
    }

    protected override BaseSequenceOperation CreateCloneCore() {
        JumpToLabelOperation label = new JumpToLabelOperation();
        label.SetTarget(this.TargetLabel, this.CurrentTarget);
        return label;
    }

    public void SetTarget(string? targetName, LabelOperation? operation) {
        if (string.IsNullOrWhiteSpace(targetName))
            targetName = null;

        this.TargetLabel = targetName;
        
        LabelOperation? oldTarget = this.CurrentTarget;
        if (!ReferenceEquals(oldTarget, operation)) {
            this.CurrentTarget = operation;
            if (oldTarget != null)
                oldTarget.LabelNameChanged -= this.OnCurrentTargetNameChanged;
            if (operation != null)
                operation.LabelNameChanged += this.OnCurrentTargetNameChanged;
            
            this.CurrentTargetChanged?.Invoke(this, new ValueChangedEventArgs<LabelOperation?>(oldTarget, operation));
        }
    }

    private void OnCurrentTargetNameChanged(object? o, EventArgs e) {
        this.TargetLabel = ((LabelOperation) o!).LabelName;
    }
}