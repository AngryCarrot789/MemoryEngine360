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

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using PFXToolKitUI;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;

namespace MemEngine360.PointerScanning;

public delegate void PointerScannerEventHandler(PointerScanner sender);

public class PointerScanner {
    private uint addressableBase;
    private uint addressableLength;
    private uint addressableEnd;
    private byte maxDepth = 3;
    private uint maximumOffset = 0x1000; // 4096 -- most structs in most games and programs probably won't exceed this
    private uint searchAddress;

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
    /// Gets or sets the maximum offset from a pointer that another pointer can be.
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
        set {
            // Align to 4 bytes
            value += value % 4;
            PropertyHelper.SetAndRaiseINE(ref this.maximumOffset, value, this, static t => t.MaximumOffsetChanged?.Invoke(t));
        }
    }

    /// <summary>
    /// Gets or sets the actual address we want to scan for, e.g. the memory address of an ammo count
    /// </summary>
    public uint SearchAddress {
        get => this.searchAddress;
        set => PropertyHelper.SetAndRaiseINE(ref this.searchAddress, value, this, static t => t.SearchAddressChanged?.Invoke(t));
    }

    public event PointerScannerEventHandler? AddressableBaseChanged;
    public event PointerScannerEventHandler? AddressableLengthChanged;
    public event PointerScannerEventHandler? MaxDepthChanged;
    public event PointerScannerEventHandler? MaximumOffsetChanged;
    public event PointerScannerEventHandler? SearchAddressChanged;

    private IntPtr hMemoryDump;
    private IntPtr cbMemoryDump;
    private readonly SortedList<uint, uint> allPointers; // (base+offset) -> addr
    private readonly HashSet<uint> nonPointers; // a set of pointers that do not resolve to the 
    private readonly HashSet<uint> visitedPointers; // a set of pointers that have already been resolved 
    private bool isMemoryLittleEndian;
    private uint baseAddress;
    private readonly List<List<Pointer>> pointerChain;

    public int PointerCount => this.allPointers.Count;

    private readonly struct Pointer {
        public readonly uint addr;
        public readonly uint offset;
        public readonly uint value;

        public Pointer(uint addr, uint offset, uint value) {
            this.addr = addr;
            this.offset = offset;
            this.value = value;
        }

        public bool Resolve(PointerScanner scanner, out uint value) {
            return scanner.TryReadU32(this.addr + this.offset, out value);
        }

        public override string ToString() {
            return $"[{this.addr:X8} + {this.offset:X}] -> {this.value:X8}";
        }
    }

    private class PointerList {
        public readonly byte level;
        public readonly SortedList<uint, uint> pointers;

        public PointerList(byte level) {
            this.level = level;
            this.pointers = new SortedList<uint, uint>();
        }
    }

    public PointerScanner() {
        this.allPointers = new SortedList<uint, uint>(100000);
        this.nonPointers = new HashSet<uint>(400000);
        this.visitedPointers = new HashSet<uint>(100000);
        this.pointerChain = new List<List<Pointer>>();
    }

    public async Task Run() {
        if (this.hMemoryDump == IntPtr.Zero) {
            throw new InvalidOperationException("Memory dump not loaded");
        }

        using CancellationTokenSource cts = new CancellationTokenSource();
        await ActivityManager.Instance.RunTask(async () => {
            ActivityTask activity = ActivityManager.Instance.CurrentTask;
            activity.Progress.Caption = "Pointer scan";
            activity.Progress.Text = "Discovering base pointers...";

            // First resolve every single possible pointer in the address space
            using (activity.Progress.CompletionState.PushCompletionRange(0, 1.0 / this.cbMemoryDump)) {
                for (IntPtr i = 0; i < this.cbMemoryDump; i += 4) {
                    uint u32value = (uint) Marshal.ReadInt32(this.hMemoryDump + i);
                    if (this.isMemoryLittleEndian != BitConverter.IsLittleEndian)
                        u32value = BinaryPrimitives.ReverseEndianness(u32value);

                    if (u32value >= this.addressableBase && u32value < this.addressableEnd)
                        this.allPointers.Add(this.baseAddress + (uint) i, u32value);

                    activity.Progress.CompletionState.OnProgress(4);
                }
            }

            using (activity.Progress.CompletionState.PushCompletionRange(0, 1.0 / this.allPointers.Count)) {
                foreach (KeyValuePair<uint, uint> entry in this.allPointers) {
                    Pointer pBase = new Pointer(this.baseAddress, entry.Key - this.baseAddress, entry.Value);
                    List<Pointer> chain = new List<Pointer>() { pBase };
                    if (pBase.value == this.searchAddress) {
                        this.pointerChain.Add(chain);
                    }
                    else if (0 < this.maxDepth) {
                        this.FindNearbyPointers(chain, 1, activity);
                    }

                    activity.Progress.CompletionState.OnProgress(1);
                }
            }
        }, new DefaultProgressTracker(DispatchPriority.Background), cts, TaskCreationOptions.LongRunning);
    }

    private void FindNearbyPointers(List<Pointer> chain, byte currDepth, ActivityTask activityTask) {
        activityTask.CheckCancelled();
        Pointer basePtr = chain[chain.Count - 1];
        
        if (((DefaultProgressTracker) activityTask.Progress).HasTextUpdated) {
            StringBuilder sb = new(64);
            sb.Append(chain[0].addr.ToString("X8")).Append('+').Append(chain[0].offset.ToString("X"));
            for (int i = 1; i < chain.Count; i++) {
                sb.Append("->").Append(chain[i].offset.ToString("X"));
            }

            activityTask.Progress.Text = $"{sb} ({currDepth} deep)";
        }
        
        for (uint offset = 0; offset <= this.maximumOffset; offset += 4) {
            foreach (Pointer ptr in chain) {
                if (this.nonPointers.Contains(ptr.value)) {
                    return;
                }
            }
            
            uint srcAddress = basePtr.value + offset;
            if (srcAddress == this.searchAddress) {
                this.pointerChain.Add(new List<Pointer>(chain) {new Pointer(basePtr.value, offset, this.TryReadU32(srcAddress, out uint value) ? value : 0)});
            }
            else if (!this.nonPointers.Contains(srcAddress)) {
                if (this.allPointers.TryGetValue(srcAddress, out uint dstAddress /* the address pointed to by srcAddress */)) {
                    Pointer dstPtr = new Pointer(basePtr.value, offset, dstAddress);
                    if (dstAddress == this.searchAddress) {
                        this.pointerChain.Add(new List<Pointer>(chain) { dstPtr });
                    }
                    else if (currDepth < this.maxDepth && !this.nonPointers.Contains(dstAddress)) {
                        if (this.visitedPointers.Add(srcAddress)) {
                            chain.Add(dstPtr);
                            this.FindNearbyPointers(chain, (byte) (currDepth + 1), activityTask);
                            chain.RemoveAt(chain.Count - 1); // backtrack
                        }
                    }
                }
                else {
                    this.nonPointers.Add(srcAddress);
                }
            }
        }
    }

    private bool TryReadU32(uint address, out uint value) {
        // Check address is actually addressable
        if (address >= this.addressableBase && (address + 4) <= this.addressableEnd) {
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
        using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read)) {
            long cbFs = fs.Length;
            if (cbFs > IntPtr.MaxValue) {
                throw new InvalidOperationException("File too large. Cannot exceed " + (IntPtr.MaxValue / 1000000000) + " GB.");
            }

            const int chunk = 0x10000;
            byte[] buffer = new byte[chunk];
            IntPtr count = checked((IntPtr) cbFs);
            IntPtr hMemory = IntPtr.Zero;
            IntPtr remaining = count;
            IntPtr offset = IntPtr.Zero;

            try {
                hMemory = Marshal.AllocHGlobal(count);
                while (remaining > 0) {
                    int cbRead = (int) Math.Min(chunk, remaining);
                    int read = await fs.ReadAsync(buffer.AsMemory(0, cbRead)).ConfigureAwait(false);
                    if (read == 0) {
                        break;
                    }

                    Debug.Assert((hMemory + offset + read) <= (hMemory + count));

                    Marshal.Copy(buffer, 0, hMemory + offset, read);
                    remaining -= read;
                    offset += read;
                }
            }
            catch (Exception) {
                if (hMemory != IntPtr.Zero) {
                    Marshal.FreeHGlobal(hMemory);
                }

                throw;
            }

            this.hMemoryDump = hMemory;
            this.cbMemoryDump = count - remaining;
            this.isMemoryLittleEndian = isLittleEndian;
            this.baseAddress = baseAddress;
        }
    }

    public void DisposeMemoryDump() {
        if (this.hMemoryDump != IntPtr.Zero) {
            Marshal.FreeHGlobal(this.hMemoryDump);
            this.hMemoryDump = IntPtr.Zero;
        }
    }
}