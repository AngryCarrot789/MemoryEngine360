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
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text;
using MemEngine360.Engine;
using PFXToolKitUI;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.PointerScanning;

public delegate void PointerScannerEventHandler(PointerScanner sender);

public class PointerScanner {
    private uint addressableBase;
    private uint addressableLength;
    private uint addressableEnd;
    private byte maxDepth = 6;
    private uint maximumOffset = 0x2000; // 8192 -- most structs in most games and programs probably won't exceed this
    private uint searchAddress;
    private uint alignment = 4;
    private bool hasPointerMap;

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
        set => PropertyHelper.SetAndRaiseINE(ref this.maximumOffset, value, this, static t => t.MaximumOffsetChanged?.Invoke(t));
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

    public IReadOnlyDictionary<uint, uint> PointerMap => this.basePointers;

    public event PointerScannerEventHandler? AddressableBaseChanged;
    public event PointerScannerEventHandler? AddressableLengthChanged;
    public event PointerScannerEventHandler? MaxDepthChanged;
    public event PointerScannerEventHandler? MaximumOffsetChanged;
    public event PointerScannerEventHandler? SearchAddressChanged;
    public event PointerScannerEventHandler? AlignmentChanged;
    public event PointerScannerEventHandler? HasPointerMapChanged;

    private IntPtr hMemoryDump;
    private IntPtr cbMemoryDump;
    private readonly SortedList<uint, uint> basePointers; // (base+offset) -> addr
    private readonly HashSet<uint> nonPointers; // a set of pointers that do not resolve to the 
    private readonly HashSet<uint> visitedPointers; // a set of pointers that have already been resolved 
    private bool isMemoryLittleEndian;
    private uint baseAddress;

    public ObservableList<ImmutableArray<Pointer>> PointerChain { get; } = new ObservableList<ImmutableArray<Pointer>>();

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

        public bool Resolve(PointerScanner scanner, out uint val) {
            return scanner.TryReadU32(this.addr + this.offset, out val);
        }

        public override string ToString() {
            return $"[{this.addr:X8} + {this.offset:X}] -> {this.value:X8}";
        }
    }

    public PointerScanner(MemoryEngine memoryEngine) {
        this.MemoryEngine = memoryEngine;
        this.basePointers = new SortedList<uint, uint>(100000);
        this.nonPointers = new HashSet<uint>(400000);
        this.visitedPointers = new HashSet<uint>(100000);
    }

    public async Task Run() {
        if (this.hMemoryDump == IntPtr.Zero) {
            throw new InvalidOperationException("Memory dump not loaded");
        }

        if (!this.HasPointerMap) {
            throw new InvalidOperationException("No pointer map loaded");
        }

        using CancellationTokenSource cts = new CancellationTokenSource();

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

            Task task = tcs.Task;
            while (!task.IsCompleted) {
                StringBuilder sb = new(64);
                (List<PointerPrivate> list, int depth)? currChain = options.currentChain;
                if (currChain.HasValue) {
                    List<PointerPrivate> chain = currChain.Value.list;
                    sb.Append(chain[0].addr.ToString("X8")).Append('+').Append(chain[0].offset.ToString("X"));
                    for (int i = 1; i < chain.Count; i++) {
                        sb.Append("->").Append(chain[i].offset.ToString("X"));
                    }

                    options.ActivityTask.Progress.Text = $"{sb} ({currChain.Value.depth} deep)";
                }

                await Task.Delay(100);
            }

            try {
                await tcs.Task;
            }
            catch (OperationCanceledException) {
                // ignored
            }
            catch (Exception e) {
                await IMessageDialogService.Instance.ShowMessage("Scan Error", "Error encountered while scanning", e.GetToString());
            }
        }, new DefaultProgressTracker(DispatchPriority.Background), cts, TaskCreationOptions.LongRunning);
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
            if (this.basePointers.Count > 0) {
                using (activity.Progress.CompletionState.PushCompletionRange(0, 1.0 / this.basePointers.Count)) {
                    List<Task> tasks = new List<Task>();

                    int split = (this.basePointers.Count / 10) + 1 /* CPU count */;
                    for (int i = 0; i < this.basePointers.Count; i += split) {
                        int idx = i, endIdx = Math.Min(i + split, this.basePointers.Count);
                        int count = endIdx - i;
                        List<KeyValuePair<uint, uint>> subList = this.basePointers.Skip(idx - 1).Take(count).ToList();
                        tasks.Add(Task.Run(() => {
                            foreach (KeyValuePair<uint, uint> entry in subList) {
                                activity.CheckCancelled();

                                PointerPrivate pBase = new PointerPrivate(this.baseAddress, entry.Key - this.baseAddress, entry.Value);
                                // if (pBase.offset > this.maximumOffset) {
                                //     continue;
                                // }

                                List<PointerPrivate> chain = new List<PointerPrivate>() { pBase };
                                if (pBase.value == this.searchAddress) {
                                    this.PointerChain.Add(chain.Select(x => new Pointer(x.addr, x.offset)).ToImmutableArray());
                                }
                                else if (0 < this.maxDepth) {
                                    this.FindNearbyPointers(chain, 1, options);
                                }

                                activity.Progress.CompletionState.OnProgress(1);
                            }
                        }));
                    }

                    Task.WhenAll(tasks).Wait();
                }
            }

            options.completion.SetResult();
        }
        catch (AggregateException e) when (e.InnerExceptions.All(x => x is OperationCanceledException)) {
            options.completion.SetCanceled(((OperationCanceledException) e.InnerExceptions.First()).CancellationToken);
        }
        catch (OperationCanceledException e) {
            options.completion.SetCanceled(e.CancellationToken);
        }
        catch (Exception e) {
            options.completion.SetException(e);
        }
    }

    private void FindNearbyPointers(List<PointerPrivate> chain, byte currDepth, PointerScanThreadOptions options) {
        options.ActivityTask.CheckCancelled();
        PointerPrivate basePtr = chain[chain.Count - 1];

        if (((DefaultProgressTracker) options.ActivityTask.Progress).HasTextUpdated) {
            options.currentChain = (chain, currDepth);
        }

        uint align = this.alignment;
        for (uint offset = 0; offset <= this.maximumOffset; offset += align) {
            // lock (this.nonPointers) {
            //     for (int i = chain.Count - 1; i >= 0; i--) {
            //         if (this.nonPointers.Contains(chain[i].value)) {
            //             return;
            //         }
            //     }   
            // }

            uint srcAddress = basePtr.value + offset;
            if (srcAddress == this.searchAddress) {
                this.PointerChain.Add(new List<PointerPrivate>(chain) {
                                          new PointerPrivate(basePtr.value, offset, this.TryReadU32(srcAddress, out uint value) ? value : 0)
                                      }.
                                      Select(x => new Pointer(x.addr, x.offset)).
                                      ToImmutableArray());
            }
            else {
                lock (this.nonPointers) {
                    if (this.nonPointers.Contains(srcAddress)) {
                        continue;
                    }   
                }

                if (this.basePointers.TryGetValue(srcAddress, out uint dstAddress /* the address pointed to by srcAddress */)) {
                    PointerPrivate dstPtr = new PointerPrivate(basePtr.value, offset, dstAddress);
                    if (dstAddress == this.searchAddress) {
                        this.PointerChain.Add(new List<PointerPrivate>(chain) { dstPtr }.
                                              Select(x => new Pointer(x.addr, x.offset)).
                                              ToImmutableArray());
                    }
                    else {
                        if (currDepth < this.maxDepth) {
                            bool state = true;
                            lock (this.nonPointers) {
                                if (this.nonPointers.Contains(dstAddress)) {
                                    state = false;
                                }
                            }

                            if (state) {
                                // if (this.visitedPointers.Add(srcAddress)) {
                                chain.Add(dstPtr);
                                this.FindNearbyPointers(chain, (byte) (currDepth + 1), options);
                                chain.RemoveAt(chain.Count - 1); // backtrack
                                // }
                            }
                        }
                    }
                }
                else {
                    lock (this.nonPointers) {
                        this.nonPointers.Add(srcAddress);
                    }
                }
            }
        }
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
        this.visitedPointers.Clear();
        this.PointerChain.Clear();
    }
}