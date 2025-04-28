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

using System.Net.Sockets;
using MemEngine360.Configs;
using MemEngine360.Connections.XBOX;
using PFXToolKitUI;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Connections;

/// <summary>
/// A service for connecting to a console
/// </summary>
public sealed class ConsoleConnectionService {
    /// <summary>
    /// Attempt to connect to an xbox 360 with a specific address (e.g. 192.168.1.100)
    /// </summary>
    /// <param name="hostName">The address of the xbox 360</param>
    /// <returns>The connection, or null, if we could not connect (e.g. timeout) or the user cancelled the operation</returns>
    public async Task<IConsoleConnection?> OpenDialogAndConnect() {
        // Try get last entered IP address. Helps with debugging and user experience ;)
        string lastIp = BasicApplicationConfiguration.Instance.LastHostName;
        if (string.IsNullOrWhiteSpace(lastIp))
            lastIp = "192.168.1.";

        SingleUserInputInfo info = new SingleUserInputInfo(lastIp) {
            Caption = "Connect to Xbox 360",
            Message = "Ensure you have xbdm running as a plugin on your console!",
            Label = "IP/Hostname Address",
            ConfirmText = "Connect", CancelText = "Cancel", DefaultButton = true,
            Validate = (args) => {
                if (string.IsNullOrWhiteSpace(args.Input))
                    args.Errors.Add("Address cannot be empty");
            }
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) != true) {
            return null;
        }

        // %appdata%/MemEngine360/Options/application.xml
        BasicApplicationConfiguration.Instance.LastHostName = info.Text;
        BasicApplicationConfiguration.Instance.StorageManager.SaveArea(BasicApplicationConfiguration.Instance);

        try {
            using CancellationTokenSource cts = new CancellationTokenSource();
            PhantomRTMConsoleConnection? result = await ActivityManager.Instance.RunTask(async () => {
                IActivityProgress progress = ActivityManager.Instance.GetCurrentProgressOrEmpty();
                progress.Caption = "Connection";
                progress.Text = "Connecting to console...";
                progress.IsIndeterminate = true;
                TcpClient client = new TcpClient();
                try {
                    await client.ConnectAsync(info.Text, 730, cts.Token);
                }
                catch (OperationCanceledException) {
                    return null;
                }
                catch (SocketException e) {
                    string message;
                    switch (e.SocketErrorCode) {
                        case SocketError.InvalidArgument:    message = "Console IP/hostname is invalid"; break;
                        case SocketError.TooManyOpenSockets: message = "System has too many sockets open"; break;
                        case SocketError.TimedOut:           message = "Timeout while connecting. Is the console running?"; break;
                        case SocketError.ConnectionRefused:  message = "Connection refused. Is the console running xbdm?"; break;
                        case SocketError.TryAgain:           message = "Could not identify hostname. Try again later"; break;
                        default:                             message = e.Message; break;
                    }

                    await IMessageDialogService.Instance.ShowMessage("Error", message, defaultButton:MessageBoxResult.OK);
                    return null;
                }

                progress.Text = "Waiting for hot chocolate...";
                StreamReader reader = new StreamReader(client.GetStream());
                string? response = (await reader.ReadLineAsync(cts.Token))?.ToLower();
                if (response == "201- connected") {
                    return new PhantomRTMConsoleConnection(client, reader);
                }

                await await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => IMessageDialogService.Instance.ShowMessage("Error", "Received invalid response from console: " + (response ?? "")));
                return null;
            }, cts);

            return result;
        }
        catch (Exception e) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Could not connect to Xbox 360: " + e.Message);
            return null;
        }
    }
}