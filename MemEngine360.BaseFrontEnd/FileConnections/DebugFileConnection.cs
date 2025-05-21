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

namespace MemEngine360.BaseFrontEnd.FileConnections;

/// <summary>
/// A console connection that is actually just a regular file on your PC. This is to debug features
/// </summary>
public class DebugFileConnection : BaseConsoleConnection {
    private FileStream? myFileStream;

    public DebugFileConnection(FileStream stream) {
        this.myFileStream = stream;
    }

    public override RegisteredConsoleType ConsoleType => DebuggingFileConsoleType.Instance;

    protected override bool IsConnectedCore => this.myFileStream != null;
    
    public override bool IsLittleEndian => BitConverter.IsLittleEndian;
    
    protected override void CloseCore() {
        try {
            this.myFileStream?.Close();
            this.myFileStream?.Dispose();
        }
        finally {
            this.myFileStream = null;
        }
    }

    protected override async Task<uint> ReadBytesCore(uint address, byte[] dstBuffer, int offset, uint count) {
        Debug.Assert(this.myFileStream != null);
        try {
            this.myFileStream.Seek(address, SeekOrigin.Begin);
            return (uint) await this.myFileStream.ReadAsync(dstBuffer.AsMemory(offset, (int) count));
        }
        catch {
            return 0U;
        }
    }

    protected override async Task<uint> WriteBytesCore(uint address, byte[] srcBuffer, int offset, uint count) {
        Debug.Assert(this.myFileStream != null);
        try {
            this.myFileStream.Seek(address, SeekOrigin.Begin);
            await this.myFileStream.WriteAsync(new ReadOnlyMemory<byte>(srcBuffer, offset, (int) count));
            await this.myFileStream.FlushAsync();
            return count;
        }
        catch {
            return 0U;
        }
    }
}