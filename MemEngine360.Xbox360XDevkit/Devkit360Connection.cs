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
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MemEngine360.Connections;
using XDevkit;
using MemoryRegion = MemEngine360.Connections.MemoryRegion;

namespace MemEngine360.Xbox360XDevkit;

public class Devkit360Connection : BaseConsoleConnection, IConsoleConnection, IHaveIceCubes, IHaveMemoryRegions {
    private readonly XboxManager manager;
    private readonly XboxConsole console;
    private bool isConnectedAsDebugger;

    public XboxConsole Console => this.console;

    public override RegisteredConsoleType ConsoleType => ConsoleTypeXbox360XDevkit.Instance;

    protected override bool IsConnectedCore => this.isConnectedAsDebugger;
    
    public override bool IsLittleEndian => false;

    public Devkit360Connection(XboxManager manager, XboxConsole console) {
        this.manager = manager;
        this.console = console;
        this.console.DebugTarget.MemoryCacheEnabled = false;
        // this.console.add_OnStdNotify(this.OnStdNotify);
        // this.console.add_OnTextNotify(this.OnTextNotify);
        this.isConnectedAsDebugger = true;

        // console.DebugTarget.SetDataBreakpoint(0, XboxBreakpointType.OnExecute);
    }

    // private void OnStdNotify(XboxDebugEventType eventcode, IXboxEventInfo eventinfo) {
    //     XBOX_EVENT_INFO inf = eventinfo.Info;
    //     StringBuilder sb = new StringBuilder();
    //     sb.Append($"IsThreadStopped: {inf.IsThreadStopped != 0}, ");
    //     sb.Append($"ExecState: {inf.ExecState}, ");
    //     sb.Append($"Message: {inf.Message}, ");
    //     sb.Append($"Code: {inf.Code}, ");
    //     sb.Append($"Address: {inf.Address}, ");
    //     sb.Append($"Flags: {inf.Flags}, ");
    //     sb.Append($"ParameterCount: {inf.ParameterCount}");
    //     if (inf.ParameterCount > 0)
    //         sb.Append($", Parameters: {string.Join(", ", inf.Parameters)}");
    //     
    //     Debug.WriteLine($"[StdNotify] {eventcode} -> {sb.ToString()}");
    // }
    //
    // private void OnTextNotify(string source, string notification) {
    //     Debug.WriteLine($"[TextNotify] {source} -> {notification}");
    // }

    protected override void CloseCore() {
        if (this.isConnectedAsDebugger) {
            this.isConnectedAsDebugger = false;
            // this.console.remove_OnStdNotify(this.OnStdNotify);
            // this.console.remove_OnTextNotify(this.OnTextNotify);
            this.console.DebugTarget.DisconnectAsDebugger();
        }
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
        IXboxMemoryRegions regions = this.console.DebugTarget.MemoryRegions;
        for (int i = 0, count = regions.Count; i < count; i++) {
            IXboxMemoryRegion region = regions[i];
            regionList.Add(new MemoryRegion((uint) region.BaseAddress, (uint) region.RegionSize, (uint) region.Flags, 0));
        }

        return regionList;
    }

    public async Task<uint> GetProcessID() {
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        uint value = this.console.RunningProcessInfo.ProcessId;
        return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
    }

    public async Task<IPAddress> GetTitleIPAddress() {
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        uint value = this.console.IPAddressTitle;
        return new IPAddress(BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value);
    }

    protected override Task<uint> ReadBytesCore(uint address, byte[] dstBuffer, int offset, uint count) {
        return Task.Run(() => {
            IXboxDebugTarget target = this.console.DebugTarget;
            target.GetMemory_cpp(address, count, ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(dstBuffer), offset), out uint cbRead);
            return cbRead;
        });
    }

    protected override Task<uint> WriteBytesCore(uint address, byte[] srcBuffer, int offset, uint count) {
        return Task.Run(() => {
            this.console.DebugTarget.SetMemory_cpp(address, count, ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(srcBuffer), offset), out uint cbWritten);
            return cbWritten;
        });
    }
}