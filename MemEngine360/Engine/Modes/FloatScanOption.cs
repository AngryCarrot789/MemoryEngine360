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

namespace MemEngine360.Engine.Modes;

public enum FloatScanOption : byte {
    /// <summary>
    /// Truncates decimal places off of the value from the console, according to how many places
    /// there are in the query. Effectively becomes integer comparison if the user specifies no decimal places
    /// </summary>
    TruncateToQuery,
    /// <summary>
    /// Rounds the console value to the number of decimal places in the query. Effectively
    /// becomes integer comparison if the user specifies no decimal places
    /// </summary>
    RoundToQuery
}