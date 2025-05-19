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
using System.Globalization;

namespace MemEngine360.Xbox360XBDM;

public static class ParamUtils {
    public static void Main() {
        PchGetParam("getmemex coolname=\"my name\" stupid=yaaa joke=\"yes\"", "ddd", true);
    }

    private static bool IsSpace(char ch) => ch == 0 || char.IsWhiteSpace(ch);

    // Bootleg implementation
    public static string? PchGetParam(string input, string key, bool hasCommand) {
        int idx = 0;
        if (input.Length < 1 || key.Length < 1) {
            return null;
        }

        bool fInQuote = false;
        if (hasCommand) {
            while (idx < input.Length && input[idx] != ' ')
                idx++;
            idx++;
        }

        // getmemex coolname="my name" stupid="hell nah" joke="yes"
        //    idx = ^
        int idxName = idx;
        for (; idx < input.Length; idx++) {
            if (input[idx] == '"' && (idx == 0 || input[idx - 1] != '\\')) {
                fInQuote = !fInQuote;
                continue;
            }

            if (!fInQuote && input[idx] == '=') {
                int idxVal = idx + 1, idxValEnd = idxVal;
                if (input[idxValEnd] == '"') {
                    idxVal++;
                    while (++idxValEnd < input.Length && (input[idxValEnd] != '"' || input[idxValEnd - 1] == '\\'))
                        ;
                }
                else {
                    while (idxValEnd < input.Length && !IsSpace(input[idxValEnd]))
                        idxValEnd++;
                }

                string theKey = input.Substring(idxName, idx - idxName);
                string theVal = input.Substring(idxVal, idxValEnd - idxVal);
                if (theKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return theVal;

                if (idxValEnd >= input.Length)
                    return null;

                if (input[idxValEnd] == '"')
                    idxValEnd++;

                idx = idxValEnd;
                while (idx < input.Length && IsSpace(input[idx]))
                    idx++;
                idxName = idx;
            }
        }

        return null;
    }

    public static bool GetStringParam(string input, string key, bool hasCommand, [NotNullWhen(true)] out string? value) {
        return (value = PchGetParam(input, key, hasCommand)) != null;
    }

    public static bool GetDwParam(string cmd, string key, bool hasCmd, out uint value) {
        if (GetStringParam(cmd, key, hasCmd, out string? text) && text.Length > 0) {
            if (text.Length > 2 && text[0] == '0' && text[1] == 'x') {
                return uint.TryParse(text.AsSpan(2), NumberStyles.HexNumber, null, out value);
            }
            
            return uint.TryParse(text, out value);
        }

        value = 0;
        return false;
    }
    
    public static bool GetQwordParam(string cmd, string key, bool hasCmd, out ulong value) {
        if (GetStringParam(cmd, key, hasCmd, out string? text) && text.Length > 0) {
            if (text.Length > 2 && text[0] == '0' && text[1] == 'x') {
                return ulong.TryParse(text.AsSpan(2), NumberStyles.HexNumber, null, out value);
            }
            
            return ulong.TryParse(text, out value);
        }

        value = 0;
        return false;
    }
}