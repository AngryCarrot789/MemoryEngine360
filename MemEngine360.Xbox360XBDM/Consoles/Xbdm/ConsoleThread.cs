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

namespace MemEngine360.Xbox360XBDM.Consoles.Xbdm;

public struct ConsoleThread {
    public uint id;
    public uint suspendCount;      // "suspend"
    public uint priority;          // "priority"
    public uint tlsBaseAddress;    // "tlsbase"
    public uint baseAddress;       // "base"
    public uint limit;             // "limit"
    public uint slack;             // "slack"
    public ulong creationTime;    // createhi | createlo
    public uint nameAddress;       // "nameaddr";
    public uint nameLength;        // "namelen";
    public uint currentProcessor;  // "proc";
    public uint lastError;         // "proc";
    public string readableName;

    public override string ToString() {
        return $"Thread '{this.readableName}': {{ {nameof(this.id)}: {this.id}, {nameof(this.suspendCount)}: {this.suspendCount}, {nameof(this.priority)}: {this.priority}, {nameof(this.tlsBaseAddress)}: {this.tlsBaseAddress:X8}, {nameof(this.baseAddress)}: {this.baseAddress:X8}, {nameof(this.limit)}: {this.limit:X8}, {nameof(this.slack)}: {this.slack}, {nameof(this.creationTime)}: {this.creationTime}, {nameof(this.nameAddress)}: {this.nameAddress:X8}, {nameof(this.nameLength)}: {this.nameLength}, {nameof(this.currentProcessor)}: {this.currentProcessor}, {nameof(this.lastError)}: {this.lastError} }}";
    }
}