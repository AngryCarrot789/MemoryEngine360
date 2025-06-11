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

namespace MemEngine360.Connections.Testing;

public class TestConsoleConnection : BaseConsoleConnection {
    public override RegisteredConnectionType ConnectionType => ConnectionTypeTest.Instance;

    protected override bool IsConnectedCore => !this.IsClosed;

    public override bool IsLittleEndian => BitConverter.IsLittleEndian;
    
    public override Task<bool?> IsMemoryInvalidOrProtected(uint address, uint count) {
        throw new TimeoutException();
    }

    protected override Task CloseCore() {
        return Task.CompletedTask;
    }

    protected override Task ReadBytesCore(uint address, byte[] dstBuffer, int offset, int count) {
        throw new TimeoutException();
    }

    protected override Task WriteBytesCore(uint address, byte[] srcBuffer, int offset, int count) {
        throw new TimeoutException();
    }
}