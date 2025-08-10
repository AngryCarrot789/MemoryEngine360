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
/// A feature for connections that support freezing and unfreezing the console
/// </summary>
public interface IFeatureIceCubes : IConsoleFeature {
    /// <summary>
    /// Signals the console to completely freeze
    /// </summary>
    /// <returns></returns>
    Task<FreezeResult> DebugFreeze();

    /// <summary>
    /// Signals the console to unfreeze/resume
    /// </summary>
    /// <returns></returns>
    Task<UnFreezeResult> DebugUnFreeze();

    /// <summary>
    /// Returns true or false when the console is currently frozen. Returns null when the state cannot be determined (e.g. unsupported)
    /// </summary>
    /// <returns></returns>
    Task<bool?> IsFrozen();
}

public enum FreezeResult : byte {
    Success,
    AlreadyFrozen,
}

public enum UnFreezeResult : byte {
    Success,
    AlreadyUnfrozen
}