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
using MemEngine360.Engine;
using MemEngine360.XboxInfo;
using PFXToolKitUI.Activities;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Utils;

namespace MemEngine360.Commands;

public class ShowMemoryRegionsCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine engine, CommandEventArgs e) {
        if (engine.Connection != null) {
            return engine.Connection.HasFeature<IFeatureMemoryRegions>() ? Executability.Valid : Executability.Invalid;
        }

        // limitation of commands API -- this is where we have to add/remove buttons dynamically to get around this
        return Executability.ValidButCannotExecute;
    }
    
    protected override DisabledHintInfo? ProvideDisabledHintOverride(MemoryEngine engine, IContextData context, ContextRegistry? sourceContextMenu) {
        if (TryProvideNotConnectedDisabledHintInfo(engine, out DisabledHintInfo? hintInfo))
            return hintInfo;
        if (!engine.Connection!.HasFeature<IFeatureMemoryRegions>())
            return new SimpleDisabledHintInfo("Unsupported", "This connection does not support reading memory regions");
        return null;
    }

    protected override async Task ExecuteCommandAsync(MemoryEngine engine, CommandEventArgs e) {
        if (engine.Connection?.HasFeature<IFeatureMemoryRegions>() == true) {
            Result<Optional<List<MemoryRegion>?>> myResult = await ActivityManager.Instance.RunTask(async () => {
                IActivityProgress p = ActivityTask.Current.Progress;
                p.Caption = "Memory Regions";
                p.Text = "Reading memory regions...";
                p.IsIndeterminate = true;
                
                return await engine.BeginBusyOperationFromActivityAsync(static async (_, c) => {
                    if (!c.TryGetFeature(out IFeatureMemoryRegions? feature)) {
                        await IMessageDialogService.Instance.ShowMessage("Unsupported", "This console does not support memory region querying");
                        return null;
                    }

                    return await feature.GetMemoryRegions(false, false);
                });
            });

            if (myResult.GetValueOrDefault().GetValueOrDefault() is List<MemoryRegion> regions) {
                MemoryRegionUserInputInfo info = new MemoryRegionUserInputInfo(regions) {
                    Caption = "Memory Regions",
                    ConfirmText = "Epic", CancelText = "Close", // UserInputDialog limitation -- cannot disable OK :-)
                    RegionFlagsToTextConverter = MemoryRegionUserInputInfo.ConvertXboxFlagsToText
                };

                await IUserInputDialogService.Instance.ShowInputDialogAsync(info);
            }
        }
    }
}