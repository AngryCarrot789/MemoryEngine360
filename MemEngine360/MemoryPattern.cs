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

using System.Diagnostics.CodeAnalysis;
using PFXToolKitUI.Utils;

namespace MemEngine360;

public readonly struct MemoryPattern {
    internal readonly byte?[] pattern;

    public byte? this[int i] => this.pattern[i];

    /// <summary>
    /// Returns the number of bytes we scan for, including wildcard bytes
    /// </summary>
    public int Length => this.pattern?.Length ?? 0;

    /// <summary>
    /// Returns the number of non-wildcard bytes we scan for. Always equal or less than <see cref="Length"/>
    /// </summary>
    public int NonWildcardLength => this.pattern?.Count(x => x.HasValue) ?? 0;

    /// <summary>
    /// Returns true when this struct contains a valid pattern
    /// </summary>
    public bool IsValid => this.pattern != null;

    /// <summary>
    /// Returns true when this pattern contains any wildcards
    /// </summary>
    public bool HasWildcards => this.pattern != null && this.pattern.Any(x => !x.HasValue);

    private MemoryPattern(byte?[] pattern) {
        this.pattern = pattern;
    }

    public static MemoryPattern Create(byte?[] pattern) {
        ArgumentNullException.ThrowIfNull(pattern);
        return new MemoryPattern((byte?[]) pattern.Clone());
    }

    public static MemoryPattern Create(byte[] pattern) {
        ArgumentNullException.ThrowIfNull(pattern);
        return new MemoryPattern(pattern.Select(x => (byte?) x).ToArray());
    }

    public static void Main() {
        MemoryPattern pattern = MemoryPattern.Compile("45 ? 25 ?? FF");
    }

    private static void CheckChar(string pattern, char ch, int idx) {
        if ((ch < '0' || ch > '9') && (ch < 'a' || ch > 'f'))
            throw new ArgumentException($"Invalid char '{ch}' at idx {idx} in pattern: " + pattern);
    }

    public static MemoryPattern Compile(string input, bool disallowWildcards = false) {
        string? error = null;
        if (!InternalTryCompile(input, out MemoryPattern pattern, disallowWildcards, ref error))
            throw new ArgumentException(error!);

        return pattern;
    }

    public static bool TryCompile(string input, out MemoryPattern pattern, bool disallowWildcards = false) {
        string? msg = ""; // set to empty so we don't waste time creating unused error message
        return InternalTryCompile(input, out pattern, disallowWildcards, ref msg);
    }

    public static bool TryCompile(string input, out MemoryPattern pattern, bool disallowWildcards, [NotNullWhen(false)] out string? errorMessage) {
        string? msg = ""; // set to empty so we don't waste time creating unused error message
        bool result = InternalTryCompile(input, out pattern, disallowWildcards, ref msg);
        errorMessage = result ? null : msg;
        return result;
    }

    private static bool InternalTryCompile(string input, out MemoryPattern pattern, bool disallowWildcards, ref string? errMsg) {
        string[] tokens = input.Split(' ', int.MaxValue, StringSplitOptions.RemoveEmptyEntries);
        byte?[] data = new byte?[tokens.Length];
        for (int i = 0; i < tokens.Length; i++) {
            if (tokens[i].StartsWith('?')) {
                if (disallowWildcards) {
                    errMsg ??= "Wildcards are not allowed";
                    pattern = default;
                    return false;
                }

                data[i] = null;
            }
            else {
                if (tokens[i].Length != 2) {
                    errMsg ??= "Byte token does not contain two characters: " + tokens[i];
                    pattern = default;
                    return false;
                }

                char ch1 = tokens[i][0], ch2 = tokens[i][1], chCheck;
                if (!NumberUtils.IsCharValidHex(chCheck = ch1) || !NumberUtils.IsCharValidHex(chCheck = ch2)) {
                    errMsg ??= $"Invalid hex character '{chCheck}' in token {i + 1}";
                    pattern = default;
                    return false;
                }

                data[i] = (byte) ((NumberUtils.HexCharToInt(ch1) << 4) | NumberUtils.HexCharToInt(ch2));
            }
        }

        pattern = new MemoryPattern(data);
        return true;
    }

    public bool Matches(ReadOnlySpan<byte> buffer) => Matches(this.pattern, buffer);

    public static bool Matches(byte?[] p, ReadOnlySpan<byte> buffer) {
        if (buffer.Length < p.Length)
            return false;

        for (int i = 0; i < p.Length; i++) {
            byte? b = p[i];
            if (b.HasValue && b.Value != buffer[i]) {
                return false;
            }
        }

        return true;
    }
}