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

using System.Diagnostics;
using MemEngine360.Connections;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Tasks.Pausable;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.Scanners;

/// <summary>
/// The pausable task used to execute the "first scan" of the memory scanning feature.
/// </summary>
public sealed class FirstScanTask : AdvancedPausableTask {
    internal readonly ScanningContext ctx;
    internal readonly IConsoleConnection connection;
    private IDisposable? myBusyToken;

    // region scanning info
    private List<MemoryRegion>? myRegions;
    private int rgIdx;
    private uint cbRegion;

    // chunk scanning
    private int chunkIdx;

    // general variables
    private uint originalOffset, offset;
    private bool isProcessingCurrentRegion;

    public FirstScanTask(ScanningContext context, IConsoleConnection connection, IDisposable busyToken) : base(true) {
        this.ctx = context ?? throw new ArgumentNullException(nameof(context));
        this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        this.myBusyToken = busyToken ?? throw new ArgumentNullException(nameof(busyToken));
        this.rgIdx = 0;
    }

    protected override async Task RunFirst(CancellationToken pauseOrCancelToken) {
        Debug.Assert(this.myBusyToken != null, "Busy token should not be null at this point");
        
        this.Activity.Progress.Text = "Scanning...";
        await this.RunScan(pauseOrCancelToken);
    }

    protected override async Task Continue(CancellationToken pauseOrCancelToken) {
        ActivityTask task = this.Activity;
        task.Progress.Text = "Scanning...";
        task.Progress.Text = "Waiting for busy operations...";
        this.myBusyToken = await this.ctx.theProcessor.MemoryEngine360.BeginBusyOperationAsync(pauseOrCancelToken);
        if (this.myBusyToken == null) {
            return;
        }
        
        await this.RunScan(pauseOrCancelToken);
    }

    protected override async Task OnPaused(bool isFirst) {
        this.Activity.Progress.Text += " (paused)";
        this.Activity.Progress.CompletionState.TotalCompletion = 0.0;
        await this.SetFrozenState(false);
        
        this.myBusyToken?.Dispose();
        this.myBusyToken = null;
    }

    protected override async Task OnCompleted() {
        await this.SetFrozenState(false);
        
        this.myBusyToken?.Dispose();
        this.myBusyToken = null;
    }

    private async Task<bool> SetFrozenState(bool isFrozen) {
        try {
            if (this.ctx.pauseConsoleDuringScan && this.connection is IHaveIceCubes ice) {
                await (isFrozen ? ice.DebugFreeze() : ice.DebugUnFreeze());
            }
        }
        catch (IOException e) {
            await this.OnConnectionException(e);
            return false;
        }
        
        return true;
    }

    private async Task RunScan(CancellationToken pauseOrCancelToken) {
        if (!await this.SetFrozenState(true)) {
            pauseOrCancelToken.ThrowIfCancellationRequested();
            return;
        }
        
        IActivityProgress progress = ActivityManager.Instance.CurrentTask.Progress;
        byte[] tmpBuffer = new byte[ScanningContext.ChunkSize];
        if (this.ctx.scanMemoryPages && this.connection is IHaveMemoryRegions iHaveRegions) {
            if (this.myRegions == null) {
                progress.IsIndeterminate = true;
                progress.Text = "Preparing memory regions...";

                List<MemoryRegion> allRegions;
                try {
                    allRegions = await iHaveRegions.GetMemoryRegions(true, false);
                }
                catch (IOException e) {
                    await this.OnConnectionException(e);
                    pauseOrCancelToken.ThrowIfCancellationRequested();
                    return;
                }

                List<MemoryRegion> regions = new List<MemoryRegion>();
                foreach (MemoryRegion region in allRegions) {
                    // Putting this comment here cus it's the 3rd time I fucked this up
                    // ------------- Do not use >= !!! scanEndAddress is exclusive
                    if (this.ctx.scanEndAddress > region.BaseAddress && this.ctx.startAddress < (region.BaseAddress + region.Size)) {
                        regions.Add(region);
                    }
                }

                this.myRegions = regions;
                this.rgIdx = 0;
            }
            
            progress.IsIndeterminate = false;
            for (; this.rgIdx < this.myRegions.Count; this.rgIdx++) {
                pauseOrCancelToken.ThrowIfCancellationRequested();
                MemoryRegion region = this.myRegions[this.rgIdx];

                // We track when processing the current region at rgIdx, because
                // we do not want to re-scan from the start again if the task was paused.
                // Technically we only need to account for offset not the other 2
                if (!this.isProcessingCurrentRegion) {
                    uint realStart = Math.Max(region.BaseAddress, this.ctx.startAddress);
                    uint realEnd = Math.Min(region.EndAddress, this.ctx.scanEndAddress);
                    this.offset = this.originalOffset = realStart - region.BaseAddress;
                    this.cbRegion = realEnd - realStart;
                }

                // The progress bar should show the true progress of the chunk scanning, so we set the
                // completion range as the actual range we're going to be reading.
                // The text will still show the absolute ranges though, which is fine
                using PopCompletionStateRangeToken token = progress.CompletionState.PushCompletionRange(0.0, 1.0 / (this.cbRegion - this.originalOffset));
                if (this.offset != this.originalOffset) {
                    progress.CompletionState.OnProgress(this.offset - this.originalOffset);
                }
                
                // mark the beginning of memory region processing -- if the 
                this.isProcessingCurrentRegion = true;
                while (this.offset < this.cbRegion) {
                    pauseOrCancelToken.ThrowIfCancellationRequested();
                    
                    // Beyond here we can't pause. Or if we could, we would
                    // need a 2nd flag and a rewrite of ProcessMemoryBlockForFirstScan
                    progress.Text = $"Region {this.rgIdx + 1}/{this.myRegions.Count} ({ValueScannerUtils.ByteFormatter.ToString(this.offset, false)}/{ValueScannerUtils.ByteFormatter.ToString(region.Size, false)})";
                    progress.CompletionState.OnProgress(ScanningContext.ChunkSize);

                    uint baseAddress = region.BaseAddress + this.offset;
                    uint cbTargetRead = Math.Min(ScanningContext.ChunkSize, this.cbRegion - this.offset /* remaining */);
                    uint cbActualRead;
                    try {
                        cbActualRead = await this.connection.ReadBytes(baseAddress, tmpBuffer, 0, cbTargetRead).ConfigureAwait(false);
                    }
                    catch (IOException e) {
                        await this.OnConnectionException(e);
                        pauseOrCancelToken.ThrowIfCancellationRequested();
                        return;
                    }
                    
                    if (cbActualRead > 0) {
                        this.ctx.ProcessMemoryBlockForFirstScan(baseAddress, tmpBuffer, cbActualRead, this.ctx.alignment);
                    }

                    this.offset += ScanningContext.ChunkSize;
                }

                this.isProcessingCurrentRegion = false;
            }
        }
        else {
            uint len = this.ctx.scanLength, totalChunks = len / ScanningContext.ChunkSize;
            using PopCompletionStateRangeToken token = progress.CompletionState.PushCompletionRange(0, 1.0 / len);

            if (!this.isProcessingCurrentRegion) {
                this.chunkIdx = 0;
            }
            
            this.isProcessingCurrentRegion = true;
            while (this.offset < len) {
                pauseOrCancelToken.ThrowIfCancellationRequested();
                progress.Text = $"Chunk {this.chunkIdx + 1}/{totalChunks} ({ValueScannerUtils.ByteFormatter.ToString(this.offset, false)}/{ValueScannerUtils.ByteFormatter.ToString(len, false)})";
                progress.CompletionState.OnProgress(ScanningContext.ChunkSize);

                uint baseAddress = this.ctx.startAddress + this.offset;
                uint cbTargetRead = Math.Min(ScanningContext.ChunkSize, Math.Max(len - this.offset, 0));
                uint cbActualRead;
                
                try {
                    cbActualRead = await this.connection.ReadBytes(baseAddress, tmpBuffer, 0, cbTargetRead).ConfigureAwait(false);
                }
                catch (IOException e) {
                    await this.OnConnectionException(e);
                    pauseOrCancelToken.ThrowIfCancellationRequested();
                    return;
                }
                
                if (cbActualRead > 0) {
                    this.ctx.ProcessMemoryBlockForFirstScan(baseAddress, tmpBuffer, cbActualRead, this.ctx.alignment);
                }
                
                this.offset += ScanningContext.ChunkSize;
                this.chunkIdx++;
            }
            
            this.isProcessingCurrentRegion = false;
        }
    }

    private async Task OnConnectionException(IOException e) {
        await IMessageDialogService.Instance.ShowMessage("Network error", "An exception occurred while performing console operation", e.GetToString());
    }
}