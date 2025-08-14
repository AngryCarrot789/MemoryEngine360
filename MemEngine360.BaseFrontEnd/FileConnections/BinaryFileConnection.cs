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

namespace MemEngine360.BaseFrontEnd.FileConnections;

/// <summary>
/// A console connection that is actually just a regular file on your PC. This is to debug features
/// </summary>
public sealed class BinaryFileConnection : BaseConsoleConnection {
    private FileStream? myFileStream;
    private readonly uint baseAddress;
    private readonly bool canResizeFile;

    public override RegisteredConnectionType ConnectionType => ConnectionTypeBinaryFile.Instance;

    public override bool IsLittleEndian { get; }

    public override AddressRange AddressableRange {
        get {
            ulong uFileLen = (ulong) Math.Max(this.FileLength /* assert >= 0 */, 0);
            uint length = (uint) Math.Max(this.baseAddress + uFileLen, uint.MaxValue) - this.baseAddress;
            return new AddressRange(this.baseAddress, length);
        }
    }

    public long FileLength {
        get {
            try {
                return this.myFileStream?.Length ?? 0;
            }
            catch {
                return 0;
            }
        }
    }

    public BinaryFileConnection(FileStream stream, uint baseAddress, bool canResizeFile, bool littleEndian) {
        this.myFileStream = stream;
        this.baseAddress = baseAddress;
        this.canResizeFile = canResizeFile;
        this.IsLittleEndian = littleEndian;
    }

    public override Task<bool?> IsMemoryInvalidOrProtected(uint address, uint count) {
        return Task.FromResult<bool?>(null);
    }

    protected override void CloseOverride() {
        try {
            this.myFileStream?.Close();
            this.myFileStream?.Dispose();
        }
        finally {
            this.myFileStream = null;
        }
    }

    protected override async Task ReadBytesCore(uint address, byte[] dstBuffer, int offset, int count) {
        Debug.Assert(this.myFileStream != null);

        int read = 0;
        long seekTo = (long) address - this.baseAddress;
        if (seekTo < 0) {
            uint under = (uint) -seekTo;
            if (under >= count) {
                dstBuffer.AsSpan(offset, count).Clear();
                return;
            }

            dstBuffer.AsSpan(offset, (int) under - offset).Clear();

            offset += (int) under;
            count -= (int) under;
            seekTo = 0;
        }

        if (seekTo < this.FileLength) {
            Memory<byte> memory = dstBuffer.AsMemory(offset, count);
            try {
                this.myFileStream.Seek(seekTo, SeekOrigin.Begin);
                read = await this.myFileStream.ReadAsync(memory);
            }
            catch (IOException) {
                throw;
            }
            catch (ObjectDisposedException) {
                this.Close();
                throw new IOException("File stream closed");
            }
            catch {
                // ignored
            }
        }

        dstBuffer.AsSpan(offset + read, count - read).Clear();
    }

    protected override async Task WriteBytesCore(uint address, byte[] srcBuffer, int offset, int count) {
        Debug.Assert(this.myFileStream != null);
        
        // This method only writes the section of srcBuffer that intersects the
        // valid region between baseAddress and FileLength
        
        long fileLen = this.FileLength;
        long seekTo = (long) address - this.baseAddress;
        if (seekTo < 0) {
            uint under = (uint) -seekTo;
            if (under >= count) {
                return;
            }

            offset += (int) under;
            count -= (int) under;
            seekTo = 0;
        }

        if (seekTo < fileLen) {
            try {
                this.myFileStream.Seek(seekTo, SeekOrigin.Begin);
                if (!this.canResizeFile) {
                    count = (int) Math.Max(Math.Min(seekTo + count, fileLen) - seekTo, 0);
                }

                await this.myFileStream.WriteAsync(new ReadOnlyMemory<byte>(srcBuffer, offset, count));
                await this.myFileStream.FlushAsync();
            }
            catch (IOException) {
                throw;
            }
            catch (ObjectDisposedException) {
                this.Close();
                throw new IOException("File stream closed");
            }
            catch {
                // ignored
            }
        }
    }
}