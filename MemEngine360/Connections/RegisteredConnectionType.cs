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

using System.Net;
using System.Runtime.CompilerServices;
using PFXToolKitUI.Activities;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Connections;

/// <summary>
/// Provides information about a specific type of connection to a console. For example, XBDM commands
/// and XDevkit COM objects are two different connection types (despite the fact XDevkit uses XBDM commands under the hood)
/// </summary>
public abstract class RegisteredConnectionType {
    internal string? internalRegisteredId;

    /// <summary>
    /// Gets a readable string which is the name of this console type, e.g. "Xbox 360 (XBDM)".
    /// This is typically shown in the list box of possible consoles to connect to.
    /// <para>
    /// Ideally, this should never change. It should basically be a read only property
    /// </para>
    /// </summary>
    public abstract string DisplayName { get; }

    /// <summary>
    /// Gets the text to display below the <see cref="DisplayName"/> in greyed out text.
    /// This can be used to indicate a version, a warning or more.  
    /// </summary>
    public virtual string? FooterText => null;

    /// <summary>
    /// Gets a string containing a readable description of this console type. E.g. for an
    /// Xbox 360 XBDM connection, it may say "This connects to the Xbox 360 via TCP to xbdm on port 730".
    /// This is typically shown has the header.
    /// <para>
    /// Ideally, this should never change. It should basically be a read only property
    /// </para> 
    /// </summary>
    public abstract string LongDescription { get; }

    /// <summary>
    /// Gets the ID this console type was registered with. Same as passing the current instance to <see cref="ConsoleConnectionManager.TryGetId"/>
    /// </summary>
    public string RegisteredId => this.internalRegisteredId ?? throw new InvalidOperationException("This console type hasn't been registered yet");

    /// <summary>
    /// Gets the icon that represents this console type. Null means no icon (duh)
    /// </summary>
    public virtual Icon? Icon => null;

    /// <summary>
    /// Returns true when connections returned by <see cref="OpenConnection"/> implement <see cref="MemEngine360.Connections.Features.IFeatureSystemEvents"/>
    /// </summary>
    public virtual bool SupportsEvents => false;

    // TODO: Coming soon, connection limiting. Xbox has no limit AFAIK, so no need to implement it yet
    // /// <summary>
    // /// Gets whether this connection type 
    // /// </summary>
    // public virtual bool HasConnectionLimit => false;

    protected RegisteredConnectionType() {
    }

    /// <summary>
    /// The main procedure for connecting to the console. How this is done is completely up to the implementation; it may
    /// show a dialog requesting an IP address, it might show a dialog with list of local devices the user can select,
    /// or it may just simply connect to a hardcoded address in the background.
    /// <para>
    /// This procedure should ideally create an activity task via <see cref="ActivityManager"/> or some
    /// sort of notification window to show the user what's going on and maybe give them the option to cancel the operation
    /// (and return null from this method).
    /// </para>
    /// <para>
    /// This function should ideally be called from within a command or <see cref="CommandManager.RunActionAsync"/>,
    /// so that information such as caller window is available to the implementor (to show message dialogs for errors).
    /// </para>
    /// </summary>
    /// <param name="_info">
    ///     Information that the user specified in the connection dialog. Value is null when <see cref="CreateConnectionInfo"/> returns null
    /// </param>
    /// <param name="additionalContext">
    ///     Additional context about how someone is attempting to open a connection. For example, this might contain
    ///     a flag to change the behaviour of what this function does (e.g. do not show a "connecting..." dialog)
    /// </param>
    /// <param name="cancellation">
    ///     A reference to the CTS created by the Connect to console dialog. It is cancelled when either the user clicks the cancel
    ///     button in the activity status bar (or list) or when the user closes the connect to console window
    /// </param>
    /// <returns>
    /// The valid console connection, or null if a connection could not be made, or cancellation was requested
    /// </returns>
    public abstract Task<IConsoleConnection?> OpenConnection(UserConnectionInfo? _info, IContextData additionalContext, CancellationTokenSource cancellation);

    /// <summary>
    /// Creates an instance of <see cref="UserConnectionInfo"/> which is used by the front end to
    /// create a control to show in the connection dialog. The user may input specific details they
    /// wish to pass to <see cref="OpenConnection"/>.
    /// <para>
    /// If a custom control is not desired, and you instead wish to just show a dialog for simplicity
    /// sakes, then return null, and no control will be created
    /// </para>
    /// </summary>
    /// <returns>The info to pass to <see cref="OpenConnection"/>, or null if a custom control is not wanted</returns>
    public virtual UserConnectionInfo? CreateConnectionInfo() {
        return null;
    }

    /// <summary>
    /// Gets text to display on the bottom-right side of the status bar. For XBDM, this returns the IP address we're connected to.
    /// The given connection's <see cref="IConsoleConnection.ConnectionType"/> must equal the current connection type instance
    /// </summary>
    /// <param name="connection">The connection</param>
    /// <returns>The text</returns>
    public string GetStatusBarText(IConsoleConnection connection) {
        ArgumentNullException.ThrowIfNull(connection);
        if (!ReferenceEquals(connection.ConnectionType, this)) {
            throw new InvalidOperationException("Invalid connection type");
        }

        return this.GetStatusBarTextCore(connection);
    }

    /// <summary>
    /// Invoked by <see cref="GetStatusBarText"/>. The connection is guaranteed to have been created by this connection type.
    /// </summary>
    /// <param name="connection">The connection</param>
    /// <returns>The text</returns>
    protected virtual string GetStatusBarTextCore(IConsoleConnection connection) {
        if (connection is INetworkConsoleConnection networked) {
            EndPoint? endPoint = networked.EndPoint;
            switch (endPoint) {
                case null:          return "No end point";
                case IPEndPoint ip: return ip.Address.MapToIPv4().ToString();
                default:            return endPoint.ToString()!;
            }
        }
        else {
            return "Connected";
        }
    }

    /// <summary>
    /// Provides a collection of context objects to be added to the "Remote Commands" menu in the
    /// memory engine UI, e.g. for xbox, these could be shutdown, open disk tray, etc.
    /// <para>
    /// Separators (<see cref="SeparatorEntry"/>) and Captions (<see cref="CaptionEntry"/>) are supported
    /// </para>
    /// </summary>
    /// <returns></returns>
    public virtual IEnumerable<IContextObject> GetRemoteContextOptions() {
        yield break;
    }

    // ConsoleConnectionManager uses the console type instance as a key to map
    // back to id, therefore, we have to ensure equality comparison is reference.
    // 
    // And besides there's no real reason to implement actual comparison or even
    // have multiple instances of the same console type

    public sealed override bool Equals(object? obj) => ReferenceEquals(this, obj);

    public sealed override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}