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
/// A feature associated with a <see cref="IConsoleConnection"/>. Interfaces implementing this should
/// be prefixed with "IFeature" with the suffix being whatever the feature provides.
/// <para>
/// The feature API solves issues with inheritance-based features (previously called traits), because
/// certain underlying features available to a connection may not actually work under all circumstances.
/// </para>
/// <para>
/// For example, showing a custom notification via XBDM. This can technically be done through the <c>consolefeatures</c> XBDM command,
/// however, it only works if JRPC2 is installed. Therefore, this feature (specifically <see cref="IFeatureXboxNotifications"/>) is only
/// available when the connection determines it to be so.
/// </para>
/// <para>
/// Ideally, features cannot be dynamically added or removed from a connection, but this
/// is up to how the connection implements <see cref="IConsoleConnection.TryGetFeature{T}"/>
/// </para>
/// </summary>
public interface IConsoleFeature {
    /// <summary>
    /// Gets the connection associated with this feature
    /// </summary>
    IConsoleConnection Connection { get; }
}