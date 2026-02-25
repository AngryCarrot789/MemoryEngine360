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

using MemEngine360.Commands;
using MemEngine360.Connections;
using MemEngine360.Connections.Features;
using MemEngine360.Engine;
using MemEngine360.Engine.View;
using MemEngine360.Xbox360XBDM.Consoles.Xbdm;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Logging;
using PFXToolKitUI.PropertyEditing.DataTransfer.Enums;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Themes;
using PFXToolKitUI.Utils;
using SkiaSharp;

namespace MemEngine360.Xbox360XBDM.Commands;

public class SendNotificationCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine engine, MemoryEngineViewState engineVs, CommandEventArgs e) {
        IConsoleConnection? connection = engine.Connection;
        if (connection != null && connection.HasFeature<IFeatureXboxNotifications>())
            return Executability.Valid;

        return Executability.ValidButCannotExecute;
    }
    
    protected override async Task ExecuteCommandAsync(MemoryEngineViewState engineVs, MemoryEngine engine, CommandEventArgs e) {
        IConsoleConnection? connection;
        using IBusyToken? token = await engineVs.Engine.BeginBusyOperationUsingActivityAsync();
        if (token == null || (connection = engineVs.Engine.Connection) == null) {
            return;
        }

        if (!connection.TryGetFeature(out IFeatureXboxNotifications? notifications)) {
            await IMessageDialogService.Instance.ShowMessage("Not supported", "This connection does not support showing notifications", defaultButton: MessageBoxResult.OK);
            return;
        }

        DataParameterEnumInfo<XNotifyLogo> dpEnumInfo = DataParameterEnumInfo<XNotifyLogo>.All();
        DoubleUserInputInfo info = new DoubleUserInputInfo("Thank you for using MemoryEngine360 <3", nameof(XNotifyLogo.FLASHING_HAPPY_FACE)) {
            Caption = "Test Notification",
            Message = "Shows a custom notification on your xbox!",
            ValidateA = (b) => {
                if (string.IsNullOrWhiteSpace(b.Input))
                    b.Errors.Add("Input cannot be empty or whitespaces only");
            },
            ValidateB = (b) => {
                if (!dpEnumInfo.TextToEnum.TryGetValue(b.Input, out XNotifyLogo val))
                    b.Errors.Add("Unknown logo type");
            },
            LabelA = "Message",
            LabelB = "Logo (search for XNotifyLogo)"
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) == true) {
            XNotifyLogo logo = dpEnumInfo.TextToEnum[info.TextB];
            await notifications.ShowNotification(logo, info.TextA);
        }
    }
}