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

using System.Diagnostics;
using System.Runtime.Versioning;
using MemEngine360.Connections;
using MemEngine360.PS3.CC;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.PS3;

public delegate void ConnectionInfoCCAPIIpAddressChangedEventHandler(ConnectToCCAPIInfo sender);

[SupportedOSPlatform("windows")]
public class ConnectToCCAPIInfo : UserConnectionInfo {
    public static readonly FileFilter RARFile = FileFilter.Builder("RaR Image").Patterns("*.rar").Build();

    private string ipAddress;

    public string IpAddress {
        get => this.ipAddress;
        set => PropertyHelper.SetAndRaiseINE(ref this.ipAddress, value, this, static t => t.IpAddressChanged?.Invoke(t));
    }

    public event ConnectionInfoCCAPIIpAddressChangedEventHandler? IpAddressChanged;
    
    // private CancellationTokenSource? ctsApiRun;

    public ConsoleControlAPI? CCApi { get; set; }
    
    

    public ConnectToCCAPIInfo() : base(ConnectionTypePS3CCAPI.Instance) {
    }

    protected override void OnShown() {
        // Debug.Assert(this.ctsApiRun == null);
        // this.ctsApiRun = new CancellationTokenSource();

        // Task.Run(async () => this.CCApi = await ConsoleControlAPI.Run());
    }

    protected override void OnHidden() {
        // this.ctsApiRun?.Cancel();
        // this.ctsApiRun?.Dispose();
        // this.ctsApiRun = null;
    }

    public static async Task<bool> TryDownloadCCApi(ITopLevel? topLevel, bool inNewActivity, CancellationToken cancellation) {
        const string ManualInstructionText = "Please download it from enstoneworld.com, then place CCAPI.dll into the same folder as MemoryEngine360.exe";
        if (File.Exists("CCAPI.dll")) {
            return true; // already exits
        }

        if (topLevel == null || !topLevel.TryGetWebLauncher(out IWebLauncher? webLauncher)) {
            await IMessageDialogService.Instance.ShowMessage("CCAPI Unavailable", "CCAPI has not been downloaded yet. " + ManualInstructionText, MessageBoxButtons.OKCancel, dialogCancellation: cancellation);
            return false;
        }

        MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("CCAPI Unavailable", "CCAPI has not been downloaded yet. Would you like to download it?", MessageBoxButtons.OKCancel, dialogCancellation: cancellation);
        if (result != MessageBoxResult.OK) {
            return false;
        }

        const string unRarPath = @"C:\Program Files\WinRAR\UnRAR.exe";
        if (!File.Exists(unRarPath)) {
            await IMessageDialogService.Instance.ShowMessage("WinRaR", "WinRaR is not installed - cannot auto-download and extract CCAPI. " + ManualInstructionText, dialogCancellation: cancellation);
            return false;
        }

        const string url = @"https://www.enstoneworld.com/downloads/index/40/CCAPI_2_80_REV13_Package__developer_";
        if (!await webLauncher.LaunchUriAsync(new Uri(url, UriKind.Absolute))) {
            return false;
        }

        if (inNewActivity) {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            ActivityTask<bool> activity = ActivityManager.Instance.RunTask(() => Part2TryDownloadCcApiInActivity(unRarPath, ActivityTask.Current.CancellationToken), cts);
            return (await activity).GetValueOrDefault();
        }
        else {
            return await Part2TryDownloadCcApiInActivity(unRarPath, cancellation);
        }
    }

    private static async Task<bool> Part2TryDownloadCcApiInActivity(string unRarPath, CancellationToken cancellation) {
        ActivityTask task = ActivityTask.Current;
        task.Progress.Text = "Waiting 2 seconds for download to complete...";
        
        await Task.Delay(2000, cancellation); // wait a little bit for the file to download or to differentiate our SFD from the web browsers'

        // using (HttpClient client = new HttpClient()) {
        //     client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36 OPR/122.0.0.0 (Edition std-2)");
        //     await using Stream s = await client.GetStreamAsync(url, cancellation);
        //     await using FileStream fs = new FileStream(rarFile, FileMode.Create);
        //     await s.CopyToAsync(fs, cancellation);
        // }

        task.Progress.Text = "Open RaR file";
        string? selectedRarFile = await IFilePickDialogService.Instance.OpenFile("Open downloaded RaR file containing CCAPI.dll", [RARFile, Filters.All]);
        if (selectedRarFile == null) {
            return false;
        }

        cancellation.ThrowIfCancellationRequested();

        task.Progress.Text = "Unzipping RaR...";
        string temporaryDir = Path.Combine(Path.GetTempPath(), "CCAPI");
        Directory.CreateDirectory(temporaryDir);

        const string fileInArchive = @"CcApi_package_2.80_Rev13_dev\DEV\Windows\CCAPI.dll";

        Process proc = new Process() {
            StartInfo = new ProcessStartInfo(unRarPath, $"x -y \"{selectedRarFile}\" \"{fileInArchive}\" \"{temporaryDir}\"") {
                CreateNoWindow = false, // true
                WindowStyle = ProcessWindowStyle.Normal // Hidden
            },
            EnableRaisingEvents = true
        };

        if (!proc.Start()) {
            throw new Exception("Failed to start unrar process");
        }

        await proc.WaitForExitAsync(cancellation);

        try {
            task.Progress.Text = "Copying files..";
            string absPath = Path.Combine(temporaryDir, fileInArchive);
            if (!File.Exists(absPath)) {
                throw new Exception("RAR file did not contain CCAPI.dll");
            }

            // copy file to executing location
            File.Move(absPath, "CCAPI.dll");
        }
        finally {
            try {
                Directory.Delete(temporaryDir, true);
            }
            catch {
                // ignored
            }
        }

        return true;
    }
}