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
using System.Text;
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

    public static MemoryPattern Compile(string input, bool allowWildcards = true) {
        string? error = null;
        if (!InternalTryCompile(input, out MemoryPattern pattern, allowWildcards, ref error))
            throw new ArgumentException(error!);

        return pattern;
    }

    public static bool TryCompile(string input, out MemoryPattern pattern, bool allowWildcards = true) {
        string? msg = ""; // set to empty so we don't waste time creating unused error message
        return InternalTryCompile(input, out pattern, allowWildcards, ref msg);
    }

    public static bool TryCompile(string input, out MemoryPattern pattern, bool allowWildcards, [NotNullWhen(false)] out string? userErrorMessage) {
        string? msg = null;
        bool result = InternalTryCompile(input, out pattern, allowWildcards, ref msg);
        userErrorMessage = result ? null : msg;
        return result;
    }

    private static bool InternalTryCompile(string input, out MemoryPattern pattern, bool allowWildcards, ref string? userErrMsg) {
        string[] tokens = input.Split(' ', int.MaxValue, StringSplitOptions.RemoveEmptyEntries);
        byte?[] data = new byte?[tokens.Length];
        for (int i = 0; i < tokens.Length; i++) {
            if (tokens[i].StartsWith('?')) {
                if (!allowWildcards) {
                    userErrMsg ??= "Wildcards are not allowed";
                    pattern = default;
                    return false;
                }

                data[i] = null;
            }
            else {
                if (tokens[i].Length != 2) {
                    if (userErrMsg == null) {
                        StringBuilder sb = new StringBuilder();
                        sb.Append("Byte does not contain two hex characters: ").Append(tokens[i]);
                        if (byte.TryParse(tokens[i], out byte value)) {
                            sb.Append(". Did you mean ").Append(value.ToString("X2")).Append(" (the byte value as hex)?");
                        }

                        userErrMsg = sb.ToString();
                    }
                    
                    pattern = default;
                    return false;
                }

                char ch1 = tokens[i][0], ch2 = tokens[i][1], chCheck;
                if (!NumberUtils.IsCharValidHex(chCheck = ch1) || !NumberUtils.IsCharValidHex(chCheck = ch2)) {
                    userErrMsg ??= $"Invalid hex character '{chCheck}' in token {i + 1}";
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