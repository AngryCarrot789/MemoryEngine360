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

namespace MemEngine360.Engine.Events.XbdmEvents;

/// <summary>
/// A standard event sent from an xbox 360 via Xbdm
/// </summary>
public class XbdmEventArgs : ConsoleSystemEventArgs {
    /// <summary>
    /// Gets the raw message string the data was parsed from
    /// </summary>
    public string RawMessage { get; }

    public XbdmEventArgs(string rawMessage) {
        this.RawMessage = rawMessage;
    }

    public override string ToString() {
        return $"{this.GetType().Name} ({this.RawMessage})";
    }
}