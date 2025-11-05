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
using PFXToolKitUI.Activities;
using PFXToolKitUI.Activities.Pausable;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.Scanners;

/// <summary>
/// The pausable task used to execute the "first scan" of the memory scanning feature.
/// </summary>
public sealed class FirstTypedScanTask : AdvancedPausableTask {
    internal readonly ScanningContext ctx;
    internal readonly IConsoleConnection connection;
    private readonly IFeatureIceCubes? iceCubes;
    private readonly Reference<IBusyToken?> myBusyTokenRef;

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

    public IBusyToken? BusyToken => this.myBusyTokenRef.Value;

    public FirstTypedScanTask(ScanningContext context, IConsoleConnection connection, Reference<IBusyToken?> busyTokenRef) : base(true) {
        this.ctx = context ?? throw new ArgumentNullException(nameof(context));
        this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        this.myBusyTokenRef = busyTokenRef ?? throw new ArgumentNullException(nameof(busyTokenRef));
        
        // RunOperation does not call TryObtainBusyToken on first run, so it won't add two handlers. 
        this.ctx.Processor.MemoryEngine.BusyLock.UserQuickReleaseRequested += this.BusyLockOnUserQuickReleaseRequested;
        this.iceCubes = connection.GetFeatureOrDefault<IFeatureIceCubes>();
        this.rgIdx = 0;
    }

    private async Task<bool> TryObtainBusyToken(CancellationToken pauseOrCancelToken) {
        Debug.Assert(this.BusyToken == null);
        BusyLock busyLock = this.ctx.Processor.MemoryEngine.BusyLock;
        this.myBusyTokenRef.Value = await busyLock.BeginBusyOperationFromActivity(pauseOrCancelToken);
        if (this.BusyToken == null) {
            return false;
        }

        busyLock.UserQuickReleaseRequested += this.BusyLockOnUserQuickReleaseRequested;
        return true;
    }

    private void ReleaseBusyToken() {
        BusyLock busyLock = this.ctx.Processor.MemoryEngine.BusyLock;
        Debug.Assert(this.BusyToken != null && busyLock.IsTokenValid(this.BusyToken));

        busyLock.UserQuickReleaseRequested -= this.BusyLockOnUserQuickReleaseRequested;
        this.BusyToken?.Dispose();
        this.myBusyTokenRef.Value = null;
    }

    private void BusyLockOnUserQuickReleaseRequested(BusyLock busyLock, Task task) {
        this.RequestPause(out _, out _);
        task.ContinueWith(static (t, s) => ((FirstTypedScanTask) s!).RequestResume(out _, out _), this, this.CancellationToken);
    }

    protected override async Task RunOperation(CancellationToken pauseOrCancelToken, bool isFirstRun) {
        this.Activity.Progress.Text = "Scanning...";
        if (isFirstRun) {
            Debug.Assert(this.BusyToken != null, "Busy token should not be null at this point");
        }
        else if (!await this.TryObtainBusyToken(pauseOrCancelToken)) {
            return;
        }

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
                    
                    ulong remaining = Math.Min(this.ctx.scanLength - this.rgBaseOffset, this.rgScanEnd - (region.BaseAddress + this.rgBaseOffset));
                    int cbRead = (int) Math.Min(DataTypedScanningContext.ChunkSize, remaining) + (int) overlap;
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
                int cbRead = (int) Math.Min(DataTypedScanningContext.ChunkSize, Math.Max(len - this.rgBaseOffset, 0)) + (int) overlap;
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

    protected override async Task OnPaused(bool isFirst) {
        this.Activity.Progress.Text += " (paused)";
        this.Activity.Progress.CompletionState.TotalCompletion = 0.0;
        await this.TrySetUnFrozen();

        if (this.BusyToken != null) {
            this.ReleaseBusyToken();
        }
    }

    protected override async Task OnCompleted() {
        await this.TrySetUnFrozen();
        // we may still hold the busy token. do not dispose since the scanner still needs it
    }

    private async Task TrySetUnFrozen() {
        if (this.BusyToken != null && !this.isAlreadyFrozen && this.ctx.pauseConsoleDuringScan && this.iceCubes != null) {
            try {
                await this.iceCubes.DebugUnFreeze();
            }
            catch (Exception ex) when (ex is IOException || ex is TimeoutException) {
                this.ctx.ConnectionException = ex;
            }
        }
    }
}