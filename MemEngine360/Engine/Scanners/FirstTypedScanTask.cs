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
using MemEngine360.Connections;
using MemEngine360.Connections.Features;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Tasks.Pausable;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.Scanners;

/// <summary>
/// The pausable task used to execute the "first scan" of the memory scanning feature.
/// </summary>
public sealed class FirstTypedScanTask : AdvancedPausableTask {
    internal readonly ScanningContext ctx;
    internal readonly IConsoleConnection connection;
    private readonly IFeatureIceCubes? iceCubes;
    private readonly Reference<IDisposable?> myBusyTokenRef;

    // region scanning info
    private List<MemoryRegion>? myRegions;
    private int rgIdx;
    private uint rgScanStart, rgScanEnd;

    // chunk scanning
    private int chunkIdx;

    // general variables
    private uint rgBaseOriginalOffset, rgBaseOffset;
    private bool isProcessingCurrentRegion;
    private bool isAlreadyFrozen;

    public IDisposable? BusyToken => this.myBusyTokenRef.Value;
    
    public FirstTypedScanTask(ScanningContext context, IConsoleConnection connection, Reference<IDisposable?> busyTokenRef) : base(true) {
        this.ctx = context ?? throw new ArgumentNullException(nameof(context));
        this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        this.myBusyTokenRef = busyTokenRef ?? throw new ArgumentNullException(nameof(busyTokenRef));
        this.iceCubes = connection.GetFeatureOrDefault<IFeatureIceCubes>();
        this.rgIdx = 0;
    }

    protected override async Task RunFirst(CancellationToken pauseOrCancelToken) {
        Debug.Assert(this.myBusyTokenRef != null, "Busy token should not be null at this point");

        this.Activity.Progress.Text = "Scanning...";
        await this.RunScan(pauseOrCancelToken);
    }

    protected override async Task Continue(CancellationToken pauseOrCancelToken) {
        ActivityTask task = this.Activity;
        task.Progress.Text = "Scanning...";
        task.Progress.Text = "Waiting for busy operations...";
        this.myBusyTokenRef.Value = await this.ctx.Processor.MemoryEngine.BeginBusyOperationAsync(pauseOrCancelToken);
        if (this.BusyToken == null) {
            return;
        }

        await this.RunScan(pauseOrCancelToken);
    }

    protected override async Task OnPaused(bool isFirst) {
        this.Activity.Progress.Text += " (paused)";
        this.Activity.Progress.CompletionState.TotalCompletion = 0.0;
        await this.TrySetUnFrozen();

        this.myBusyTokenRef.Value?.Dispose();
        this.myBusyTokenRef.Value = null;
    }

    protected override async Task OnCompleted() {
        await this.TrySetUnFrozen();
        // do not dispose busy token after completed since the scanner may still need it
    }

    private async Task TrySetUnFrozen() {
        if (!this.isAlreadyFrozen && this.ctx.pauseConsoleDuringScan && this.iceCubes != null) {
            try {
                await this.iceCubes.DebugUnFreeze();
            }
            catch (Exception ex) when (ex is IOException || ex is TimeoutException) {
                this.ctx.ConnectionException = ex;
            }
        }
    }

    private async Task RunScan(CancellationToken pauseOrCancelToken) {
        if (this.ctx.HasConnectionError || this.connection.IsClosed) {
            return;
        }

        if (this.ctx.pauseConsoleDuringScan && this.iceCubes != null) {
            try {
                this.isAlreadyFrozen = await this.iceCubes.DebugFreeze() == FreezeResult.AlreadyFrozen;
            }
            catch (Exception ex) when (ex is IOException || ex is TimeoutException) {
                this.ctx.ConnectionException = ex;
                return;
            }
        }
        
        Debug.Assert(!this.connection.IsClosed);
        IActivityProgress progress = ActivityManager.Instance.CurrentTask.Progress;
        
        uint overlap = this.ctx.Overlap;
        byte[] tmpBuffer = new byte[DataTypedScanningContext.ChunkSize + overlap];
        if (this.ctx.scanMemoryPages && this.connection.TryGetFeature(out IFeatureMemoryRegions? memRegionFeature)) {
            if (this.myRegions == null) {
                progress.IsIndeterminate = true;
                progress.Text = "Preparing memory regions...";

                List<MemoryRegion> allRegions;
                try {
                    allRegions = await memRegionFeature.GetMemoryRegions(true, false);
                }
                catch (Exception ex) when (ex is IOException || ex is TimeoutException) {
                    this.ctx.ConnectionException = ex;
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
                    this.rgScanStart = Math.Max(region.BaseAddress, this.ctx.startAddress);
                    this.rgScanEnd = Math.Min(region.EndAddress, this.ctx.scanEndAddress);
                    this.rgBaseOffset = this.rgBaseOriginalOffset = this.rgScanStart - region.BaseAddress;
                }

                // The progress bar should show the true progress of the chunk scanning, so we set the
                // completion range as the actual range we're going to be reading.
                // The text will still show the absolute ranges though, which is fine
                using PopCompletionStateRangeToken token = progress.CompletionState.PushCompletionRange(0.0, 1.0 / (this.rgScanEnd - this.rgScanStart));
                if (this.rgBaseOffset != this.rgBaseOriginalOffset) {
                    progress.CompletionState.OnProgress(this.rgBaseOffset - this.rgBaseOriginalOffset);
                }

                // mark the beginning of memory region processing -- if the 
                this.isProcessingCurrentRegion = true;
                while ((region.BaseAddress + this.rgBaseOffset) < this.rgScanEnd) {
                    pauseOrCancelToken.ThrowIfCancellationRequested();

                    // Beyond here we can't pause. Or if we could, we would
                    // need a 2nd flag and a rewrite of ProcessMemoryBlockForFirstScan
                    progress.Text = $"Region {this.rgIdx + 1}/{this.myRegions.Count} ({ValueScannerUtils.ByteFormatter.ToString(this.rgBaseOffset, false)}/{ValueScannerUtils.ByteFormatter.ToString(region.Size, false)})";
                    progress.CompletionState.OnProgress(DataTypedScanningContext.ChunkSize);

                    uint baseAddress = region.BaseAddress + this.rgBaseOffset;
                    int cbRead = (int) Math.Min(DataTypedScanningContext.ChunkSize, Math.Max(this.rgScanEnd - this.rgBaseOffset, 0) /* remaining */) + (int) overlap;
                    try {
                        await this.connection.ReadBytes(baseAddress, tmpBuffer, 0, cbRead).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is IOException || ex is TimeoutException) {
                        this.ctx.ConnectionException = ex;
                        pauseOrCancelToken.ThrowIfCancellationRequested();
                        return;
                    }

                    this.ctx.ProcessMemoryBlockForFirstScan(baseAddress, new ReadOnlySpan<byte>(tmpBuffer, 0, cbRead));
                    this.rgBaseOffset += DataTypedScanningContext.ChunkSize;
                }

                this.isProcessingCurrentRegion = false;
            }
        }
        else {
            // uint overlap = (uint) Math.Max(this.ctx.cbDataType - (int) this.ctx.alignment, 0);
            uint len = this.ctx.scanLength, totalChunks = len / DataTypedScanningContext.ChunkSize;
            using PopCompletionStateRangeToken token = progress.CompletionState.PushCompletionRange(0, 1.0 / len);
            if (this.rgBaseOffset != 0) {
                progress.CompletionState.OnProgress(this.rgBaseOffset);
            }

            if (!this.isProcessingCurrentRegion) {
                this.chunkIdx = 0;
            }

            this.isProcessingCurrentRegion = true;
            while (this.rgBaseOffset < len) {
                pauseOrCancelToken.ThrowIfCancellationRequested();
                progress.Text = $"Chunk {this.chunkIdx + 1}/{totalChunks} ({ValueScannerUtils.ByteFormatter.ToString(this.rgBaseOffset, false)}/{ValueScannerUtils.ByteFormatter.ToString(len, false)})";
                progress.CompletionState.OnProgress(DataTypedScanningContext.ChunkSize);

                // if (overlap > 0) {
                //     Buffer.BlockCopy(tmpBuffer, (int) (ScanningContext.ChunkSize - overlap + currentOverlap), tmpBuffer, 0, (int) overlap);
                // }

                uint baseAddress = this.ctx.startAddress + this.rgBaseOffset;
                int cbRead = (int) Math.Min(DataTypedScanningContext.ChunkSize, Math.Max(this.ctx.scanLength - this.rgBaseOffset, 0)) + (int) overlap;
                try {
                    await this.connection.ReadBytes(baseAddress, tmpBuffer, 0, cbRead).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException || ex is TimeoutException) {
                    this.ctx.ConnectionException = ex;
                    pauseOrCancelToken.ThrowIfCancellationRequested();
                    return;
                }

                this.ctx.ProcessMemoryBlockForFirstScan(baseAddress, new ReadOnlySpan<byte>(tmpBuffer, 0, cbRead));

                this.rgBaseOffset += DataTypedScanningContext.ChunkSize; // + (uint) (overlap - currentOverlap);
                this.chunkIdx++;
                // currentOverlap = (int) overlap;
                // totalOverlap += (int) overlap;
            }

            this.isProcessingCurrentRegion = false;
        }
    }
}