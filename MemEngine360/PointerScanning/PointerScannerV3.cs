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

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using MemEngine360.Engine;
using MemEngine360.Engine.Addressing;
using PFXToolKitUI;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.PointerScanning;

public class PointerScannerV3 {
    private readonly struct AddressableRange(uint @base, uint length) {
        public readonly uint @base = @base;
        public readonly uint length = length;
        public readonly uint end = @base + length;

        public bool Contains(uint value) => value >= this.@base && value < this.end;

        public AddressableRange WithBase(uint value) => new AddressableRange(value, this.length);
        public AddressableRange WithLength(uint value) => new AddressableRange(this.@base, value);
    }

    private AddressableRange addressableRange;
    private byte maxDepth = 6;
    private uint minimumOffset = 4; // using 4 might help with avoiding linked lists 
    private uint maximumOffset = 0x4000;
    private uint searchAddress;
    private uint alignment = 4;

    /// <summary>
    /// Gets the base address of the addressable memory space, as in, the smallest address a pointer can be
    /// </summary>
    public uint AddressableBase {
        get => this.addressableRange.@base;
        set {
            if (this.addressableRange.@base != value) {
                this.addressableRange = this.addressableRange.WithBase(value);
                this.AddressableRangeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Gets the number of bytes (relative to <see cref="AddressableBase"/>) that can be scanned as a potential pointer
    /// </summary>
    public uint AddressableLength {
        get => this.addressableRange.length;
        set {
            if (this.addressableRange.length != value) {
                this.addressableRange = this.addressableRange.WithLength(value);
                this.AddressableRangeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Gets the maximum depth a pointer can be, as in, the max amount of offsets there can be to reach <see cref="SearchAddress"/>
    /// </summary>
    public byte MaxDepth {
        get => this.maxDepth;
        set => PropertyHelper.SetAndRaiseINE(ref this.maxDepth, value, this, this.MaxDepthChanged);
    }

    /// <summary>
    /// Same as <see cref="MaximumOffset"/> but for depths of >= 2
    /// </summary>
    public uint MinimumOffset {
        get => this.minimumOffset;
        set => PropertyHelper.SetAndRaiseINE(ref this.minimumOffset, value, this, this.MinimumOffsetChanged);
    }

    /// <summary>
    /// Gets or sets the maximum offset from a pointer that another pointer can be. Default value is 0x2000 (8192).
    /// <para>
    /// For example, say the actual pointer chain of a value you're interested in is <c><![CDATA[ 0x8262AA00 + 0xFC -> +0x24 ]]></c>,
    /// and the maximum offset is FF, this value can be discovered by the pointer scan. But If the pointer chain is
    /// say <c><![CDATA[ 0x8262AA00 +0xFc -> +0xEF2 ]]></c>, then it cannot be discovered, because 0xEF2 exceeds <see cref="MaximumOffset"/>
    /// </para>
    /// <para>
    /// Therefore, ideally this value should be pretty big but not so big that the scanning takes the rest of the universe's lifetime to complete
    /// </para>
    /// </summary>
    public uint MaximumOffset {
        get => this.maximumOffset;
        set => PropertyHelper.SetAndRaiseINE(ref this.maximumOffset, value, this, this.MaximumOffsetChanged);
    }

    /// <summary>
    /// Gets or sets the actual address we want to scan for, e.g. the memory address of an ammo count
    /// </summary>
    public uint SearchAddress {
        get => this.searchAddress;
        set => PropertyHelper.SetAndRaiseINE(ref this.searchAddress, value, this, this.SearchAddressChanged);
    }

    /// <summary>
    /// Gets or sets the alignment for pointer types. Default is 4, since the xbox 360 (apparently) uses this for the size of words.
    /// </summary>
    public uint Alignment {
        get => this.alignment;
        set => PropertyHelper.SetAndRaiseINE(ref this.alignment, value, this, this.AlignmentChanged);
    }

    public bool HasPointerMap {
        get => field;
        private set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.HasPointerMapChanged);
    }

    public bool IsScanRunning {
        get => field;
        private set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.IsScanRunningChanged);
    }

    public IReadOnlyDictionary<uint, uint> PointerMap => this.alignedVirtualAddressToValue;

    public ObservableList<DynamicAddress> PointerChain { get; } = new ObservableList<DynamicAddress>();

    public MemoryEngine MemoryEngine { get; }

    public event EventHandler? AddressableRangeChanged;
    public event EventHandler? MaxDepthChanged;
    public event EventHandler? MinimumOffsetChanged;
    public event EventHandler? MaximumOffsetChanged;
    public event EventHandler? SearchAddressChanged;
    public event EventHandler? AlignmentChanged;
    public event EventHandler? HasPointerMapChanged;
    public event EventHandler? IsScanRunningChanged;

    private byte[]? memoryDump;

    // aligned offsets are aligned to the `loadedAlignment` field.
    //     Say align is 4, offsets 0x4 and 0x8 for an int32 are right next to eachother.
    // unaligned offsets are packed closed together.
    //     Say align is 4, offsets 1 and 2 for an int32 and right next to eachother.
    //     You get the aligned offset by doing `unaligned * loadedAlign`, but there may be some
    //     issues for align values that aren't 4, 8, 16, etc.
    
    // Offset (relative to virtualBaseAddress) to value
    private uint[]? unalignedOffsetToValue;

    // Virtual address (virtualBaseAddress + aligned_offset) to value
    private readonly SortedList<uint, uint> alignedVirtualAddressToValue = new SortedList<uint, uint>(200000);

    // A set of unaligned offsets (relative to virtualBaseAddress) that have a value.
    private readonly List<uint> unalignedValidOffsets = new List<uint>(50000);

    // Maps an unaligned offset (relative to virtualBaseAddress) to a list of unaligned
    // offsets that, when added to the key, are an index into `unalignedOffsetToValue`.
    // This dictionary will be similar sized to alignedVirtualAddressToValue,
    // and each list will contain a maximum of `maximumOffset / loadedAlign` offsets,
    // but i've only seen up to 1023 maximum so there may be a bug somewhere.
    private readonly Dictionary<uint, List<uint>> unalignedOffsetToNearbyValidOffsets = new Dictionary<uint, List<uint>>(50000);

    // private readonly ConcurrentDictionary<uint, object> visitedPointers; // a set of pointers that have already been resolved 
    private bool isMemoryLittleEndian;
    private uint virtualBaseAddress; // The base address the user specified when loading the file
    private CancellationTokenSource? scanCts;
    private uint loadedAlignment;

    private readonly struct PointerPrivate : IEquatable<PointerPrivate> {
        public readonly uint addr; // The base address
        public readonly uint offset; // The offset from the address
        public readonly uint value; // The dereferenced u32 value at addr+offset

        public PointerPrivate(uint addr, uint offset, uint value) {
            this.addr = addr;
            this.offset = offset;
            this.value = value;
        }

        public override string ToString() {
            return $"[{this.addr:X8} + {this.offset:X}] -> {this.value:X8}";
        }

        public bool Equals(PointerPrivate other) {
            return this.addr == other.addr && this.offset == other.offset && this.value == other.value;
        }

        public override bool Equals(object? obj) {
            return obj is PointerPrivate other && this.Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine(this.addr, this.offset, this.value);
        }
    }

    public PointerScannerV3(MemoryEngine memoryEngine) {
        this.MemoryEngine = memoryEngine;
    }

    public async Task Run() {
        if (this.memoryDump == null) {
            throw new InvalidOperationException("Memory dump not loaded");
        }

        if (!this.HasPointerMap) {
            throw new InvalidOperationException("No pointer map loaded");
        }

        this.PointerChain.Clear();

        using CancellationTokenSource cts = new CancellationTokenSource();
        this.scanCts = cts;
        this.IsScanRunning = true;
        await ActivityManager.Instance.RunTask(async () => {
            TaskCompletionSource tcs = new TaskCompletionSource();
            ThreadedScanningContext threadContext = new ThreadedScanningContext(ActivityTask.Current, tcs, this.virtualBaseAddress, this.memoryDump!.Length);
            Thread thread = new Thread(this.ThreadedPointerScanMain) {
                Name = "Pointer Scan Thread",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };

            try {
                thread.Start(threadContext);
            }
            catch (Exception e) {
                await IMessageDialogService.Instance.ShowMessage("Thread Error", "Error starting thread", e.GetToString());
                return;
            }

            IDispatcherTimer timer = ApplicationPFX.Instance.Dispatcher.CreateTimer(DispatchPriority.Default);
            timer.Interval = TimeSpan.FromSeconds(0.2);
            timer.Tick += (sender, args) => {
                threadContext.ActivityTask.Progress.Text = $"Scanned {threadContext.TotalValidAddresses} valid addresses";
            };

            timer.Start();

            Exception? exception = null;
            try {
                await tcs.Task;
            }
            catch (OperationCanceledException) {
                // ignored
            }
            catch (Exception e) {
                exception = e;
            }

            timer.Stop();
            if (exception != null) {
                await IMessageDialogService.Instance.ShowExceptionMessage("Pointer Scan Error", "Error while scanning", exception);
            }
        }, new DispatcherActivityProgress(DispatchPriority.Background), cts);

        this.IsScanRunning = false;
        this.scanCts = cts;
    }

    public void CancelScan() {
        try {
            this.scanCts?.Cancel();
        }
        catch (ObjectDisposedException) {
            // ignored
        }
    }

    private sealed class ThreadedScanningContext {
        public readonly ActivityTask ActivityTask;
        public readonly TaskCompletionSource completion;

        private readonly ConcurrentDictionary<uint, byte> nonPointers;

        private ulong totalValidAddresses;

        public ulong TotalValidAddresses => this.totalValidAddresses;

        public ThreadedScanningContext(ActivityTask activityTask, TaskCompletionSource completion, uint memoryDumpVirtualOffset, int memoryDumpSize) {
            this.ActivityTask = activityTask;
            this.completion = completion;
            this.nonPointers = new ConcurrentDictionary<uint, byte>(Environment.ProcessorCount * 2, 1000000);
        }

        public void IncrementTotalAddresses() => Interlocked.Increment(ref this.totalValidAddresses);

        public bool IsNonPointer(uint address) {
            // using (this.npmLock.EnterScope()) {
            //     if (address >= this.memoryDumpVirtualOffset && address < this.memoryDumpVirtualEndIndex) {
            //         bool isNonPointer = this.nonPointerMap[address - this.memoryDumpVirtualOffset];
            //         return isNonPointer;
            //     }
            //     return false;
            // }

            return this.nonPointers.ContainsKey(address);
        }

        public void AssignNonPointer(uint address) {
            // using (this.npmLock.EnterScope()) {
            //     if (address >= this.memoryDumpVirtualOffset && address < this.memoryDumpVirtualEndIndex) {
            //         this.nonPointerMap[address - this.memoryDumpVirtualOffset] = true;
            //     }
            // }

            this.nonPointers.TryAdd(address, 0 /* dummy empty entry */);
        }
    }

    private ref struct ScanningContext {
        private readonly Span<PointerPrivate> chain;
        public readonly ThreadedScanningContext thread;
        public uint virtualBaseAddr, maxDepth, alignment;
        public uint searchAddress;

        public int CurrentDepth { get; private set; }

        public PointerPrivate TopPointer => this.chain[this.CurrentDepth - 1];

        public Span<PointerPrivate> CurrentChain => this.chain.Slice(0, this.CurrentDepth);

        public ScanningContext(ThreadedScanningContext thread, Span<PointerPrivate> chain) {
            Debug.Assert(chain.Length == byte.MaxValue + 1);
            this.thread = thread;
            this.chain = chain;
        }

        public void PushFrame(PointerPrivate newBase) {
            this.chain[this.CurrentDepth++] = newBase;
        }

        public void PopFrame() {
            this.CurrentDepth--;
        }

        public void BeginScan(PointerPrivate newBase) {
            this.chain.Clear();
            this.PushFrame(newBase);
        }

        public void PushVisited(uint dstAddress) {
        }

        public void PopVisited(uint dstAddress) {
        }

        public bool HasVisited(uint dstAddress) {
            foreach (PointerPrivate p in this.CurrentChain) {
                if (p.value == dstAddress) {
                    return true;
                }
            }

            return false;
        }
    }

    private void ThreadedPointerScanMain(object? _param) {
        ThreadedScanningContext threadContext = (ThreadedScanningContext) _param!;
        if (this.alignedVirtualAddressToValue.Count < 1) {
            threadContext.completion.TrySetResult();
            return;
        }

        ActivityTask activity = threadContext.ActivityTask;
        activity.Progress.Caption = "Pointer scan";
        activity.Progress.Text = "Running full pointer scan...";
        using PopCompletionStateRangeToken range = activity.Progress.CompletionState.PushCompletionRange(0, 1.0 / this.alignedVirtualAddressToValue.Count);

        ParallelOptions options = new ParallelOptions {
            MaxDegreeOfParallelism = (int) (Environment.ProcessorCount / 4.0 * 3.0),
            CancellationToken = activity.CancellationToken
        };

        try {
            Parallel.ForEach(this.alignedVirtualAddressToValue.ToList(), options, RunScanInTask);
        }
        catch (AggregateException e) when (e.InnerExceptions.All(x => x is OperationCanceledException)) {
            threadContext.completion.TrySetCanceled(((OperationCanceledException) e.InnerExceptions.First()).CancellationToken);
            activity.TryCancel();
        }
        catch (OperationCanceledException e) {
            threadContext.completion.TrySetCanceled(e.CancellationToken);
            activity.TryCancel();
        }
        catch (Exception e) {
            threadContext.completion.TrySetException(e);
            activity.TryCancel();
        }

        threadContext.completion.TrySetResult();
        return;

        void RunScanInTask(KeyValuePair<uint, uint> entry) {
            try {
                Span<PointerPrivate> chain = stackalloc PointerPrivate[byte.MaxValue + 1];
                ScanningContext ctx = new ScanningContext(threadContext, chain) {
                    virtualBaseAddr = this.virtualBaseAddress, maxDepth = this.maxDepth, alignment = this.loadedAlignment,
                    searchAddress = this.searchAddress
                };

                ctx.BeginScan(new PointerPrivate(this.virtualBaseAddress, entry.Key - this.virtualBaseAddress, entry.Value));
                if (entry.Value == this.searchAddress) {
                    this.AddPointerResult(ctx.CurrentChain);
                }
                else if (this.maxDepth > 0) {
                    this.ScanRecursive(ref ctx);
                }

                activity.Progress.CompletionState.OnProgress(1);
            }
            catch (OperationCanceledException) {
                // ignored
            }
            catch (Exception e) {
                activity.TryCancel();
                Debugger.Break();
            }
        }
    }

    private void ScanRecursive(ref ScanningContext ctx) {
        ctx.thread.ActivityTask.ThrowIfCancellationRequested();

        uint unalignedAddressOffset = (ctx.TopPointer.value - ctx.virtualBaseAddr) / ctx.alignment;
        if (!this.unalignedOffsetToNearbyValidOffsets.TryGetValue(unalignedAddressOffset, out List<uint>? unalignedOffsets)) {
            // No offsets available to scan for the current pointer
            return;
        }

        for (int i = 0; i < unalignedOffsets.Count; i++) {
            uint alignedOffset = unalignedOffsets[i] * ctx.alignment;
            uint virtualAddress = ctx.TopPointer.value + alignedOffset;
            if (virtualAddress == ctx.searchAddress) {
                ctx.thread.IncrementTotalAddresses();
                this.AddPointerResult(ctx.CurrentChain, alignedOffset);
            }
            else {
                uint dstVirtualAddress = this.unalignedOffsetToValue![unalignedAddressOffset + unalignedOffsets[i]];
                if (dstVirtualAddress == ctx.searchAddress) {
                    ctx.thread.IncrementTotalAddresses();
                    this.AddPointerResult(ctx.CurrentChain, alignedOffset);
                }
                else if (ctx.CurrentDepth < ctx.maxDepth && !ctx.HasVisited(dstVirtualAddress)) {
                    ctx.PushVisited(dstVirtualAddress);
                    ctx.thread.IncrementTotalAddresses();
                    ctx.PushFrame(new PointerPrivate(ctx.TopPointer.value, alignedOffset, dstVirtualAddress));
                    this.ScanRecursive(ref ctx);
                    ctx.PopFrame();
                    ctx.PopVisited(dstVirtualAddress);
                }
            }
        }

        // for (uint offset = ctx.minimumOffset; offset <= maxAddressOffset; offset += ctx.alignment) {
        //     uint address = ctx.TopPointer.value + offset;
        //     if (address == ctx.searchAddress) {
        //         ctx.thread.IncrementTotalAddresses();
        //         this.AddPointerResult(ctx.CurrentChain, offset);
        //     }
        //     else if (!this.TryDereferenceVirtual(address, out uint dstAddress /* the address pointed to by srcAddress */)) {
        //         // ctx.thread.AssignNonPointer(address);
        //         continue;
        //     }
        //     else if (dstAddress == ctx.searchAddress) {
        //         ctx.thread.IncrementTotalAddresses();
        //         this.AddPointerResult(ctx.CurrentChain, offset);
        //     }
        //     else if (ctx.CurrentDepth < ctx.maxDepth && ctx.visitedAddresses.Add(dstAddress)) {
        //         ctx.thread.IncrementTotalAddresses();
        //         ctx.PushFrame(new PointerPrivate(ctx.TopPointer.value, offset, dstAddress));
        //         this.ScanRecursive(ref ctx, ctx.CurrentDepth == 1 ? this.secondaryMaximumOffset : this.primaryMaximumOffset);
        //         ctx.PopFrame();
        //
        //         ctx.visitedAddresses.Remove(dstAddress);
        //     }
        // }
    }

    private void AddPointerResult(Span<PointerPrivate> chain) {
        IEnumerable<int> offsets = chain.Length < 2
            ? ReadOnlyCollection<int>.Empty // only 1 pointer so no offets 
            : chain.Slice(1).ToArray().Select(x => (int) x.offset);

        this.AddPointerResult(new DynamicAddress(chain[0].addr + chain[0].offset, offsets));
    }

    private void AddPointerResult(Span<PointerPrivate> chain, uint offset) {
        IEnumerable<int> offsets = chain.Length < 2
            ? ReadOnlyCollection<int>.Empty // only 1 pointer so no offets 
            : chain.Slice(1).ToArray().Select(x => (int) x.offset);

        this.AddPointerResult(new DynamicAddress(chain[0].addr + chain[0].offset, offsets.Append((int) offset)));
    }

    private void AddPointerResult(DynamicAddress result) {
        ApplicationPFX.Instance.Dispatcher.Post(() => this.PointerChain.Add(result));
    }

    private bool TryReadU32(uint address, out uint value) {
        // Check address is actually addressable
        if (address >= this.addressableRange.@base && address <= this.addressableRange.end - sizeof(uint)) {
            if (address >= this.virtualBaseAddress) {
                uint bufferOffset = address - this.virtualBaseAddress; // within the memory dump file
                if (bufferOffset <= (this.memoryDump!.Length - sizeof(uint))) {
                    value = MemoryEngine.ReadValueFromBytes<uint>(this.memoryDump.AsSpan(unchecked((int) bufferOffset), sizeof(uint)), this.isMemoryLittleEndian);
                    return true;
                }
            }
        }

        value = 0;
        return false;
    }

    private bool TryDereferenceVirtual(uint virtualAddress, out uint value) {
        if (virtualAddress >= this.virtualBaseAddress) {
            return this.TryDereferenceAligned(virtualAddress - this.virtualBaseAddress, out value);
        }

        value = 0;
        return false;
    }

    private bool TryDereferenceAligned(uint offset, out uint value) {
        return this.TryDereferenceUnaligned(offset / this.loadedAlignment, out value);
    }

    private bool TryDereferenceUnaligned(uint offset, out uint value) {
        uint[]? array = this.unalignedOffsetToValue;
        if (offset < array!.Length) {
            return (value = array[offset]) != 0;
        }

        value = 0;
        return false;
    }

    public async Task LoadMemoryDump(string filePath, uint newVirtualBaseAddress, bool isLittleEndian, CancellationToken cancellation) {
        byte[] memory;
        try {
            memory = await File.ReadAllBytesAsync(filePath, cancellation);
        }
        catch (IOException) {
            throw new InvalidOperationException($"File too large. Cannot exceed {Math.Round(int.MaxValue / 1000000000.0, 2)} GB.");
        }

        this.memoryDump = memory;
        this.isMemoryLittleEndian = isLittleEndian;
        this.virtualBaseAddress = newVirtualBaseAddress;
        this.addressableRange = new AddressableRange(newVirtualBaseAddress, (uint) memory.Length);
        this.AddressableRangeChanged?.Invoke(this, EventArgs.Empty);
        this.Alignment = 4;
    }

    public async Task GenerateBasePointerMap(IActivityProgress progress, CancellationToken cancellation = default) {
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();

        if (this.HasPointerMap) {
            this.HasPointerMap = false;
            this.ClearPointerMap();
        }

        using var _ = progress.SaveState(default, "Pointer map", false);

        this.loadedAlignment = this.alignment;
        int align = (int) this.loadedAlignment;

        try {
            await Task.Run(() => {
                progress.Text = "Generating base-level pointer map...";

                // First resolve every single possible pointer in the address space
                Span<byte> dumpSpan = this.memoryDump.AsSpan();
                this.unalignedOffsetToValue = new uint[dumpSpan.Length / align];

                using (progress.CompletionState.PushCompletionRange(0, 1.0 / dumpSpan.Length)) {
                    bool bIsLittleEndian = this.isMemoryLittleEndian;
                    for (int i = 0, j = 0; i < dumpSpan.Length; i += align, j++) {
                        cancellation.ThrowIfCancellationRequested();

                        uint virtualAddress = this.virtualBaseAddress + (uint) i;
                        uint u32value = MemoryEngine.ReadValueFromBytes<uint>(dumpSpan.Slice(i, sizeof(uint)), bIsLittleEndian);
                        if (u32value != 0 && this.addressableRange.Contains(u32value)) {
                            this.alignedVirtualAddressToValue[virtualAddress] = u32value;
                            this.unalignedValidOffsets.Add((uint) j);
                            this.unalignedOffsetToValue[j] = u32value;
                        }

                        progress.CompletionState.OnProgress(align);
                    }
                }
            }, cancellation);

            await Task.Run(() => {
                using (progress.CompletionState.PushCompletionRange(0, 1.0 / this.unalignedValidOffsets.Count)) {
                    progress.Text = "Pre-computing valid offsets";
                    uint[] unalignedValueArray = this.unalignedOffsetToValue! ?? throw new Exception("Error");

                    foreach (uint unalignedValidOffset in this.unalignedValidOffsets) {
                        cancellation.ThrowIfCancellationRequested();
                        if (!this.unalignedOffsetToNearbyValidOffsets.TryGetValue(unalignedValidOffset, out List<uint>? unalignedOffsets)) {
                            this.unalignedOffsetToNearbyValidOffsets[unalignedValidOffset] = unalignedOffsets = new List<uint>(0);
                        }

                        ParallelOptions options = new ParallelOptions {
                            MaxDegreeOfParallelism = (int) (Environment.ProcessorCount / 8.0 * 7.0),
                            CancellationToken = cancellation
                        };

                        try {
                            Parallel.For(this.minimumOffset / align, this.maximumOffset / align, options, (i, state) => {
                                if (!state.ShouldExitCurrentIteration) {
                                    long offset = unalignedValidOffset + i;
                                    if (offset >= 0 && offset < unalignedValueArray.Length) {
                                        uint value = unalignedValueArray[offset];
                                        if (value != 0) {
                                            lock (unalignedOffsets) {
                                                unalignedOffsets.Add((uint) (ulong) i);
                                            }
                                        }
                                    }
                                }
                            });
                        }
                        catch (Exception e) {
                            return;
                        }

                        if (unalignedOffsets.Count < 1) {
                            this.unalignedOffsetToNearbyValidOffsets.Remove(unalignedValidOffset);
                        }

                        progress.CompletionState.OnProgress(1);
                    }
                }
            }, cancellation);
        }
        catch (Exception e) {
            Debug.Assert(e is OperationCanceledException);
            this.ClearPointerMap();
            return;
        }

        this.HasPointerMap = true;
    }

    public void DisposeMemoryDump() {
        this.memoryDump = null;
    }

    public void Clear() {
        this.ClearPointerMap();
        this.PointerChain.Clear();
    }

    private void ClearPointerMap() {
        this.alignedVirtualAddressToValue.Clear();
        this.unalignedOffsetToNearbyValidOffsets.Clear();
        this.unalignedValidOffsets.Clear();
        this.unalignedOffsetToValue = null;
    }
}