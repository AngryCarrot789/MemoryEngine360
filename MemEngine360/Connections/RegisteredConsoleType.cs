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

using System.Runtime.CompilerServices;
using MemEngine360.Engine;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Icons;

namespace MemEngine360.Connections;

/// <summary>
/// Represents a registered connection type
/// </summary>
public abstract class RegisteredConsoleType {
    internal string? internalRegisteredId;
    
    // TODO: allow custom controls to be presented in the MemEngine connection dialog.
    // Maybe we can use a similar system to the UserInputInfo, where you register a
    // control type with a model class, and we add a method in here called "CreateConsoleUserInputInfo()"
    
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
    public virtual Icon? Icon { get; }

    /// <summary>
    /// The main procedure for connecting to the console. How this is done is completely up to the implementation; it may
    /// show a dialog requesting an IP address, it might show a dialog with list of local devices the user can select,
    /// or it may just simply connect to a hardcoded address in the background.
    /// <para>
    /// This procedure should ideally create an activity task via <see cref="PFXToolKitUI.Tasks.ActivityManager"/> or some
    /// sort of notification window to show the user what's going on and maybe give them the option to cancel the operation
    /// (and return null from this method).
    /// </para> 
    /// </summary>
    /// <param name="engine">
    ///     The engine trying to connect. Do not call <see cref="MemoryEngine360.SetConnection"/>, since it
    ///     will be done once this async method completes, if it returns a non-null value
    /// </param>
    /// <param name="_info">
    ///     Information that the user specified in the connection dialog. Value is null when <see cref="CreateConnectionInfo"/> is null
    /// </param>
    /// <param name="cancellation">
    /// A reference to the CTS created by the Connect to console dialog. It is cancelled when either the user clicks the cancel
    /// button in the activity status bar (or list) or when the user closes the connect to console window
    /// </param>
    /// <returns>
    /// The valid console connection, or null if a connection could not be made, or cancellation was requested
    /// </returns>
    public abstract Task<IConsoleConnection?> OpenConnection(MemoryEngine360 engine, UserConnectionInfo? _info, CancellationTokenSource cancellation);

    /// <summary>
    /// Creates an instance of <see cref="UserConnectionInfo"/> which is used by the front end to
    /// create a control to show in the connection dialog. The user may input specific details they
    /// wish to pass to <see cref="OpenConnection"/>.
    /// <para>
    /// If a custom control is not desired, and you instead wish to just show a dialog for simplicity
    /// sakes, then return null, and no control will be created
    /// </para>
    /// </summary>
    /// <param name="engine"></param>
    /// <returns>The info to pass to <see cref="OpenConnection"/>, or null if a custom control is not wanted</returns>
    public virtual UserConnectionInfo? CreateConnectionInfo(MemoryEngine360 engine) {
        return null;
    }

    // ConsoleConnectionManager uses the console type instance as a key to map
    // back to id, therefore, we have to ensure equality comparison is reference.
    // 
    // And besides there's no real reason to implement actual comparison or even
    // have multiple instances of the same console type
    
    public sealed override bool Equals(object? obj) {
        return ReferenceEquals(this, obj);
    }

    public sealed override int GetHashCode() {
        return RuntimeHelpers.GetHashCode(this);
    }

    /// <summary>
    /// Provides a collection of context objects to be added to the "Remote Commands" menu in the
    /// memory engine UI, e.g. for xbox, these could be shutdown, open disk tray, etc.
    /// </summary>
    /// <returns></returns>
    public virtual IEnumerable<IContextObject> GetRemoteContextOptions() {
        yield break;
    }
}