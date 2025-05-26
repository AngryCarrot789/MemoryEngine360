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

public class StdEventNotification : StdEvent {
    /// <summary>
    /// Gets the type of notification
    /// </summary>
    public NotificationType NotificationType { get; }
    
    public StdEventNotification(string rawMessage, NotificationType notificationType) : base(rawMessage) {
        this.NotificationType = notificationType;
    }
}

public enum NotificationType {
    Break,
    SingleStep,
    Data,
    Exception,
    Rip,
    Assert
}