using System.Net.Sockets;
using MemEngine360.Configs;
using MemEngine360.Connections.Impl;
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
    public async Task<IConsoleConnection?> OpenDialogAndConnect(IActivityProgress progress) {
        // Try get last entered IP address. Helps with debugging and user experience ;)
        string lastIp = BasicApplicationConfiguration.Instance.LastHostName;
        if (string.IsNullOrWhiteSpace(lastIp))
            lastIp = "192.168.1.";

        SingleUserInputInfo info = new SingleUserInputInfo("Connect to Xbox 360", "Ensure you have xbdm running as a plugin on your console!", "IP/Hostname Address", lastIp) {
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
                catch {
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
            }, progress, cts);

            return result;
        }
        catch (Exception e) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Could not connect to Xbox 360: " + e.Message);
            return null;
        }
    }
}