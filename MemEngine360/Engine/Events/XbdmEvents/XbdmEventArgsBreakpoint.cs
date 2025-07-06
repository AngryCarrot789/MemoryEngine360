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

public class XbdmEventArgsBreakpoint : XbdmEventArgsNotification {
    /// <summary>
    /// Gets the address we are at
    /// </summary>
    public uint Address { get; init; }

    /// <summary>
    /// Gets the ID of the thread we are on
    /// </summary>
    public uint Thread { get; init; }

    public XbdmEventArgsBreakpoint(string rawMessage, NotificationType notificationType) : base(rawMessage, notificationType) {
    }
}

public class XbdmEventArgsDataBreakpoint : XbdmEventArgsBreakpoint {
    /// <summary>
    /// Gets the type of break type
    /// </summary>
    public BreakType BreakType { get; }

    /// <summary>
    /// Gets the address of the data... I assume?
    /// </summary>
    public uint DataAddress { get; }

    public XbdmEventArgsDataBreakpoint(string rawMessage, BreakType breakType, uint dataAddress) : base(rawMessage, NotificationType.Data) {
        this.BreakType = breakType;
        this.DataAddress = dataAddress;
    }
}

public enum BreakType {
    None,
    Write,
    Read,
    Execute
}