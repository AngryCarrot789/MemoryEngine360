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
using System.Globalization;
using System.Text;

namespace MemEngine360.Xbox360XBDM;

public static class ParamUtils {
    public static bool GetStrParam(string text, string key, bool hasNoCommandPrefix, [NotNullWhen(true)] out string? value, int initialCapacity = 64) {
        return GetStrParam(text.AsSpan(), key, hasNoCommandPrefix, out value, initialCapacity);
    }

    public static bool GetDwParam(string text, string key, bool hasNoCommandPrefix, out uint value) {
        return GetDwParam(text.AsSpan(), key, hasNoCommandPrefix, out value);
    }

    public static bool GetQwParam(string text, string key, bool hasNoCommandPrefix, out ulong value) {
        return GetQwParam(text.AsSpan(), key, hasNoCommandPrefix, out value);
    }

    public static bool GetStrParam(ReadOnlySpan<char> text, string key, bool hasNoCommandPrefix, [NotNullWhen(true)] out string? value, int initialCapacity = 64) {
        int offset = GetOffsetToValue(text, key, true, hasNoCommandPrefix);
        return (value = offset >= 0 ? GetValueAt(text, offset, initialCapacity) : null) != null;
    }

    public static bool GetDwParam(ReadOnlySpan<char> text, string key, bool hasNoCommandPrefix, out uint value) {
        int offset = GetOffsetToValue(text, key, true, hasNoCommandPrefix);
        if (offset >= 0) {
            string? txt = GetValueAt(text, offset, 13, 13); // uint cannot be expressed with more than 13 chars as hex/int 
            NumberStyles ns = txt != null && txt.StartsWith("0x") ? NumberStyles.HexNumber : NumberStyles.Integer;
            return uint.TryParse(txt.AsSpan(ns == NumberStyles.HexNumber ? 2 : 0), ns, null, out value);
        }

        value = 0;
        return false;
    }

    public static bool GetQwParam(ReadOnlySpan<char> text, string key, bool hasNoCommandPrefix, out ulong value) {
        int offset = GetOffsetToValue(text, key, true, hasNoCommandPrefix);
        if (offset >= 0) {
            string? txt = GetValueAt(text, offset, 24, 24); // ulong cannot be expressed with more than 24 chars as hex/int
            NumberStyles ns = txt != null && (txt.StartsWith("0q") || txt.StartsWith("0x")) ? NumberStyles.HexNumber : NumberStyles.Integer;
            return ulong.TryParse(txt.AsSpan(ns == NumberStyles.HexNumber ? 2 : 0), ns, null, out value);
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Returns the index to either the first char of the value (when <see cref="isValueRequired"/> is true) including the quote,
    /// or the index of where the equal sign would be. Returns -1 when the key is not found
    /// </summary>
    /// <param name="text">The input command text</param>
    /// <param name="key">The key to search for</param>
    /// <param name="isValueRequired">True when a value is required, as in, an equals sign must follow the key within <see cref="text"/></param>
    /// <param name="hasNoCommandPrefix">True when <see cref="text"/> does not contain a command name. When false, we skip until a whitespace is encountered</param>
    /// <returns></returns>
    public static int GetOffsetToValue(ReadOnlySpan<char> text, string key, bool isValueRequired, bool hasNoCommandPrefix) {
        ReadOnlySpan<char> keyAsSpan = key.AsSpan(); // saves constantly allocating ROS, although it is cheap to alloc...
        bool isInQuote = false;
        int i = 0, j, offset;
        if (!hasNoCommandPrefix)
            while (i < text.Length && !IsSpaceOrEOL(text[i]))
                i++;

        while (i < text.Length) {
            // Skip excessive whitespaces, fail on end of string
            while (i < text.Length && IsSpaceOrEOL(text[i]))
                i++;
            if (i == text.Length)
                return -1;

            // Search for equals sign (beginning of value) and check if the key matches. Even if
            // we don't need a value (isValueRequired == false), the key is still present so we return
            for (j = 0; (offset = i + j) < text.Length && !IsSpaceOrEOL(text[offset]); j++) {
                if (text[offset] == '=') {
                    if (MatchRegion(keyAsSpan, text, j, i))
                        return offset + 1; // Skip '='
                    break;
                }
            }

            // When no value is required and no equal sign is present, try to match key
            if (!isValueRequired && offset < text.Length && text[offset] != '=' && MatchRegion(keyAsSpan, text, j, i)) {
                return offset;
            }

            // No match found, so skip past value, if any
            for (i = offset; i < text.Length && (!IsSpaceOrEOL(text[i]) || isInQuote); i++)
                if (text[i] == '"')
                    isInQuote = !isInQuote;
        }

        return -1;
    }

    /// <summary>
    /// Gets the value present at the offset within the text until a whitespace is encountered
    /// outside a quoted region, or we reach the end of a string
    /// </summary>
    /// <param name="text">The source text</param>
    /// <param name="offset">Offset within text</param>
    /// <param name="initialCapacity">Initial capacity of the character buffer to append the value chars to</param>
    /// <param name="maxChars">Optionally the maximum numbers of chars allowed. When reached, we return null</param>
    /// <returns>The value, or null, if the max chars were reached</returns>
    public static string? GetValueAt(ReadOnlySpan<char> text, int offset, int initialCapacity = 64, int maxChars = -1) {
        bool isInQuote = false;
        StringBuilder sb = new StringBuilder(maxChars < 0 ? initialCapacity : Math.Min(initialCapacity, maxChars));
        while (offset < text.Length && (!IsSpaceOrEOL(text[offset]) || isInQuote)) {
            if (text[offset] == '"') {
                if (isInQuote && offset != (text.Length - 1 /* check not last char */) && text[offset + 1] == '"') {
                    if (maxChars >= 0 && sb.Length == maxChars)
                        return null;

                    sb.Append('"');
                    offset += 2;
                }
                else {
                    isInQuote = !isInQuote;
                    ++offset;
                }
            }
            else {
                if (maxChars >= 0 && sb.Length == maxChars)
                    return null;

                sb.Append(text[offset++]);
            }
        }

        return sb.ToString();
    }

    private static bool MatchRegion(ReadOnlySpan<char> str1, ReadOnlySpan<char> str2, int count, int offset) {
        int i = 0;
        while (count-- != 0 && i < str1.Length) {
            char ch1 = char.ToLower(str1[i++]);
            char ch2 = char.ToLower(str2[offset++]);
            if (ch1 != ch2)
                return false;
        }

        return count < 0 && i >= str1.Length;
    }

    private static bool IsSpaceOrEOL(char ch) => ch == ' ' || ch == '\r' || ch == 0;
}