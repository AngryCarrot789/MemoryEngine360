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

namespace MemEngine360.Connections;

/// <summary>
/// A trait for a <see cref="IConsoleConnection"/> which can provide memory regions
/// </summary>
public interface IHaveMemoryRegions {
    /// <summary>
    /// Walks all the memory regions on the console. Optionally you can provide flags on
    /// intention in order to filter out regions that will not allow such operations (e.g.
    /// if you intend to write, then read-only regions will not be provided).
    /// Set all intentions as false to just get all regions no matter what
    /// </summary>
    /// <param name="willRead">We intend to read data from this region</param>
    /// <param name="willWrite">We intend to write data to this region</param>
    /// <returns>A task containing a list of all memory regions (that fit the intentions)</returns>
    Task<List<MemoryRegion>> GetMemoryRegions(bool willRead, bool willWrite);
}