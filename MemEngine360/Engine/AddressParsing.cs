using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace MemEngine360.Engine;

/// <summary>
/// A helper class for parsing memory addresses
/// </summary>
public static class AddressParsing {
    // 0x0x0x86000000
    public static ReadOnlySpan<char> TrimHexPrefix(ReadOnlySpan<char> input) {
        int i;
        while ((i = input.IndexOf("0x", StringComparison.OrdinalIgnoreCase)) != -1)
            input = input.Slice(i);
        return input;
    }

    public static ReadOnlySpan<char> TrimHexPrefix(string input) {
        int j, i = input.IndexOf("0x", StringComparison.Ordinal);
        if (i == -1)
            return input.AsSpan();
        
        while ((j = input.IndexOf("0x", i + 2, StringComparison.OrdinalIgnoreCase)) != -1)
            i = j;

        return input.AsSpan(i + 2);
    }


    /// <summary>
    /// Tries to parse a numeric memory address
    /// </summary>
    /// <param name="input">The input value</param>
    /// <param name="is32bit">Gets whether to parse the value as 32 bit</param>
    /// <param name="value">The parsed value</param>
    /// <param name="error">The error string. Non-null when this function fails</param>
    /// <param name="formatProvider">The format provider passed to the integer parse functions</param>
    /// <returns>True if parsed</returns>
    public static bool TryParse(string? input, bool is32bit, out ulong value, [NotNullWhen(false)] out string? error, IFormatProvider? formatProvider = null) {
        if (!string.IsNullOrWhiteSpace(input)) {
            ReadOnlySpan<char> inputSpan = TrimHexPrefix(input);
            if (is32bit) {
                if (uint.TryParse(inputSpan, NumberStyles.HexNumber, formatProvider, out uint u32value)) {
                    error = null;
                    value = u32value;
                    return true;
                }
                else if (ulong.TryParse(inputSpan, NumberStyles.HexNumber, formatProvider, out _)) {
                    error = "Address is too large. The maximum is 0xFFFFFFFF";
                }
                else {
                    error = "Invalid 32-bit address";
                }
            }
            else {
                if (ulong.TryParse(inputSpan, NumberStyles.HexNumber, formatProvider, out ulong u64value)) {
                    error = null;
                    value = u64value;
                    return true;
                }

                error = "Invalid 64-bit address";
            }
        }
        else {
            error = "Input is empty";
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Tries to parse an address as a 32-bit value
    /// </summary>
    /// <param name="input">The input value</param>
    /// <param name="value">The parsed value</param>
    /// <param name="error">The error string. Non-null when this function fails</param>
    /// <param name="formatProvider">The format provider passed to the integer parse functions</param>
    /// <returns>True if parsed</returns>
    public static bool TryParse32(string? input, out uint value, [NotNullWhen(false)] out string? error, IFormatProvider? formatProvider = null) {
        if (!TryParse(input, true, out ulong u64value, out error, formatProvider)) {
            value = 0;
            return false;
        }

        Debug.Assert(u64value <= uint.MaxValue);
        value = (uint) u64value;
        return true;
    }
}