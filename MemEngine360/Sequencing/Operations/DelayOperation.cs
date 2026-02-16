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

using System.Diagnostics.CodeAnalysis;
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
            if (IsDelayInvalid(value, out string? errorMessage))
                throw new ArgumentOutOfRangeException(nameof(value), value, errorMessage);

            PropertyHelper.SetAndRaiseINE(ref field, value, this, this.DelayChanged);
        }
    } = TimeSpan.Zero;

    public override string DisplayName => "Delay";

    public event EventHandler? DelayChanged;

    public DelayOperation() {
    }

    public DelayOperation(int delayMillis) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(delayMillis);
        this.Delay = TimeSpan.FromMilliseconds(delayMillis);
    }

    public static bool IsDelayInvalid(TimeSpan delay, [NotNullWhen(true)] out string? errorMessage) {
        if (delay.Ticks < TimeSpan.TicksPerMillisecond)
            errorMessage = "Delay must be 1 or more milliseconds";
        else if (delay.TotalMilliseconds >= int.MaxValue)
            errorMessage = "Delay is too large";
        else
            errorMessage = null;

        return errorMessage != null;
    }

    protected override async Task RunOperation(SequenceExecutionContext ctx, CancellationToken token) {
        double totalMs = this.Delay.TotalMilliseconds;
        ctx.Progress.Text = $"Waiting {Math.Round(totalMs, 2)} ms";

        // Don't spin-wait since it chews up CPU, and it's highly unlikely
        // such precise timing is required anyway since the next operations
        // might involve reading/writing and those take around a millisecond each
        await Time.DelayForAsyncEx(this.Delay, canSpin: false, token);
    }

    protected override BaseSequenceOperation CreateCloneCore() {
        return new DelayOperation() { Delay = this.Delay };
    }
}