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

namespace MemEngine360.Xbox360XBDM.StandardEvents;

public class StdEventException : StdEventNotification {
    /// <summary>
    /// Gets the flags associated with the exception
    /// </summary>
    public ExceptionFlags Flags { get; init; }

    public uint Code { get; init; }
    
    /// <summary>
    /// Gets the calling thread
    /// </summary>
    public uint Thread { get; init; }
    
    public uint Address { get; init; }
    
    /// <summary>
    /// Gets the address that was attempted to be read from or written to that caused the exception
    /// </summary>
    public uint ReadOrWriteAddress { get; init; }

    /// <summary>
    /// Gets whether the exception was caused by a write operation
    /// </summary>
    public bool IsOnWrite { get; init; }

    public StdEventException(string rawMessage) : base(rawMessage, NotificationType.Exception) {
        
    }
}

[Flags]
public enum ExceptionFlags {
    None = 0,
    FirstChance = 1,
    NonContinuable = 2
}