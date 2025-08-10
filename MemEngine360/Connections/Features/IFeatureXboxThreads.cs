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

namespace MemEngine360.Connections.Features;

/// <summary>
/// A feature for xbox 360 based connections that can provide thread information
/// </summary>
public interface IFeatureXboxThreads : IConsoleFeature {
    /// <summary>
    /// Reads the thread info for a thread ID. The returned value's <see cref="XboxThread.id"/> will be 0 if the thread does not exist.
    /// </summary>
    /// <param name="threadId">The ID of the thread to get the info of</param>
    /// <param name="requireName">True to query thread name, otherwise false ot leave null</param>
    /// <returns></returns>
    /// <exception cref="IOException">Response was invalid</exception>
    /// <exception cref="TimeoutException">Timeout while writing/reading</exception>
    Task<XboxThread> GetThreadInfo(uint threadId, bool requireName = true);

    /// <summary>
    /// Gets all the threads running on this console
    /// </summary>
    /// <param name="requireNames">True to query thread names, otherwise false ot leave null</param>
    /// <returns></returns>
    /// <exception cref="IOException">Response was invalid</exception>
    /// <exception cref="TimeoutException">Timeout while writing/reading</exception>
    Task<List<XboxThread>> GetThreadDump(bool requireNames = true);
}