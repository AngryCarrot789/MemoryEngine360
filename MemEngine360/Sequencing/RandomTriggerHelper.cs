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

namespace MemEngine360.Sequencing;

/// <summary>
/// A helper class for sequence operations that allows them to randomly trigger
/// </summary>
public class RandomTriggerHelper {
    private readonly Random random = new Random();
    
    /// <summary>
    /// Gets or sets how long to sleep in a loop waiting until we trigger. When non-null,
    /// <see cref="TryTrigger"/> is guaranteed to return true unless cancelled.
    /// </summary>
    public TimeSpan? WaitForTriggerInterval { get; set; }

    /// <summary>
    /// Gets or sets the chance that we can trigger successfully.
    /// For example. if set to 10, there's a 1/10 chance of triggering. If set to a value equal or below 1, we can always trigger
    /// </summary>
    public int Chance { get; set; }

    public RandomTriggerHelper() {
    }

    /// <summary>
    /// Attempts to trigger. Returns true when triggered successfully and the operation can commence. Returns false when trigger failed
    /// </summary>
    /// <param name="token">A cancellation token to cancel the trigger countdowns</param>
    public async Task<bool> TryTrigger(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        int chance = this.Chance;
        if (chance <= 1) {
            return true;
        }

        int rnd = this.random.Next(0, chance);
        if (rnd == 0) {
            return true;
        }

        TimeSpan? delay;
        while ((delay = this.WaitForTriggerInterval).HasValue) {
            await Task.Delay(delay.Value, token);
            if ((chance = this.Chance) <= 1 || (rnd = this.random.Next(0, chance)) == 0) {
                return true;
            }
        }

        return false;
    }
}