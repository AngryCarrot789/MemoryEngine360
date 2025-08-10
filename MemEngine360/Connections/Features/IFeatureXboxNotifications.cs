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

using MemEngine360.XboxBase;

namespace MemEngine360.Connections.Features;

/// <summary>
/// A feature for an xbox connection that can show custom notifications
/// </summary>
public interface IFeatureXboxNotifications : IConsoleFeature {
    /// <summary>
    /// Shows a notification on the screen
    /// </summary>
    /// <param name="logo">The logo/notification type</param>
    /// <param name="message">A message to show. Note that not all logos support messages</param>
    /// <returns></returns>
    Task ShowNotification(XNotifyLogo logo, string? message);
}