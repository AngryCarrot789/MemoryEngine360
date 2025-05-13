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

using System.Collections.Concurrent;
using System.Globalization;
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Engine.HexEditing.Commands;
using MemEngine360.Engine.Scanners;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Tasks.Pausable;

namespace MemEngine360.Commands;

public class DumpMemoryCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine360 engine, CommandEventArgs e) {
        if (engine.IsConnectionBusy || engine.Connection == null) {
            return Executability.ValidButCannotExecute;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(MemoryEngine360 engine, CommandEventArgs e) {
        using IDisposable? token = await engine.BeginBusyOperationActivityAsync("Dump memory");
        if (token == null) {
            return;
        }

        Action<ValidationArgs> validateMemAddr = (a) => {
            if (!uint.TryParse(a.Input, NumberStyles.HexNumber, null, out _)) {
                if (ulong.TryParse(a.Input, NumberStyles.HexNumber, null, out _)) {
                    a.Errors.Add("Value is too big. Maximum is 0xFFFFFFFF");
                }
                else {
                    a.Errors.Add("Invalid UInt32.");
                }
            }
        };

        ScanningProcessor p = engine.ScanningProcessor;
        DoubleUserInputInfo info = new DoubleUserInputInfo() {
            Caption = "Dump memory region",
            Message = "Select a memory region to read and dump to a file. There will be 2 more dialog prompts after this",
            Footer = "You can pause or cancel the operation at any time",
            ConfirmText = "Start", DefaultButton = true,
            LabelA = "Start address (hex)", LabelB = "Length (hex)",
            ValidateA = validateMemAddr, ValidateB = validateMemAddr,
            TextA = p.StartAddress.ToString("X8"),
            TextB = p.ScanLength.ToString("X8")
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) == true) {
            uint start = uint.Parse(info.TextA, NumberStyles.HexNumber);
            uint length = uint.Parse(info.TextB, NumberStyles.HexNumber);

            MessageBoxResult freezeResult = await IMessageDialogService.Instance.ShowMessage(
                "Freeze console",
                "Freezing the console massively increases how quickly we can download memory from the console",
                "Freeze console during memory dump?",
                MessageBoxButton.YesNo, MessageBoxResult.Yes);
            if (freezeResult == MessageBoxResult.None) {
                return;
            }

            string? filePath = await IFilePickDialogService.Instance.SaveFile("Save binary data", SaveSelectionAsFileCommand.BinaryTypeAndAll);
            if (filePath == null) {
                return;
            }

            DumpMemoryTask task = new DumpMemoryTask(engine, filePath, start, length, freezeResult == MessageBoxResult.Yes, token);
            await task.Run();
        }
    }

    private class DumpMemoryTask : AdvancedPausableTask {
        private readonly MemoryEngine360 engine;
        private readonly uint startAddress;
        private readonly uint countBytes;
        private readonly bool freezeConsole;
        private readonly string filePath;
        private readonly SimpleCompletionState downloadCompletion;
        private PopCompletionStateRangeToken downloadCompletionToken;

        // live data
        private uint dlCurrAddress, dlCbRemaining;
        private uint cbDownloaded, cbWritten;
        private volatile bool isDoneDownloading;
        private readonly ConcurrentQueue<byte[]> buffers;

        // live handles
        private IDisposable? busyToken;
        private FileStream? fileOutput;

        public DumpMemoryTask(MemoryEngine360 engine, string filePath, uint startAddress, uint countBytes, bool freezeConsole, IDisposable busyToken) : base(true) {
            this.engine = engine;
            this.filePath = filePath;
            this.startAddress = startAddress;
            this.countBytes = countBytes;
            this.freezeConsole = freezeConsole;
            this.busyToken = busyToken;
            this.dlCurrAddress = this.startAddress;
            this.dlCbRemaining = this.countBytes;
            this.buffers = new ConcurrentQueue<byte[]>();
            
            this.downloadCompletion = new SimpleCompletionState();
            this.downloadCompletion.CompletionValueChanged += state => {
                this.Activity.Progress.CompletionState.TotalCompletion = state.TotalCompletion;
                this.Activity.Progress.Text = $"Downloaded {ValueScannerUtils.ByteFormatter.ToString(this.countBytes * state.TotalCompletion, false)}/{ValueScannerUtils.ByteFormatter.ToString(this.countBytes, false)}";
            };
            
            this.downloadCompletionToken = this.downloadCompletion.PushCompletionRange(0.0, 1.0 / this.countBytes);
        }

        protected override async Task RunFirst(CancellationToken pauseOrCancelToken) {
            // Update initial text
            this.downloadCompletion.OnCompletionValueChanged();

            this.Activity.Progress.Caption = "Memory Dump";
            await this.RunDownloadLoop(true, pauseOrCancelToken);
        }

        protected override async Task Continue(CancellationToken pauseOrCancelToken) {
            ActivityTask task = this.Activity;
            task.Progress.Text = "Waiting for busy operations...";
            this.busyToken = await this.engine.BeginBusyOperationAsync(pauseOrCancelToken);
            if (this.busyToken == null) {
                return;
            }

            await this.RunDownloadLoop(false, pauseOrCancelToken);
        }

        protected override Task OnPaused(bool isFirst) {
            ActivityTask task = this.Activity;
            task.Progress.Text += " (paused)";
            this.fileOutput?.Dispose();
            this.busyToken?.Dispose();
            this.busyToken = null;
            return Task.CompletedTask;
        }

        protected override Task OnCompleted() {
            this.fileOutput?.Dispose();
            this.busyToken?.Dispose();
            this.busyToken = null;
            this.downloadCompletionToken.Dispose();
            this.downloadCompletionToken = default;
            return Task.CompletedTask;
        }

        private async Task RunDownloadLoop(bool isFirst, CancellationToken pauseOrCancelToken) {
            // We don't handle the exception here
            this.fileOutput = new FileStream(this.filePath, isFirst ? FileMode.Create : FileMode.Append, FileAccess.Write, FileShare.Read);

            Task taskDownload = Task.Run(async () => {
                IConsoleConnection? connection = this.engine.Connection;
                if (this.engine.IsShuttingDown || connection?.IsConnected != true) {
                    return;
                }

                if (this.freezeConsole && connection is IHaveIceCubes)
                    await ((IHaveIceCubes) connection).DebugFreeze();

                Exception? e = null;
                try {
                    pauseOrCancelToken.ThrowIfCancellationRequested();
                    while (this.dlCbRemaining > 0) {
                        pauseOrCancelToken.ThrowIfCancellationRequested();
                        byte[] downloadBuffer = new byte[0x10000];
                        uint cbRead = Math.Min((uint) downloadBuffer.Length, this.dlCbRemaining);
                        await connection.ReadBytes(this.dlCurrAddress, downloadBuffer, 0, cbRead);
                        this.dlCbRemaining -= cbRead;
                        this.dlCurrAddress += cbRead;

                        this.buffers.Enqueue(downloadBuffer);
                        this.downloadCompletion?.OnProgress(cbRead);
                    }
                }
                catch (OperationCanceledException ex) {
                    e = ex;
                }

                if (this.freezeConsole && connection is IHaveIceCubes)
                    await ((IHaveIceCubes) connection).DebugUnFreeze();

                if (e != null)
                    throw e;
                
                this.isDoneDownloading = true;
            }, CancellationToken.None);

            Task taskFileIO = Task.Run(async () => {
                while (!this.isDoneDownloading || !this.buffers.IsEmpty) {
                    while (!pauseOrCancelToken.IsCancellationRequested && this.buffers.TryDequeue(out byte[]? buffer)) {
                        await this.fileOutput.WriteAsync(buffer, CancellationToken.None);
                    }

                    pauseOrCancelToken.ThrowIfCancellationRequested();
                    await Task.Delay(50, pauseOrCancelToken);
                }
            }, CancellationToken.None);

            await Task.WhenAll(taskDownload, taskFileIO);
        }
    }
}