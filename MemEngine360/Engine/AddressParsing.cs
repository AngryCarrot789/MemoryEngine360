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

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ILMath;

namespace MemEngine360.Engine;

/// <summary>
/// A helper class for parsing memory addresses
/// </summary>
public static class AddressParsing {
    private static readonly IEvaluationContext<uint> DefaultU32EvalCtx = EvaluationContexts.CreateForInteger<uint>();
    private static readonly IEvaluationContext<ulong> DefaultU64EvalCtx = EvaluationContexts.CreateForInteger<ulong>();

    public static int TrimHexPrefix(string input) {
        int j, i = input.IndexOf("0x", StringComparison.OrdinalIgnoreCase);
        if (i == 0) {
            while ((j = input.IndexOf("0x", i + 2, StringComparison.OrdinalIgnoreCase)) == (i + 2))
                i = j;
            return i + 2;
        }

        return i;
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
    public static bool TryParse(string? input, bool is32bit, out ulong value, [NotNullWhen(false)] out string? error, bool canParseAsExpression = false, bool hexByDefault = true, IFormatProvider? formatProvider = null) {
        if (!string.IsNullOrWhiteSpace(input)) {
            NumberStyles ns;
            int endOfPrefix;
            ReadOnlySpan<char> inputSpan;
            if ((endOfPrefix = TrimHexPrefix(input)) != -1 || hexByDefault) {
                inputSpan = endOfPrefix == -1 ? input.AsSpan() : input.AsSpan(endOfPrefix);
                ns = NumberStyles.HexNumber;
            }
            else {
                inputSpan = input.AsSpan();
                ns = NumberStyles.Integer;
            }
            
            if (is32bit) {
                if (uint.TryParse(inputSpan, ns, formatProvider, out uint u32value)) {
                    error = null;
                    value = u32value;
                    return true;
                }
                else if (ulong.TryParse(inputSpan, ns, formatProvider, out _)) {
                    error = "Value is too large. The maximum is 0xFFFFFFFF";
                    value = 0;
                    return false;
                }
            }
            else if (ulong.TryParse(inputSpan, ns, formatProvider, out ulong u64value)) {
                error = null;
                value = u64value;
                return true;
            }

            if (canParseAsExpression) {
                ParsingContext ctx = new ParsingContext() { DefaultIntegerParseMode = IntegerParseMode.Hexadecimal };
                try {
                    if (is32bit) {
                        Evaluator<uint> expression = MathEvaluation.CompileExpression<uint>("", input, ctx, CompilationMethod.Functional);
                        value = expression(DefaultU32EvalCtx);
                    }
                    else {
                        Evaluator<ulong> expression = MathEvaluation.CompileExpression<ulong>("", input, ctx, CompilationMethod.Functional);
                        value = expression(DefaultU64EvalCtx);
                    }

                    error = null;
                    return true;
                }
                catch {
                    // ignored
                }
            }

            error = $"Invalid {(is32bit ? "32-bit" : "64-bit")} value";
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
    public static bool TryParse32(string? input, out uint value, [NotNullWhen(false)] out string? error, bool canParseAsExpression = false, bool hexByDefault = true, IFormatProvider? formatProvider = null) {
        if (!TryParse(input, true, out ulong u64value, out error, canParseAsExpression, hexByDefault, formatProvider)) {
            value = 0;
            return false;
        }

        Debug.Assert(u64value <= uint.MaxValue);
        value = (uint) u64value;
        return true;
    }

    public static uint Parse32(string input, bool canParseAsExpression = false, bool hexByDefault = true, IFormatProvider? formatProvider = null) {
        if (!TryParse32(input, out uint value, out _, canParseAsExpression, hexByDefault, formatProvider))
            throw new ArgumentException("Invalid 32-bit value");
        return value;
    }
    
    public static ulong Parse64(string input, bool canParseAsExpression = false, bool hexByDefault = true, IFormatProvider? formatProvider = null) {
        if (!TryParse(input, false, out ulong value, out _, canParseAsExpression, hexByDefault, formatProvider))
            throw new ArgumentException("Invalid 64-bit value");
        return value;
    }
}