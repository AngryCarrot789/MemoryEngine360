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
    private readonly TestConnectionMode mode;

    public override RegisteredConnectionType ConnectionType => ConnectionTypeTest.Instance;

    protected override bool IsConnectedCore => !this.IsClosed;

    public override bool IsLittleEndian => BitConverter.IsLittleEndian;
    
    public override AddressRange AddressableRange { get; } = new AddressRange(0, uint.MaxValue);

    public TestConsoleConnection(TestConnectionMode mode) {
        this.mode = mode;
    }

    public override Task<bool?> IsMemoryInvalidOrProtected(uint address, uint count) {
        return this.mode switch {
            TestConnectionMode.TimeoutError => Task.FromException<bool?>(new TimeoutException("Test timeout exception")), 
            TestConnectionMode.IOError => Task.FromException<bool?>(new IOException("Test IO error")), 
            _ => Task.FromResult<bool?>(null)
        };
    }

    protected override Task CloseCore() {
        return Task.CompletedTask;
    }

    protected override Task ReadBytesCore(uint address, byte[] dstBuffer, int offset, int count) => this.GetTask();

    protected override Task WriteBytesCore(uint address, byte[] srcBuffer, int offset, int count) => this.GetTask();
    
    private Task GetTask() {
        return this.mode switch {
            TestConnectionMode.TimeoutError => Task.FromException(new TimeoutException("Test timeout exception")), 
            TestConnectionMode.IOError => Task.FromException(new IOException("Test IO error")), 
            _ => Task.CompletedTask
        };
    }
}

public enum TestConnectionMode {
    DoNothing,
    TimeoutError,
    IOError
}