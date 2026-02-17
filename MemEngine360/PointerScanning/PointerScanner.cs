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

public class PointerScanner {
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
    private uint primaryMaximumOffset = 0x4000; // most structs/classes won't exceed 16KB 
    private uint secondaryMaximumOffset = 0x4000;
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
    /// Same as <see cref="PrimaryMaximumOffset"/> but for depths of >= 2
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
    /// say <c><![CDATA[ 0x8262AA00 +0xFc -> +0xEF2 ]]></c>, then it cannot be discovered, because 0xEF2 exceeds <see cref="PrimaryMaximumOffset"/>
    /// </para>
    /// <para>
    /// Therefore, ideally this value should be pretty big but not so big that the scanning takes the rest of the universe's lifetime to complete
    /// </para>
    /// </summary>
    public uint PrimaryMaximumOffset {
        get => this.primaryMaximumOffset;
        set => PropertyHelper.SetAndRaiseINE(ref this.primaryMaximumOffset, value, this, this.PrimaryMaximumOffsetChanged);
    }

    /// <summary>
    /// Same as <see cref="PrimaryMaximumOffset"/> but for depths of >= 2
    /// </summary>
    public uint SecondaryMaximumOffset {
        get => this.secondaryMaximumOffset;
        set => PropertyHelper.SetAndRaiseINE(ref this.secondaryMaximumOffset, value, this, this.SecondaryMaximumOffsetChanged);
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

    public IReadOnlyDictionary<uint, uint> PointerMap => this.basePointers_D;

    public event EventHandler? AddressableRangeChanged;
    public event EventHandler? MaxDepthChanged;
    public event EventHandler? MinimumOffsetChanged;
    public event EventHandler? PrimaryMaximumOffsetChanged;
    public event EventHandler? SecondaryMaximumOffsetChanged;
    public event EventHandler? SearchAddressChanged;
    public event EventHandler? AlignmentChanged;
    public event EventHandler? HasPointerMapChanged;
    public event EventHandler? IsScanRunningChanged;

    private byte[]? memoryDump;
    private readonly Dictionary<uint, uint> basePointers_D; // (base+offset) -> addr

    private readonly ConcurrentDictionary<uint, byte> nonPointers; // a set of pointers that do not resolve to the 

    // private readonly ConcurrentDictionary<uint, object> visitedPointers; // a set of pointers that have already been resolved 
    private bool isMemoryLittleEndian;
    private uint virtualBaseAddress; // The base address the user specified when loading the file
    private CancellationTokenSource? scanCts;

    public ObservableList<DynamicAddress> PointerChain { get; } = new ObservableList<DynamicAddress>();

    public MemoryEngine MemoryEngine { get; }

    private readonly struct PointerPrivate {
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
    }

    public PointerScanner(MemoryEngine memoryEngine) {
        this.MemoryEngine = memoryEngine;

        int procCount = Environment.ProcessorCount * 2;

        this.basePointers_D = new Dictionary<uint, uint>(200000);
        this.nonPointers = new ConcurrentDictionary<uint, byte>(procCount, 1000000);

        // this.visitedPointers = new ConcurrentDictionary<uint, object>(Environment.ProcessorCount, 100000);
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
            ThreadedScanningContext threadContext = new ThreadedScanningContext(ActivityTask.Current, tcs);
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
            timer.Interval = TimeSpan.FromSeconds(0.25);
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

    private sealed class ThreadedScanningContext(ActivityTask activityTask, TaskCompletionSource completion) {
        public readonly ActivityTask ActivityTask = activityTask;
        public readonly TaskCompletionSource completion = completion;
        private ulong totalValidAddresses;
        
        public ulong TotalValidAddresses => this.totalValidAddresses;

        public void IncrementTotalAddresses() => Interlocked.Increment(ref this.totalValidAddresses);
    }

    private ref struct ScanningContext {
        public readonly ThreadedScanningContext thread;
        private readonly Span<PointerPrivate> chain;
        private int chainTail;
        public int currentDepth;

        public PointerPrivate TopPointer => this.chain[this.chainTail - 1];

        public Span<PointerPrivate> CurrentChain => this.chain.Slice(0, this.chainTail);

        public ScanningContext(ThreadedScanningContext thread, Span<PointerPrivate> chain) {
            Debug.Assert(chain.Length == byte.MaxValue + 1);
            this.thread = thread;
            this.chain = chain;
        }

        public void PushFrame(PointerPrivate newBase) {
            this.chain[this.chainTail++] = newBase;
            this.currentDepth++;
        }

        public void PopFrame() {
            this.chainTail--;
        }

        public void BeginScan(PointerPrivate newBase) {
            this.chain.Clear();
            this.currentDepth = 0;
            this.PushFrame(newBase);
        }
    }

    /*
     *  Threaded pointer scanner
     *    The current implementation does not fully work, and gets random results each scan (release mode) or every
     *    restart of the app (debug mode, for some reason).
     *
     *  See PointerScannerBackup2 that has a working (at least AFAIK) but are of course very slow scanning algorithm.
     */
    private void ThreadedPointerScanMain(object? _param) {
        ThreadedScanningContext threadContext = (ThreadedScanningContext) _param!;
        if (this.basePointers_D.Count < 1) {
            threadContext.completion.TrySetResult();
            return;
        }

        ActivityTask activity = threadContext.ActivityTask;
        activity.Progress.Caption = "Pointer scan";
        activity.Progress.Text = "Running full pointer scan...";

        this.nonPointers.Clear();
        using PopCompletionStateRangeToken range = activity.Progress.CompletionState.PushCompletionRange(0, 1.0 / this.basePointers_D.Count);

        ParallelOptions options = new ParallelOptions {
            MaxDegreeOfParallelism = (int) (Environment.ProcessorCount / 4.0 * 3.0),
            CancellationToken = activity.CancellationToken
        };

        try {
            Parallel.ForEach(this.basePointers_D.ToList(), options, RunScanInTask);
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
                if (activity.IsCancellationRequested) {
                    return;
                }

                Span<PointerPrivate> chain = stackalloc PointerPrivate[byte.MaxValue + 1];
                ScanningContext ctx = new ScanningContext(threadContext, chain);
                ctx.BeginScan(new PointerPrivate(this.virtualBaseAddress, entry.Key - this.virtualBaseAddress, entry.Value));
                if (entry.Value == this.searchAddress) {
                    this.AddPointerResult(ctx.CurrentChain);
                }
                else if (this.maxDepth > 0) {
                    this.FindNearbyPointers(ref ctx, this.primaryMaximumOffset);
                }

                activity.Progress.CompletionState.OnProgress(1);
            }
            catch (OperationCanceledException) {
                // ignored
            }
            catch (Exception e) {
                Debugger.Break();
            }
        }
    }

    private void FindNearbyPointers(ref ScanningContext ctx, uint maxOffset) {
        ctx.thread.ActivityTask.ThrowIfCancellationRequested();

        for (uint offset = this.minimumOffset; offset <= maxOffset; offset += this.alignment) {
            uint address = ctx.TopPointer.value + offset;
            if (address == this.searchAddress) {
                ctx.thread.IncrementTotalAddresses();
                this.AddPointerResult(ctx.CurrentChain, offset);
            }
            else if (!this.nonPointers.ContainsKey(address)) {
                if (!this.basePointers_D.TryGetValue(address, out uint dstAddress /* the address pointed to by srcAddress */)) {
                    this.nonPointers.TryAdd(address, 0 /* dummy empty entry */);
                }
                else if (dstAddress == this.searchAddress) {
                    ctx.thread.IncrementTotalAddresses();
                    this.AddPointerResult(ctx.CurrentChain, offset);
                }
                else if (ctx.currentDepth < this.maxDepth && !this.nonPointers.ContainsKey(dstAddress)) {
                    ctx.thread.IncrementTotalAddresses();
                    ctx.PushFrame(new PointerPrivate(ctx.TopPointer.value, offset, dstAddress));
                    this.FindNearbyPointers(ref ctx, ctx.currentDepth == 1 ? this.secondaryMaximumOffset : this.primaryMaximumOffset);
                    ctx.PopFrame();
                }
            }
        }
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
        if (address >= this.addressableRange.@base && (address + sizeof(uint)) <= this.addressableRange.end) {
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
            this.basePointers_D.Clear();
        }

        progress.Caption = "Pointer map";
        progress.Text = "Generating base-level pointer map...";

        try {
            await Task.Run(() => {
                // First resolve every single possible pointer in the address space
                Span<byte> dumpSpan = this.memoryDump.AsSpan();

                using (progress.CompletionState.PushCompletionRange(0, 1.0 / dumpSpan.Length)) {
                    int align = (int) this.alignment;
                    bool bIsLittleEndian = this.isMemoryLittleEndian;
                    for (int i = 0; i < dumpSpan.Length; i += align) {
                        cancellation.ThrowIfCancellationRequested();

                        uint u32value = MemoryEngine.ReadValueFromBytes<uint>(dumpSpan.Slice(i, sizeof(uint)), bIsLittleEndian);
                        if (u32value != 0 && this.addressableRange.Contains(u32value)) {
                            this.basePointers_D[this.virtualBaseAddress + (uint) i] = u32value;
                        }

                        progress.CompletionState.OnProgress(this.alignment);
                    }
                }
            }, cancellation);
        }
        catch (Exception e) {
            Debug.Assert(e is OperationCanceledException);
            this.basePointers_D.Clear();
            return;
        }

        this.HasPointerMap = true;
    }

    public void DisposeMemoryDump() {
        this.memoryDump = null;
    }

    public void Clear() {
        this.basePointers_D.Clear();
        this.nonPointers.Clear();
        this.PointerChain.Clear();
    }
}