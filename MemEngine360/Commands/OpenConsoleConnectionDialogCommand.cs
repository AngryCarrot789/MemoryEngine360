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

using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Engine.View;
using PFXToolKitUI;
using PFXToolKitUI.Activities;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Dialogs;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Commands;

public class OpenConsoleConnectionDialogCommand : Command {
    private static readonly DataKey<IOpenConnectionView> ExistingOCVDataKey = DataKeys.Create<IOpenConnectionView>(nameof(OpenConsoleConnectionDialogCommand) + "_" + nameof(ExistingOCVDataKey));


    public const string AlreadyOpenDialogName = "dialog.AlreadyConnectedToConsole";

    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!MemoryEngineViewState.DataKey.TryGetContext(e.ContextData, out MemoryEngineViewState? engineVs))
            return Executability.Invalid;
        if (ExistingOCVDataKey.TryGetContext(engineVs.Engine.UserContext, out IOpenConnectionView? view))
            return Executability.ValidButCannotExecute;

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngineViewState.DataKey.TryGetContext(e.ContextData, out MemoryEngineViewState? engineVs))
            return;
        if (!ITopLevel.TopLevelDataKey.TryGetContext(e.ContextData, out ITopLevel? topLevel))
            return;

        if (ExistingOCVDataKey.TryGetContext(engineVs.Engine.UserContext, out IOpenConnectionView? view)) {
            if (view.DialogOperation is IDesktopDialogOperation<ConnectionResult> op)
                op.Activate();
            return;
        }

        ulong frame = engineVs.Engine.GetNextConnectionChangeFrame();

        if (engineVs.Engine.Connection != null && !engineVs.Engine.Connection.IsClosed) {
            MessageBoxResult result = await MessageBoxes.AlreadyConnectedToConsole.ShowMessage();
            if (result != MessageBoxResult.OK) {
                return;
            }

            if (!await DisconnectInActivity(topLevel, engineVs.Engine, frame)) {
                return;
            }
        }

        UserConnectionInfo? lastInfo = engineVs.Engine.LastUserConnectionInfo;
        string focusedTypeId = lastInfo != null
            ? lastInfo.ConnectionType.RegisteredId
            : BasicApplicationConfiguration.Instance.LastConnectionTypeUsed;

        OpenConnectionInfo info = OpenConnectionInfo.CreateDefault(focusedTypeId);
        IOpenConnectionView? dialog = await ApplicationPFX.GetComponent<ConsoleConnectionManager>().ShowOpenConnectionView(info);
        if (dialog != null) {
            if (lastInfo != null && info.TryGetEntryForType(lastInfo.ConnectionType, out ConnectionTypeEntry? entry)) {
                entry.Info = lastInfo;
                info.SelectedConnectionType = entry;
            }

            IBusyToken? token = null;
            try {
                engineVs.Engine.UserContext.Set(ExistingOCVDataKey, dialog);
                ConnectionResult? result = await dialog.WaitForConnection();
                if (result.HasValue) {
                    // When returned token is null, close the connection since we can't
                    // do anything else with the connection since the user cancelled the operation
                    if ((token = await SetEngineConnectionAndHandleProblemsAsync(engineVs.Engine, result.Value.Connection, frame, result.Value.Info)) == null) {
                        result.Value.Connection.Close();
                    }
                }
            }
            finally {
                token?.Dispose();
                engineVs.Engine.UserContext.Remove(ExistingOCVDataKey);
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="engine"></param>
    /// <param name="frame"></param>
    /// <returns>False when token could not be acquired</returns>
    public static async Task<bool> DisconnectInActivity(ITopLevel engineTopLevel, MemoryEngine engine, ulong frame) {
        using CancellationTokenSource cts = new CancellationTokenSource();
        Result<bool> result = await ActivityManager.Instance.RunTask(async () => {
            ActivityTask task = ActivityTask.Current;
            task.Progress.Caption = "Disconnect from connection";
            task.Progress.Text = "Stopping all tasks...";

            // ConnectionAboutToChange can be called at any time even if the connection isn't
            // about to change. It's purely just to signal tasks to stop
            await engine.BroadcastConnectionAboutToChange(engineTopLevel, frame);

            using IBusyToken? token = await engine.BusyLock.BeginBusyOperationFromActivity(CancellationToken.None);
            if (token == null) {
                return false;
            }

            // Doesn't matter if the connection became null in the meantime
            IConsoleConnection? existingConnection = engine.Connection;
            if (existingConnection != null) {
                await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                    if ((existingConnection = engine.Connection) != null) {
                        engine.SetConnection(token, frame, null, ConnectionChangeCause.User);
                    }
                }, token: CancellationToken.None);

                try {
                    existingConnection.Close();
                }
                catch (Exception) {
                    // ignored
                }
            }

            return true;
        }, cts);

        return result.GetValueOrDefault();
    }

    /// <summary>
    /// Acquires the busy token, and attempts to set the connection to the given value (on the given frame).
    /// If a connection already exists, we ask the user if they want to overwrite the existing one.
    /// If they say yes, we close the existing and use the new one. Otherwise, we close the new one and nothing else happens.
    /// <para>
    /// If the busy token could not be acquired, we do nothing (the new connection remains open)
    /// </para>
    /// </summary>
    /// <param name="engine">The memory engine</param>
    /// <param name="newConnection">The new connection</param>
    /// <param name="frame">The connection changing frame</param>
    /// <returns>The token</returns>
    public static async Task<IBusyToken?> SetEngineConnectionAndHandleProblemsAsync(MemoryEngine engine, IConsoleConnection newConnection, ulong frame, UserConnectionInfo? userConnectionInfo = null) {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(newConnection);

        IBusyToken? token = await engine.BeginBusyOperationUsingActivityAsync("Change connection");
        if (token == null) {
            return null;
        }

        IConsoleConnection? oldConnection = engine.Connection;
        if (ReferenceEquals(oldConnection, newConnection)) {
            return token;
        }

        if (oldConnection != null && !oldConnection.IsClosed) {
            // Somehow a connection was set before we got here and user doesn't want to overwrite it.
            // Maybe they opened two windows for some reason? Perhaps via the task sequencer and main window.

            MessageBoxResult result = await MessageBoxes.AlreadyConnectedToConsole.ShowMessage();
            if (result != MessageBoxResult.OK) {
                try {
                    newConnection.Close();
                }
                catch (Exception) {
                    // ignored
                }

                return token;
            }
        }

        engine.SetConnection(token, frame, newConnection, ConnectionChangeCause.User, userConnectionInfo);
        if (oldConnection != null) {
            // Always close AFTER changing, just in case a listener wants to send data or whatever
            try {
                oldConnection.Close();
            }
            catch (Exception) {
                // ignored
            }
        }

        return token;
    }
}