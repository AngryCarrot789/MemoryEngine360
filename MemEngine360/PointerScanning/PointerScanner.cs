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

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MemEngine360.Engine;
using MemEngine360.Engine.Addressing;
using PFXToolKitUI;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.PointerScanning;

public delegate void PointerScannerEventHandler(PointerScanner sender);

public class PointerScanner {
    private readonly object DummyNonNull = new object();

    private uint addressableBase;
    private uint addressableLength;
    private uint addressableEnd;
    private byte maxDepth = 6;
    private uint minimumOffset = 4; // using 4 might help with avoiding linked lists 
    private uint primaryMaximumOffset = 0x4000; // most structs/classes won't exceed 16KB 
    private uint secondaryMaximumOffset = 0x4000;
    private uint searchAddress;
    private uint alignment = 4;
    private bool hasPointerMap;
    private bool isScanRunning;

    /// <summary>
    /// Gets the base address of the addressable memory space, as in, the smallest address a pointer can be
    /// </summary>
    public uint AddressableBase {
        get => this.addressableBase;
        set {
            if (this.addressableBase != value) {
                this.addressableBase = value;
                this.addressableEnd = value + this.addressableLength;
                this.AddressableBaseChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Gets the number of bytes (relative to <see cref="AddressableBase"/>) that can be scanned as a potential pointer
    /// </summary>
    public uint AddressableLength {
        get => this.addressableLength;
        set {
            if (this.addressableLength != value) {
                this.addressableLength = value;
                this.addressableEnd = this.addressableBase + value;
                this.AddressableLengthChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Gets the maximum depth a pointer can be, as in, the max amount of offsets there can be to reach <see cref="SearchAddress"/>
    /// </summary>
    public byte MaxDepth {
        get => this.maxDepth;
        set => PropertyHelper.SetAndRaiseINE(ref this.maxDepth, value, this, static t => t.MaxDepthChanged?.Invoke(t));
    }

    /// <summary>
    /// Same as <see cref="PrimaryMaximumOffset"/> but for depths of >= 2
    /// </summary>
    public uint MinimumOffset {
        get => this.minimumOffset;
        set => PropertyHelper.SetAndRaiseINE(ref this.minimumOffset, value, this, static t => t.MinimumOffsetChanged?.Invoke(t));
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
        set => PropertyHelper.SetAndRaiseINE(ref this.primaryMaximumOffset, value, this, static t => t.PrimaryMaximumOffsetChanged?.Invoke(t));
    }

    /// <summary>
    /// Same as <see cref="PrimaryMaximumOffset"/> but for depths of >= 2
    /// </summary>
    public uint SecondaryMaximumOffset {
        get => this.secondaryMaximumOffset;
        set => PropertyHelper.SetAndRaiseINE(ref this.secondaryMaximumOffset, value, this, static t => t.SecondaryMaximumOffsetChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets the actual address we want to scan for, e.g. the memory address of an ammo count
    /// </summary>
    public uint SearchAddress {
        get => this.searchAddress;
        set => PropertyHelper.SetAndRaiseINE(ref this.searchAddress, value, this, static t => t.SearchAddressChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets the alignment for pointer types. Default is 4, since the xbox 360 (apparently) uses this for the size of words.
    /// </summary>
    public uint Alignment {
        get => this.alignment;
        set => PropertyHelper.SetAndRaiseINE(ref this.alignment, value, this, static t => t.AlignmentChanged?.Invoke(t));
    }

    public bool HasPointerMap {
        get => this.hasPointerMap;
        private set => PropertyHelper.SetAndRaiseINE(ref this.hasPointerMap, value, this, static t => t.HasPointerMapChanged?.Invoke(t));
    }

    public bool IsScanRunning {
        get => this.isScanRunning;
        private set => PropertyHelper.SetAndRaiseINE(ref this.isScanRunning, value, this, static t => t.IsScanRunningChanged?.Invoke(t));
    }

    public IReadOnlyDictionary<uint, uint> PointerMap => this.basePointers;

    public event PointerScannerEventHandler? AddressableBaseChanged;
    public event PointerScannerEventHandler? AddressableLengthChanged;
    public event PointerScannerEventHandler? MaxDepthChanged;
    public event PointerScannerEventHandler? MinimumOffsetChanged;
    public event PointerScannerEventHandler? PrimaryMaximumOffsetChanged;
    public event PointerScannerEventHandler? SecondaryMaximumOffsetChanged;
    public event PointerScannerEventHandler? SearchAddressChanged;
    public event PointerScannerEventHandler? AlignmentChanged;
    public event PointerScannerEventHandler? HasPointerMapChanged;
    public event PointerScannerEventHandler? IsScanRunningChanged;

    private IntPtr hMemoryDump;
    private IntPtr cbMemoryDump;
    private readonly SortedList<uint, uint> basePointers; // (base+offset) -> addr

    private readonly ConcurrentDictionary<uint, object> nonPointers; // a set of pointers that do not resolve to the 

    // private readonly ConcurrentDictionary<uint, object> visitedPointers; // a set of pointers that have already been resolved 
    private bool isMemoryLittleEndian;
    private uint baseAddress;
    private CancellationTokenSource? scanCts;

    public ObservableList<DynamicAddress> PointerChain { get; } = new ObservableList<DynamicAddress>();

    public MemoryEngine MemoryEngine { get; }

    private readonly struct PointerPrivate {
        public readonly uint addr;
        public readonly uint offset;
        public readonly uint value;

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
        this.basePointers = new SortedList<uint, uint>(100000);
        this.nonPointers = new ConcurrentDictionary<uint, object>(Environment.ProcessorCount, 400000);
        // this.visitedPointers = new ConcurrentDictionary<uint, object>(Environment.ProcessorCount, 100000);
    }

    public async Task Run() {
        if (this.hMemoryDump == IntPtr.Zero) {
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
            PointerScanThreadOptions options = new PointerScanThreadOptions(ActivityManager.Instance.CurrentTask, tcs);
            Thread thread = new Thread(this.ThreadedPointerScanMain) {
                Name = "Pointer Scan Thread",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };

            try {
                thread.Start(options);
            }
            catch (Exception e) {
                await IMessageDialogService.Instance.ShowMessage("Thread Error", "Error starting thread", e.GetToString());
                return;
            }

            try {
                while (!tcs.Task.IsCompleted) {
                    (List<PointerPrivate> list, int depth)? currChain = options.currentChain;
                    if (currChain.HasValue) {
                        List<PointerPrivate> chain;
                        try {
                            chain = currChain.Value.list.ToList();
                        }
                        catch (Exception) {
                            // possible concurrent modification exception
                            continue;
                        }

                        if (chain.Count > 1) {
                            DynamicAddress address = new DynamicAddress(chain[0].addr + chain[0].offset, chain.Skip(1).Select(x => (int) x.offset));
                            options.ActivityTask.Progress.Text = $"{address} ({currChain.Value.depth} deep)";
                        }
                        else if (chain.Count > 0) {
                            options.ActivityTask.Progress.Text = $"{(chain[0].addr + chain[0].offset):X8} ({currChain.Value.depth} deep)";
                        }
                        else {
                            options.ActivityTask.Progress.Text = $"Idle...";
                        }
                    }

                    await Task.Delay(100, cts.Token);
                }
            }
            catch (OperationCanceledException) {
            }
            catch (Exception e) { // oops...
                Debugger.Break();
            }

            try {
                await tcs.Task;
            }
            catch (OperationCanceledException) {
                // ignored
            }
            catch (Exception e) {
                await LogExceptionHelper.ShowMessageAndPrintToLogs("Pointer Scan Eror", "Error while scanning", e);
            }
        }, new ConcurrentActivityProgress(DispatchPriority.Background), cts);
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

    private sealed class PointerScanThreadOptions {
        public readonly ActivityTask ActivityTask;
        public readonly TaskCompletionSource completion;
        public (List<PointerPrivate> list, int depth)? currentChain;

        public PointerScanThreadOptions(ActivityTask activityTask, TaskCompletionSource completion) {
            this.ActivityTask = activityTask;
            this.completion = completion;
        }
    }

    private void ThreadedPointerScanMain(object? _param) {
        PointerScanThreadOptions options = (PointerScanThreadOptions) _param!;
        ActivityTask activity = options.ActivityTask;
        activity.Progress.Caption = "Pointer scan";
        activity.Progress.Text = "Running full pointer scan...";

        try {
            this.nonPointers.Clear();
            if (this.basePointers.Count > 0) {
                using (activity.Progress.CompletionState.PushCompletionRange(0, 1.0 / this.basePointers.Count)) {
                    List<Task> tasks = new List<Task>();

                    int split = (this.basePointers.Count / (Environment.ProcessorCount / 2)) + 1 /* CPU count / 2 */;
                    for (int i = 0; i < this.basePointers.Count; i += split) {
                        int idx = i, endIdx = Math.Min(i + split, this.basePointers.Count);
                        int count = endIdx - i;
                        List<KeyValuePair<uint, uint>> subList = this.basePointers.Skip(idx - 1).Take(count).ToList();
                        tasks.Add(Task.Factory.StartNew(() => {
                            foreach (KeyValuePair<uint, uint> entry in subList) {
#if DEBUG
                                try {
#endif
                                    activity.CheckCancelled();

                                    PointerPrivate pBase = new PointerPrivate(this.baseAddress, entry.Key - this.baseAddress, entry.Value);
                                    // if (pBase.offset > this.maximumOffset) {
                                    //     continue;
                                    // }

                                    List<PointerPrivate> chain = new List<PointerPrivate>() { pBase };
                                    if (pBase.value == this.searchAddress) {
                                        this.AddPointerResult(new DynamicAddress(chain[0].addr + chain[0].offset, chain.Skip(1).Select(x => (int) x.offset)));
                                    }
                                    else if (0 < this.maxDepth) {
                                        this.FindNearbyPointers(chain, 1, this.primaryMaximumOffset, options);
                                    }

                                    activity.Progress.CompletionState.OnProgress(1);
#if DEBUG
                                }
                                catch (OperationCanceledException) {
                                    throw;
                                }
                                catch (Exception e) {
                                    Debugger.Break();
                                    throw;
                                }
#endif
                            }
                        }, TaskCreationOptions.LongRunning));
                    }

                    Task.WhenAll(tasks).Wait();
                }
            }

            options.completion.TrySetResult();
        }
        catch (AggregateException e) when (e.InnerExceptions.All(x => x is OperationCanceledException)) {
            options.completion.TrySetCanceled(((OperationCanceledException) e.InnerExceptions.First()).CancellationToken);
            activity.TryCancel();
        }
        catch (OperationCanceledException e) {
            options.completion.TrySetCanceled(e.CancellationToken);
            activity.TryCancel();
        }
        catch (Exception e) {
            options.completion.TrySetException(e);
            activity.TryCancel();
        }
    }

    private void FindNearbyPointers(List<PointerPrivate> chain, byte currDepth, uint maxOffset, PointerScanThreadOptions options) {
        options.ActivityTask.CheckCancelled();
        PointerPrivate basePtr = chain[chain.Count - 1];

        if (((ConcurrentActivityProgress) options.ActivityTask.Progress).IsTextClean) {
            options.currentChain = (chain, currDepth);
        }

        uint align = this.alignment;
        for (uint offset = this.minimumOffset; offset <= maxOffset; offset += align) {
            for (int i = chain.Count - 1; i >= 0; i--) {
                if (this.nonPointers.ContainsKey(chain[i].value)) {
                    return;
                }
            }

            uint srcAddress = basePtr.value + offset;
            if (srcAddress == this.searchAddress) {
                this.AddPointerResult(new DynamicAddress(chain[0].addr + chain[0].offset, chain.Skip(1).Select(x => (int) x.offset).Append((int) offset)));
            }
            else {
                if (this.nonPointers.ContainsKey(srcAddress)) {
                    continue;
                }

                if (this.basePointers.TryGetValue(srcAddress, out uint dstAddress /* the address pointed to by srcAddress */)) {
                    PointerPrivate dstPtr = new PointerPrivate(basePtr.value, offset, dstAddress);
                    if (dstAddress == this.searchAddress) {
                        this.AddPointerResult(new DynamicAddress(chain[0].addr + chain[0].offset, chain.Skip(1).Select(x => (int) x.offset).Append((int) dstPtr.offset)));
                    }
                    else if (currDepth < this.maxDepth) {
                        if (!this.nonPointers.ContainsKey(dstAddress)) {
                            chain.Add(dstPtr);
                            this.FindNearbyPointers(chain, (byte) (currDepth + 1), currDepth == 1 ? this.secondaryMaximumOffset : this.primaryMaximumOffset, options);
                            chain.RemoveAt(chain.Count - 1); // backtrack
                        }
                    }
                }
                else {
                    this.nonPointers[srcAddress] = this.DummyNonNull;
                }
            }
        }
    }

    private void AddPointerResult(DynamicAddress result) {
        ApplicationPFX.Instance.Dispatcher.Invoke(() => this.PointerChain.Add(result));
    }

    private bool TryReadU32(uint address, out uint value) {
        // Check address is actually addressable
        if (address >= this.addressableBase && (address + sizeof(uint)) <= this.addressableEnd) {
            IntPtr offset = (IntPtr) (address - this.baseAddress); // within the memory dump file
            // Check offset is within range of the memory dump
            if (offset >= 0 && offset < this.cbMemoryDump) {
                value = (uint) Marshal.ReadInt32(this.hMemoryDump + offset);
                if (this.isMemoryLittleEndian != BitConverter.IsLittleEndian)
                    value = BinaryPrimitives.ReverseEndianness(value); // flip endianness for BE data

                return true;
            }
        }

        value = 0;
        return false;
    }

    public async Task LoadMemoryDump(string filePath, uint baseAddress, bool isLittleEndian) {
        this.DisposeMemoryDump();
        await using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        long cbFs = fs.Length;
        if (cbFs > IntPtr.MaxValue) {
            throw new InvalidOperationException($"File too large. Cannot exceed {Math.Round(IntPtr.MaxValue / 1000000000.0, 2)} GB.");
        }

        const int chunk = 0x10000;
        byte[] buffer = new byte[chunk];

        IntPtr fileSize = checked((IntPtr) cbFs);
        IntPtr hMemory = 0;
        IntPtr totalRead = 0;

        try {
            hMemory = Marshal.AllocHGlobal(fileSize);
            await Task.Run(async () => {
                while (totalRead < fileSize) {
                    int cbRead = (int) Math.Min(chunk, fileSize - totalRead);
                    int read = await fs.ReadAsync(buffer.AsMemory(0, cbRead)).ConfigureAwait(false);
                    if (read == 0) {
                        break;
                    }

                    Marshal.Copy(buffer, 0, hMemory + totalRead, read);
                    totalRead += read;
                }
            });
        }
        catch (Exception) {
            if (hMemory != IntPtr.Zero) {
                Marshal.FreeHGlobal(hMemory);
            }

            throw;
        }

        this.hMemoryDump = hMemory;
        this.cbMemoryDump = totalRead;
        this.isMemoryLittleEndian = isLittleEndian;
        this.baseAddress = baseAddress;

        this.AddressableBase = baseAddress;
        this.AddressableLength = (uint) totalRead;
        this.Alignment = 4;
    }

    public async Task GenerateBasePointerMap(IActivityProgress progress, CancellationToken cancellation = default) {
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();

        if (this.HasPointerMap) {
            this.HasPointerMap = false;
            this.basePointers.Clear();
        }

        progress.Caption = "Pointer map";
        progress.Text = "Generating base-level pointer map...";

        try {
            await Task.Run(() => {
                // First resolve every single possible pointer in the address space
                using (progress.CompletionState.PushCompletionRange(0, 1.0 / this.cbMemoryDump)) {
                    uint align = this.alignment;
                    bool bIsLittleEndian = this.isMemoryLittleEndian;
                    for (uint i = 0; i < this.cbMemoryDump; i += align) {
                        cancellation.ThrowIfCancellationRequested();

                        uint u32value = (uint) Marshal.ReadInt32((IntPtr) (this.hMemoryDump + i));
                        if (bIsLittleEndian != BitConverter.IsLittleEndian) {
                            u32value = BinaryPrimitives.ReverseEndianness(u32value);
                        }

                        if (u32value != 0 && u32value >= this.addressableBase && u32value < this.addressableEnd) {
                            this.basePointers.Add(this.baseAddress + i, u32value);
                        }

                        progress.CompletionState.OnProgress(this.alignment);
                    }
                }
            }, cancellation);
        }
        catch (OperationCanceledException) {
            this.basePointers.Clear();
            return;
        }

        this.HasPointerMap = true;
    }

    public void DisposeMemoryDump() {
        if (this.hMemoryDump != IntPtr.Zero) {
            Marshal.FreeHGlobal(this.hMemoryDump);
            this.hMemoryDump = IntPtr.Zero;
        }
    }

    public void Clear() {
        this.basePointers.Clear();
        this.nonPointers.Clear();
        // this.visitedPointers.Clear();
        this.PointerChain.Clear();
    }
}