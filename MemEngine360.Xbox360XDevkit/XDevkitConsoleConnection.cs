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

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using MemEngine360.Connections;
using MemEngine360.Connections.Traits;
using XDevkit;

namespace MemEngine360.Xbox360XDevkit;

public class XDevkitConsoleConnection : BaseConsoleConnection, IConsoleConnection, IHaveIceCubes, IHaveMemoryRegions {
    private readonly XboxManager manager;
    private readonly XboxConsole console;
    private bool isConnectedAsDebugger;

    public XboxConsole Console => this.console;

    public override RegisteredConnectionType ConnectionType => ConnectionTypeXbox360XDevkit.Instance;

    protected override bool IsConnectedCore => this.isConnectedAsDebugger;

    public override bool IsLittleEndian => false;
    
    public override AddressRange AddressableRange => new AddressRange(0, uint.MaxValue);

    public XDevkitConsoleConnection(XboxManager manager, XboxConsole console) {
        this.manager = manager;
        this.console = console;
        IXboxDebugTarget dbgTarget = this.console.DebugTarget;
        dbgTarget.MemoryCacheEnabled = false;
        XboxEvents_OnStdNotifyEventHandler handler = this.OnStdNotify;
        this.console.add_OnStdNotify(handler);
        this.console.add_OnTextNotify(this.OnTextNotify);
        this.isConnectedAsDebugger = true;

        // console.DebugTarget.SetDataBreakpoint(0, XboxBreakpointType.OnExecute);
    }

    private void OnStdNotify(XboxDebugEventType eventcode, IXboxEventInfo eventinfo) {
        XBOX_EVENT_INFO inf = eventinfo.Info;
        StringBuilder sb = new StringBuilder();
        sb.Append($"IsThreadStopped: {inf.IsThreadStopped != 0}, ");
        sb.Append($"ExecState: {inf.ExecState}, ");
        sb.Append($"Message: {inf.Message}, ");
        sb.Append($"Code: {inf.Code}, ");
        sb.Append($"Address: {inf.Address}, ");
        sb.Append($"Flags: {inf.Flags}, ");
        sb.Append($"ParameterCount: {inf.ParameterCount}");
        if (inf.ParameterCount > 0)
            sb.Append($", Parameters: {string.Join(", ", inf.Parameters)}");
        
        System.Console.WriteLine($"[StdNotify] {eventcode} -> {sb.ToString()}");
    }
    
    private void OnTextNotify(string source, string notification) {
        System.Console.WriteLine($"[TextNotify] {source} -> {notification}");
    }

    public override Task<bool?> IsMemoryInvalidOrProtected(uint address, uint count) {
        return Task.FromResult<bool?>(null);
    }

    protected override Task CloseCore() {
        if (this.isConnectedAsDebugger) {
            this.isConnectedAsDebugger = false;
            // this.console.remove_OnStdNotify(this.OnStdNotify);
            // this.console.remove_OnTextNotify(this.OnTextNotify);
            this.console.DebugTarget.DisconnectAsDebugger();
        }

        return Task.CompletedTask;
    }

    public async Task DebugFreeze() {
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();
        await Task.Run(() => this.console.DebugTarget.Stop(out bool isAlreadyStopped)).ConfigureAwait(false);
    }

    public async Task DebugUnFreeze() {
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();
        await Task.Run(() => this.console.DebugTarget.Go(out bool isAlreadyGoing)).ConfigureAwait(false);
    }

    public async Task<List<MemoryRegion>> GetMemoryRegions(bool willRead, bool willWrite) {
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        List<MemoryRegion> regionList = new List<MemoryRegion>();
        IXboxMemoryRegions regions = await Task.Run(() => {
            try {
                return this.console.DebugTarget.MemoryRegions;
            }
            catch (COMException) {
                throw new TimeoutException("Timeout reading memory regions");
            }
        });
        for (int i = 0, count = regions.Count; i < count; i++) {
            IXboxMemoryRegion region = regions[i];
            regionList.Add(new MemoryRegion((uint) region.BaseAddress, (uint) region.RegionSize, (uint) region.Flags, 0));
        }

        return regionList;
    }

    protected override Task ReadBytesCore(uint address, byte[] dstBuffer, int offset, int count) {
        return Task.Run(() => {
            IXboxDebugTarget target = this.console.DebugTarget;
            target.GetMemory_cpp(address, (uint) count, ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(dstBuffer), offset), out uint cbRead);
        });
    }

    protected override Task WriteBytesCore(uint address, byte[] srcBuffer, int offset, int count) {
        return Task.Run(() => {
            this.console.DebugTarget.SetMemory_cpp(address, (uint) count, ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(srcBuffer), offset), out uint cbWritten);
        });
    }
}