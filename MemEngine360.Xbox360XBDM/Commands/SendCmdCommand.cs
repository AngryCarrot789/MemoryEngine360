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

using MemEngine360.Commands;
using MemEngine360.Engine;
using MemEngine360.Xbox360XBDM.Consoles.Xbdm;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.Xbox360XBDM.Commands;

public class SendCmdCommand : BaseMemoryEngineCommand {
    private string? lastCommand;

    protected override Executability CanExecuteCore(MemoryEngine360 engine, CommandEventArgs e) {
        if (engine.Connection == null)
            return Executability.ValidButCannotExecute;

        return engine.Connection is XbdmConsoleConnection ? Executability.Valid : Executability.Invalid;
    }

    protected override async Task ExecuteCommandAsync(MemoryEngine360 engine, CommandEventArgs e) {
        if (engine.Connection == null || !(engine.Connection is XbdmConsoleConnection xbdm)) {
            return;
        }

        SingleUserInputInfo info = new SingleUserInputInfo("Send command", "Specify command to send", this.lastCommand) {
            Footer = $"Please be careful what you send. Binary responses (e.g. GETMEMEX) cannot be handled.",
            Validate = (b) => {
                if (string.IsNullOrWhiteSpace(b.Input))
                    b.Errors.Add("Command text cannot be an empty string");
            }
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) == true) {
            using IDisposable? token = await engine.BeginBusyOperationActivityAsync("Sending command");
            if (token != null) {
                // XBDM appends \r\n for us so we remove it
                string text = info.Text;
                while (text.EndsWith("\r\n")) {
                    text = text.Substring(0, text.Length - 2);
                }

                info.Text = text; // allow lastCommand to have \r\n removed
                ConsoleResponse command = await xbdm.SendCommand(text);
                int crt = (int) command.ResponseType;

                switch (command.ResponseType) {
                    case ResponseType.SingleResponse:
                        if (command.Message.EndsWith("Follows", StringComparison.OrdinalIgnoreCase)) {
                            await IMessageDialogService.Instance.ShowMessage("Single response with lines", command.Message, string.Join(Environment.NewLine, await xbdm.ReadMultiLineResponse()));
                        }
                        else {
                            await IMessageDialogService.Instance.ShowMessage($"Single response ({crt})", command.Message);
                        }

                        break;
                    case ResponseType.Connected:                     await IMessageDialogService.Instance.ShowMessage($"Connected ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.MultiResponse:                 await IMessageDialogService.Instance.ShowMessage("Multi-Response", string.Join(Environment.NewLine, await xbdm.ReadMultiLineResponse()), defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.BinaryResponse:                await IMessageDialogService.Instance.ShowMessage($"Binary Response ({crt})", "(Cannot received data! Command line may now be broken)", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.ReadyForBinary:                await IMessageDialogService.Instance.ShowMessage($"Ready For Binary ({crt})", "(Cannot received data! Command line may now be broken)", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.DedicatedConnection:           await IMessageDialogService.Instance.ShowMessage($"Dedicated Connection ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.NoError:                       await IMessageDialogService.Instance.ShowMessage($"No Error ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.MaxConnectionsExceeded:        await IMessageDialogService.Instance.ShowMessage($"Max Connections Exceeded ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.FileNotFound:                  await IMessageDialogService.Instance.ShowMessage($"File Not Found ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.NoSuchModule:                  await IMessageDialogService.Instance.ShowMessage($"No Such Module ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.MemoryNotMapped:               await IMessageDialogService.Instance.ShowMessage($"Memory Not Mapped ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.NoSuchThread:                  await IMessageDialogService.Instance.ShowMessage($"No Such Thread ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.ClockNotSet:                   await IMessageDialogService.Instance.ShowMessage($"Clock Not Set ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.UnknownCommand:                await IMessageDialogService.Instance.ShowMessage($"Unknown Command ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.NotStopped:                    await IMessageDialogService.Instance.ShowMessage($"Not Stopped ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.FileMustBeCopied:              await IMessageDialogService.Instance.ShowMessage($"File Must Be Copied ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.FileAlreadyExists:             await IMessageDialogService.Instance.ShowMessage($"File Already Exists ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.DirectoryNotEmpty:             await IMessageDialogService.Instance.ShowMessage($"Directory Not Empty ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.BadFileName:                   await IMessageDialogService.Instance.ShowMessage($"Bad File Name ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.FileCannotBeCreated:           await IMessageDialogService.Instance.ShowMessage($"File Cannot Be Created ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.AccessDenied:                  await IMessageDialogService.Instance.ShowMessage($"Access Denied ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.DeviceIsFull:                  await IMessageDialogService.Instance.ShowMessage($"Device Is Full ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.NotDebuggable:                 await IMessageDialogService.Instance.ShowMessage($"Not Debuggable ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.CountTypeInvalid:              await IMessageDialogService.Instance.ShowMessage($"Count Type Invalid ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.CountNotAvailable:             await IMessageDialogService.Instance.ShowMessage($"Count Not Available ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.BoxIsNotLocked:                await IMessageDialogService.Instance.ShowMessage($"Box Is Not Locked ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.KeyExchangeRequired:           await IMessageDialogService.Instance.ShowMessage($"Key Exchange Required ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.DedicatedConnectionRequired:   await IMessageDialogService.Instance.ShowMessage($"Dedicated Connection Required ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.InvalidArgument:               await IMessageDialogService.Instance.ShowMessage($"Invalid Argument ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.ProfileNotStarted:             await IMessageDialogService.Instance.ShowMessage($"Profile NotStarted ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.ProfileAlreadyStarted:         await IMessageDialogService.Instance.ShowMessage($"Profile Already Started ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.D3DDebugCommandNotImplemented: await IMessageDialogService.Instance.ShowMessage($"D3D Debug Command Not Implemented ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.D3DInvalidSurface:             await IMessageDialogService.Instance.ShowMessage($"D3D Invalid Surface ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.VxTaskPending:                 await IMessageDialogService.Instance.ShowMessage($"Vx Task Pending ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    case ResponseType.VxTooManySessions:             await IMessageDialogService.Instance.ShowMessage($"Vx Too Many Sessions ({crt})", command.Message, defaultButton:MessageBoxResult.OK); break;
                    default:                                         throw new ArgumentOutOfRangeException();
                }
            }
        }

        // save last command no matter what
        this.lastCommand = info.Text;
    }
}