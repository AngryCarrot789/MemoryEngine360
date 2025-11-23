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
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Sequencing.Operations;

public class DelayOperation : BaseSequenceOperation {
    /// <summary>
    /// Gets or sets this operation's delay time. Setting to null results in <see cref="RunOperation"/> returning a completed task
    /// </summary>
    public TimeSpan Delay {
        get => field;
        set {
            if (value.TotalMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Delay must be positive");
            if (value.TotalMilliseconds >= int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Delay is too large");

            PropertyHelper.SetAndRaiseINE(ref field, value, this, this.DelayChanged);
        }
    } = TimeSpan.Zero;

    public override string DisplayName => "Delay";

    public event EventHandler? DelayChanged;

    public DelayOperation() {
    }

    public DelayOperation(int delayMillis) {
        this.Delay = TimeSpan.FromMilliseconds(delayMillis);
    }

    protected override async Task RunOperation(SequenceExecutionContext ctx, CancellationToken token) {
        double totalMs = this.Delay.TotalMilliseconds;
        ctx.Progress.Text = $"Waiting {Math.Round(totalMs, 2)} ms";

        if (DoubleUtils.GreaterThanOrClose(totalMs, 20.0)) {
            await Task.Delay(this.Delay, token);
        }
        else {
            await Time.DelayForAsync(this.Delay, token);
        }
    }

    protected override BaseSequenceOperation CreateCloneCore() {
        return new DelayOperation() { Delay = this.Delay };
    }
}