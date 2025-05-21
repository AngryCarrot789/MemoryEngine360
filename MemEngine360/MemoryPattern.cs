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

namespace MemEngine360;

public readonly struct MemoryPattern {
    internal readonly byte?[] pattern;

    public byte? this[int i] => this.pattern[i];
    
    public int Length => this.pattern?.Length ?? 0;

    public bool IsValid => this.pattern != null;
    
    private MemoryPattern(byte?[] pattern) {
        this.pattern = pattern;
    }

    public static MemoryPattern Create(byte?[] pattern) {
        ArgumentNullException.ThrowIfNull(pattern);
        return new MemoryPattern((byte?[]) pattern.Clone());
    }

    public static void Main() {
        MemoryPattern pattern = MemoryPattern.Compile("45 ? 25 ?? FF");
    }

    private static void CheckChar(string pattern, char ch, int idx) {
        if ((ch < '0' || ch > '9') && (ch < 'a' || ch > 'f'))
            throw new ArgumentException($"Invalid char '{ch}' at idx {idx} in pattern: " + pattern);
    }
    
    public static MemoryPattern Compile(string pattern) {
        string[] tokens = pattern.Split(' ', int.MaxValue, StringSplitOptions.RemoveEmptyEntries);
        byte?[] data = new byte?[tokens.Length];
        for (int i = 0; i < tokens.Length; i++) {
            if (tokens[i].StartsWith('?')) {
                data[i] = null;
            }
            else {
                if (tokens[i].Length != 2)
                    throw new ArgumentException("Token is not 2 characters: " + tokens[i]);
                data[i] = (byte) ((NumberUtils.HexCharToInt(tokens[i][0]) << 4) | NumberUtils.HexCharToInt(tokens[i][1]));
            }
        }

        return new MemoryPattern(data);
    }
}