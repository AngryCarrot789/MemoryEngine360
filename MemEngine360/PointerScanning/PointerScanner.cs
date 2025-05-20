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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.PointerScanning;

public delegate void PointerScannerEventHandler(PointerScanner sender);

public class PointerScanner {
    private uint addressableBase;
    private uint addressableLength;
    private uint addressableEnd;
    private uint maxDepth;
    private uint targetAddress;

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
    /// Gets the maximum depth a pointer can be, as in, the max amount of offsets there can be to reach <see cref="TargetAddress"/>
    /// </summary>
    public uint MaxDepth {
        get => this.maxDepth;
        set {
            if (this.maxDepth != value) {
                this.maxDepth = value;
                this.MaxDepthChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Gets or sets the actual address we want to scan for, e.g. the memory address of an ammo count
    /// </summary>
    public uint TargetAddress {
        get => this.targetAddress;
        set {
            if (this.targetAddress != value) {
                this.targetAddress = value;
                this.TargetAddressChanged?.Invoke(this);
            }
        }
    }

    public event PointerScannerEventHandler? AddressableBaseChanged;
    public event PointerScannerEventHandler? AddressableLengthChanged;
    public event PointerScannerEventHandler? MaxDepthChanged;
    public event PointerScannerEventHandler? AlignChanged;
    public event PointerScannerEventHandler? TargetAddressChanged;

    private IntPtr hMemoryDump;
    private IntPtr cbMemoryDump;
    private readonly List<uint> pointerList;
    private readonly SortedList<uint, uint> pointers;
    private bool isLittleEndian;
    private uint baseAddress;

    public int PointerCount => this.pointerList.Count;

    public PointerScanner() {
        this.pointerList = new List<uint>();
        this.pointers = new SortedList<uint, uint>();
    }

    public async Task Run() {
        IntPtr hMemDumpEnd = this.hMemoryDump + this.cbMemoryDump;
        IntPtr remaining = this.cbMemoryDump;
        const int chunk = 0x1000;
        byte[] buffer = new byte[chunk];
        uint j = 0;
        for (IntPtr hMem = this.hMemoryDump; hMem < hMemDumpEnd; hMem += chunk, j += chunk) {
            int cbBuffer = (int) Math.Min(chunk, remaining);
            Marshal.Copy(hMem, buffer, 0, cbBuffer);

            for (uint i = 0; i < chunk; i += 4) {
                uint address = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), i));
                if (this.isLittleEndian != BitConverter.IsLittleEndian)
                    address = BinaryPrimitives.ReverseEndianness(address);
                
                if (address >= this.addressableBase && address < this.addressableEnd)
                    this.pointers[this.baseAddress + j + i] = address; 
            }
            
            remaining -= cbBuffer;
        }
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
            this.isLittleEndian = isLittleEndian;
            this.baseAddress = baseAddress;
        }
    }

    private void DisposeMemoryDump() {
        if (this.hMemoryDump != IntPtr.Zero) {
            Marshal.FreeHGlobal(this.hMemoryDump);
            this.hMemoryDump = IntPtr.Zero;
        }
    }
}