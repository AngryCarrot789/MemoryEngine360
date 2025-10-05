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

using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Scripting.Commands;

public class RunScriptCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!Script.DataKey.TryGetContext(e.ContextData, out Script? script) || script.Manager == null) {
            return Executability.Invalid;
        }

        return !script.IsRunning
            ? Executability.Valid
            : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!Script.DataKey.TryGetContext(e.ContextData, out Script? script) || script.Manager == null) {
            return;
        }

        if (script.IsRunning) {
            if (e.Shortcut == null)
                await IMessageDialogService.Instance.ShowMessage("Script is running", "The script already running", defaultButton: MessageBoxResult.OK);
            return;
        }

        // IConsoleConnection? connection = script.DedicatedConnection;
        // if (await RunSequenceCommand.HandleConnectionErrors(connection, false)) {
        //     return;
        // }
        
        ScriptViewState.GetInstance(script).RaiseFlushEditorToScript();
        Result result = await script.StartCommand();
        if (result.HasException) {
            await IMessageDialogService.Instance.ShowMessage("Script", "Script compiling failed: " + result.Exception!.Message, defaultButton: MessageBoxResult.OK);
        }
    }

    protected override Task OnAlreadyExecuting(CommandEventArgs args) {
        return args.Shortcut != null 
            ? Task.CompletedTask // do not show already running message when activated on shortcut 
            : base.OnAlreadyExecuting(args);
    }
}