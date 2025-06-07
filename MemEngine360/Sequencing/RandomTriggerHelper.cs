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

using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing;

public delegate void RandomTriggerHelperEventHandler(RandomTriggerHelper sender);

/// <summary>
/// A helper class for sequence operations that allows them to randomly trigger
/// </summary>
public class RandomTriggerHelper {
    private readonly Random random = new Random();
    private TimeSpan? waitForTriggerInterval;
    private uint chance = 1;

    /// <summary>
    /// Gets or sets how long to sleep in a loop waiting until we trigger. When non-null,
    /// <see cref="TryTrigger"/> is guaranteed to return true unless cancelled.
    /// </summary>
    public TimeSpan? WaitForTriggerInterval {
        get => this.waitForTriggerInterval;
        set {
            if (value is TimeSpan ts && (ts.TotalMilliseconds < 0 || ts.TotalMilliseconds >= uint.MaxValue)) {
                throw new ArgumentOutOfRangeException(nameof(value), value, "TimeSpan is out of range. Cannot be negative or millis cannot exceed uint.MaxValue");
            }

            PropertyHelper.SetAndRaiseINE(ref this.waitForTriggerInterval, value, this, static t => t.WaitForTriggerIntervalChanged?.Invoke(t));
        }
    }

    /// <summary>
    /// Gets or sets the chance that we can trigger successfully. Minimum value is 1
    /// For example. if set to 10, there's a 1/10 chance of triggering. If set to a value equal or below 1, we can always trigger
    /// </summary>
    public uint Chance {
        get => this.chance;
        set {
            value = Math.Max(value, 1);
            PropertyHelper.SetAndRaiseINE(ref this.chance, value, this, static t => t.ChanceChanged?.Invoke(t));
        }
    }

    public event RandomTriggerHelperEventHandler? ChanceChanged;
    public event RandomTriggerHelperEventHandler? WaitForTriggerIntervalChanged;

    public RandomTriggerHelper() {
    }

    /// <summary>
    /// Attempts to trigger. Returns true when triggered successfully and the operation can commence. Returns false when trigger failed
    /// </summary>
    /// <param name="token">A cancellation token to cancel the trigger countdowns</param>
    public async Task<bool> TryTrigger(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        uint theChance = this.Chance;
        if (theChance <= 1) {
            return true;
        }

        int rnd = this.random.Next(0, (int) theChance);
        if (rnd == 0) {
            return true;
        }

        // When WaitForTriggerInterval is null, we skip the loop and just return false
        TimeSpan? delay;
        while ((delay = this.WaitForTriggerInterval).HasValue) {
            await Task.Delay(delay.Value, token);
            if ((theChance = this.Chance) <= 1 || (rnd = this.random.Next(0, (int) theChance)) == 0) {
                return true;
            }
        }

        return false;
    }
}