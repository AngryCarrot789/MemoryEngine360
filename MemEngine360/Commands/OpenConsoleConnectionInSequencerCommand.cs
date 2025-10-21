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

using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Sequencing;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Dialogs;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Commands;

public class OpenConsoleConnectionInSequencerCommand() : Command(allowMultipleExecutions: true) {
    private static readonly DataKey<IOpenConnectionView> ExistingOCVDataKey = DataKeys.Create<IOpenConnectionView>(nameof(OpenConsoleConnectionInSequencerCommand) + "_" + nameof(ExistingOCVDataKey));

    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!TaskSequenceManager.DataKey.TryGetContext(e.ContextData, out TaskSequenceManager? manager))
            return Executability.Invalid;
        if (ExistingOCVDataKey.TryGetContext(manager.UserContext, out IOpenConnectionView? view))
            return Executability.ValidButCannotExecute;
        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!TaskSequenceManager.DataKey.TryGetContext(e.ContextData, out TaskSequenceManager? manager))
            return;
        if (!ITopLevel.TopLevelDataKey.TryGetContext(e.ContextData, out ITopLevel? topLevel))
            return;

        if (ExistingOCVDataKey.TryGetContext(manager.UserContext, out IOpenConnectionView? view)) {
            if (view.DialogOperation is IDesktopDialogOperation<ConnectionResult> op)
                op.Activate();
            return;
        }

        MemoryEngine engine = manager.MemoryEngine;
        ulong frame = engine.GetNextConnectionChangeFrame();
        if (engine.Connection != null && !engine.Connection.IsClosed) {
            MessageBoxResult result = await MessageBoxes.AlreadyConnectedToConsole.ShowMessage();
            if (result != MessageBoxResult.OK) {
                return;
            }

            if (!await OpenConsoleConnectionDialogCommand.DisconnectInActivity(topLevel, engine, frame)) {
                return;
            }
        }

        IOpenConnectionView? dialog = await ApplicationPFX.GetComponent<ConsoleConnectionManager>().ShowOpenConnectionView(OpenConnectionInfo.CreateDefault());
        if (dialog != null) {
            manager.UserContext.Set(ExistingOCVDataKey, dialog);

            IBusyToken? token = null;
            try {
                ConnectionResult result = await dialog.DialogOperation.WaitForResultAsync();
                token = await OpenConsoleConnectionDialogCommand.SetEngineConnectionAndHandleProblemsAsync(manager.MemoryEngine, result.Connection, frame);

                // When returned token is null, close the connection since we can't
                // do anything else with the connection since the user cancelled the operation
                if (token == null) {
                    result.Connection.Close();
                }
            }
            catch (OperationCanceledException) {
                // ignored
            }
            finally {
                token?.Dispose();
                manager.UserContext.Remove(ExistingOCVDataKey);
            }
        }
    }
}