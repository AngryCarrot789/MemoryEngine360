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
public class DebugFileConnection : BaseConsoleConnection {
    private FileStream? myFileStream;

    public DebugFileConnection(FileStream stream) {
        this.myFileStream = stream;
    }

    public override RegisteredConnectionType ConnectionType => ConnectionTypeDebugFile.Instance;

    protected override bool IsConnectedCore => this.myFileStream != null;
    
    public override bool IsLittleEndian => BitConverter.IsLittleEndian;

    public override Task<bool?> IsMemoryInvalidOrProtected(uint address, uint count) {
        return Task.FromResult<bool?>(null);
    }

    protected override Task CloseCore() {
        try {
            this.myFileStream?.Close();
            this.myFileStream?.Dispose();
        }
        finally {
            this.myFileStream = null;
        }

        return Task.CompletedTask;
    }

    protected override async Task ReadBytesCore(uint address, byte[] dstBuffer, int offset, int count) {
        Debug.Assert(this.myFileStream != null);
        try {
            this.myFileStream.Seek(address, SeekOrigin.Begin);
            int read = await this.myFileStream.ReadAsync(dstBuffer.AsMemory(offset, count));
            for (int i = read; i < count; i++)
                dstBuffer[i] = 0;
        }
        catch {
            // ignored
        }
    }

    protected override async Task WriteBytesCore(uint address, byte[] srcBuffer, int offset, int count) {
        Debug.Assert(this.myFileStream != null);
        try {
            this.myFileStream.Seek(address, SeekOrigin.Begin);
            await this.myFileStream.WriteAsync(new ReadOnlyMemory<byte>(srcBuffer, offset, count));
            await this.myFileStream.FlushAsync();
        }
        catch {
            // ignored
        }
    }
}