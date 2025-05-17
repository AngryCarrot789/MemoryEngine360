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

namespace MemEngine360.Sequencing.Operations;

public delegate void DelayOperationEventHandler(DelayOperation sender);

public class DelayOperation : BaseSequenceOperation {
    private uint delay;

    /// <summary>
    /// Gets or sets this operation's delay time. Setting to null results in <see cref="RunOperation"/> returning a completed task
    /// </summary>
    public uint Delay {
        get => this.delay;
        set {
            if (this.delay == value)
                return;

            this.delay = value;
            this.DelayChanged?.Invoke(this);
        }
    }

    public event DelayOperationEventHandler? DelayChanged;

    public DelayOperation() {
    }

    public DelayOperation(uint delay) {
        this.delay = delay;
    }

    protected override Task RunOperation(SequenceExecutionContext ctx, CancellationToken token) {
        ctx.Progress.Text = "Waiting " + this.delay + " ms";
        return Task.Delay((int) this.Delay, token);
    }
}