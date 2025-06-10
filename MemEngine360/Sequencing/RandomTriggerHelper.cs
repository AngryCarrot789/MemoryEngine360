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

namespace MemEngine360.Sequencing;

public delegate void RandomTriggerHelperEventHandler(RandomTriggerHelper sender);

/// <summary>
/// A helper class for sequence operations that allows them to randomly trigger
/// </summary>
public class RandomTriggerHelper {
    private readonly Random random = new Random();
    private TimeSpan? waitForTriggerInterval;
    private uint chance = 1;
    private uint minimumTriesToTrigger;
    private uint triggerAttemptsRemaining; // this is set to minimumTriesToTrigger on successful trigger 
    private readonly object triggerDataLock = new object();

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

    /// <summary>
    /// Gets or sets the minimum amount of tries since the last successful trigger that must be attempted before
    /// we can actually try to trigger. Default value is 0, meaning trigger as much as you want
    /// </summary>
    public uint MinimumTriesToTrigger {
        get => this.minimumTriesToTrigger;
        set {
            if (this.minimumTriesToTrigger != value) {
                lock (this.triggerDataLock) {
                    // Value was set to a smaller value, and we haven't triggered that much since it was initially set
                    if (this.triggerAttemptsRemaining > value) {
                        this.triggerAttemptsRemaining = value;
                    }
                
                    this.minimumTriesToTrigger = value;   
                }
                
                this.MinimumTriesToTriggerChanged?.Invoke(this);
            }
        }
    }

    public event RandomTriggerHelperEventHandler? ChanceChanged;
    public event RandomTriggerHelperEventHandler? WaitForTriggerIntervalChanged;
    public event RandomTriggerHelperEventHandler? MinimumTriesToTriggerChanged;

    public RandomTriggerHelper() {
    }

    /// <summary>
    /// Attempts to trigger. Returns true when triggered successfully and the operation can commence. Returns false when trigger failed
    /// </summary>
    /// <param name="token">A cancellation token to cancel the trigger countdowns</param>
    public async Task<bool> TryTrigger(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        if (this.triggerAttemptsRemaining != 0) {
            lock (this.triggerDataLock) {
                if (this.triggerAttemptsRemaining != 0) {
                    this.triggerAttemptsRemaining--;
                    return false;
                }
            }
        }
        
        uint chanceToTrigger = this.Chance;
        if (chanceToTrigger <= 1) {
            lock (this.triggerDataLock)
                this.triggerAttemptsRemaining = this.minimumTriesToTrigger;
            return true;
        }

        int rnd = this.random.Next(0, (int) chanceToTrigger);
        if (rnd == 0) {
            lock (this.triggerDataLock)
                this.triggerAttemptsRemaining = this.minimumTriesToTrigger;
            return true;
        }

        // When WaitForTriggerInterval is null, we skip the loop and just return false
        TimeSpan? delay;
        while ((delay = this.WaitForTriggerInterval).HasValue) {
            await Task.Delay(delay.Value, token);
            if ((chanceToTrigger = this.Chance) <= 1 || (rnd = this.random.Next(0, (int) chanceToTrigger)) == 0) {
                lock (this.triggerDataLock)
                    this.triggerAttemptsRemaining = this.minimumTriesToTrigger;
                return true;
            }
        }

        return false;
    }
}