// 
// Copyright (c) 2026-2026 REghZy
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

namespace MemEngine360.Ps3Base;

/// <summary>
/// Represents an attached process for a PS3 connection
/// </summary>
public readonly struct Ps3Process : IEquatable<Ps3Process> {
    public static Ps3Process Default => new Ps3Process(0xFFFFFFFF, null);

    public static Ps3Process Empty => default;

    /// <summary>
    /// Gets the attached process id
    /// </summary>
    public uint ProcessId { get; }

    /// <summary>
    /// Gets the process name, or null if it has no readable name
    /// </summary>
    public string? ProcessName { get; }

    public Ps3Process(uint processId, string? processName) {
        this.ProcessId = processId;

        if (string.IsNullOrWhiteSpace(processName)) {
            this.ProcessName = null;
        }
        else {
            int length = processName.IndexOf('\0');
            if (length == -1) {
                length = processName.Length;
            }

            int idx = processName.IndexOf('_');
            if (idx > length) {
                idx = -1;
            }
            
            if (idx != -1 && processName.AsSpan(0, idx).SequenceEqual(processId.ToString("X8").AsSpan())) {
                this.ProcessName = processName.AsSpan(idx + 1, length - (idx + 1)).Trim().ToString();
            }
            else {
                this.ProcessName = processName.AsSpan(0, length).Trim().ToString();
            }
        }
    }

    public bool Equals(Ps3Process other) {
        return this.ProcessId == other.ProcessId && this.ProcessName == other.ProcessName;
    }

    public override bool Equals(object? obj) {
        return obj is Ps3Process other && this.Equals(other);
    }

    public override int GetHashCode() {
        return HashCode.Combine(this.ProcessId, this.ProcessName);
    }

    public static bool operator ==(Ps3Process left, Ps3Process right) {
        return left.Equals(right);
    }

    public static bool operator !=(Ps3Process left, Ps3Process right) {
        return !left.Equals(right);
    }
}