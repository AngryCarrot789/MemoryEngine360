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

using System.Text;

namespace MemEngine360.Engine.Modes;

public enum StringType {
    /// <summary>
    /// ASCII chars, basically the same as UTF8, one byte per character
    /// </summary>
    ASCII,
    /// <summary>
    /// UTF8 chars, basically the same as ASCII, one byte per character
    /// </summary>
    UTF8,
    /// <summary>
    /// Unicode chars, two bytes per character
    /// </summary>
    UTF16,
    /// <summary>
    /// UTF32 chars, four bytes per character
    /// </summary>
    UTF32
}

public static class StringTypeExtensions {
    public static Encoding ToEncoding(this StringType stringType) {
        switch (stringType) {
            case StringType.ASCII: return Encoding.ASCII;
            case StringType.UTF8:  return Encoding.UTF8;
            case StringType.UTF16: return Encoding.Unicode;
            case StringType.UTF32: return Encoding.UTF32;
            default:               throw new ArgumentOutOfRangeException(nameof(stringType), stringType, null);
        }
    }
    
    public static uint GetByteCount(this StringType stringType, string value) {
        return (uint) stringType.ToEncoding().GetByteCount(value);
    }
}