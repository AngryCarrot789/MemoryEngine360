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

namespace MemEngine360.Connections;

/// <summary>
/// A service which manages registered console types and provides information and mechanisms for connecting to consoles
/// </summary>
public abstract class ConsoleConnectionManager {
    private readonly Dictionary<string, RegisteredConnectionType> idToConsoleType;
    private readonly Dictionary<RegisteredConnectionType, string> consoleTypeToId;

    protected ConsoleConnectionManager() {
        this.idToConsoleType = new Dictionary<string, RegisteredConnectionType>();
        this.consoleTypeToId = new Dictionary<RegisteredConnectionType, string>();
    }

    /// <summary>
    /// Gets all of the registered console types
    /// </summary>
    public IEnumerable<RegisteredConnectionType> RegisteredConsoleTypes => this.idToConsoleType.Values;

    public void Register(string id, RegisteredConnectionType type) {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(type);

        if (this.consoleTypeToId.TryGetValue(type, out string? existingId))
            throw new InvalidOperationException($"Same console type instance registered again. Registered id = '{existingId}', new id = '{id}'");
        if (!this.idToConsoleType.TryAdd(id, type))
            throw new InvalidOperationException($"ID already in use '{id}'");

        this.consoleTypeToId[type] = id;
        type.internalRegisteredId = id;
    }

    /// <summary>
    /// Gets a console type from its ID
    /// </summary>
    /// <param name="id">The ID</param>
    /// <param name="type">The found type</param>
    /// <returns>True when found</returns>
    public bool TryGetConsoleType(string id, [NotNullWhen(true)] out RegisteredConnectionType? type) {
        return this.idToConsoleType.TryGetValue(id, out type);
    }

    /// <summary>
    /// Gets the ID a console type was registered with
    /// </summary>
    /// <param name="type">The console type</param>
    /// <param name="id">The found ID</param>
    /// <returns>True when the console type was registered</returns>
    public bool TryGetId(RegisteredConnectionType type, [NotNullWhen(true)] out string? id) {
        return this.consoleTypeToId.TryGetValue(type, out id);
    }

    /// <summary>
    /// Opens a new window for connecting to a console
    /// </summary>
    /// <param name="focusedTypeId">
    ///     The ID of the console type to focus on by default. When null, defaults to the app properties' last connected type
    /// </param>
    /// <returns>The dialog, or null, if there's no windowing system</returns>
    public abstract Task<IOpenConnectionView?> ShowOpenConnectionView(string? focusedTypeId = null);
}