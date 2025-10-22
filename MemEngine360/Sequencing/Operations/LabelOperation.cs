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

using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing.Operations;

public delegate void LabelOperationEventHandler(LabelOperation sender);

/// <summary>
/// An operation that acts as a label
/// </summary>
public class LabelOperation : BaseSequenceOperation, IPlaceholderOperation {
    private string? labelName;

    public override string DisplayName => "Label";

    public string? LabelName {
        get => this.labelName;
        set {
            if (string.IsNullOrWhiteSpace(value))
                value = null;
            PropertyHelper.SetAndRaiseINE(ref this.labelName, value, this, static t => t.LabelNameChanged?.Invoke(t));
            this.TaskSequence?.UpdateAllJumpTargets();
        }
    }

    public event LabelOperationEventHandler? LabelNameChanged;

    protected override Task RunOperation(SequenceExecutionContext ctx, CancellationToken token) {
        return Task.CompletedTask;
    }

    protected override BaseSequenceOperation CreateCloneCore() {
        return new LabelOperation() { LabelName = this.LabelName };
    }
}