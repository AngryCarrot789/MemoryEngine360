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

namespace MemEngine360.Engine.Modes;

public enum NumericScanType {
    /// <summary>
    /// Match when memory equals query
    /// </summary>
    Equals,
    /// <summary>
    /// Match when memory is not equal to query
    /// </summary>
    NotEquals,
    /// <summary>
    /// Match when memory is less than query
    /// </summary>
    LessThan,
    /// <summary>
    /// Match when memory is less than or equal to query
    /// </summary>
    LessThanOrEquals,
    /// <summary>
    /// Match when memory is greater than query
    /// </summary>
    GreaterThan,
    /// <summary>
    /// Match when memory is greater than or equal to query
    /// </summary>
    GreaterThanOrEquals,
    /// <summary>
    /// Match when memory is greater than or equal to query1 ("from") and
    /// less than or equal to query2 ("to"), therefore, making both query values inclusive
    /// </summary>
    Between,
    /// <summary>
    /// Match when memory is less than query1 ("from") or greater than query2 ("to")
    /// </summary>
    NotBetween
}

public static class NumericScanTypeExtensions {
    public static bool IsBetween(this NumericScanType type) {
        return type == NumericScanType.Between || type == NumericScanType.NotBetween;
    }
}