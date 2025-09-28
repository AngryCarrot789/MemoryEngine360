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
using MemEngine360.Connections.Features;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.Engine.FileBrowsing.Commands;

public class CreateAbsoluteDirectoryCommand : BaseFileExplorerCommand {
    protected override Executability CanExecuteCore(FileTreeExplorer explorer, CommandEventArgs e) {
        return explorer.MemoryEngine.Connection?.HasFeature<IFeatureFileSystemInfo>() == true
            ? Executability.Valid
            : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(FileTreeExplorer explorer, CommandEventArgs e) {
        FileTreeExplorerViewState state = FileTreeExplorerViewState.GetInstance(explorer);
        
        IFeatureFileSystemInfo? fsInfo = null;
        string? newPath = null;
        
        ConnectionAction action = new ConnectionAction(IConnectionLockPair.Lambda(explorer.MemoryEngine, x => x.BusyLocker, x => x.Connection)) {
            ActivityCaption = "Launch File",
            CanRetryOnConnectionChanged = true,
            Setup = async (action, connection, hasConnectionChanged) => {
                if (!connection.TryGetFeature(out fsInfo)) {
                    await IMessageDialogService.Instance.ShowMessage("Unsupported", "This connection does not support File I/O");
                    return false;
                }

                if (newPath == null) {
                    SingleUserInputInfo info = new SingleUserInputInfo("Hdd1:\\New Directory") {
                        Caption = "Create Directory",
                        Label = "Directory Path",
                        MinimumDialogWidthHint = 500,
                        Validate = (args) => {
                            if (!fsInfo.IsPathValid(args.Input))
                                args.Errors.Add("Invalid path");
                        }
                    };
                    
                    if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) != true) {
                        return false;
                    }
                    
                    newPath = info.Text;
                }

                return true;
            },
            Execute = async (action, connection) => {
                await fsInfo!.CreateDirectory(newPath!);
                await state.Explorer.SelectFilePath(newPath!, fsInfo!, true);
            }
        };

        await action.RunAsync();
    }
}