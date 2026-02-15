// 
// Copyright (c) 2026-2026 REghZy
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

using System.Net;
using MemEngine360.Connections;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.PS3.MAPI;

public class ConsoleConnectionMAPI : BaseConsoleConnection, IPs3ConsoleConnection {
    public override RegisteredConnectionType ConnectionType => ConnectionTypePS3MAPI.Instance;
    
    public override bool IsLittleEndian => false;

    public override AddressRange AddressableRange => new AddressRange(0, uint.MaxValue);

    public uint AttachedProcess {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, static t => {
            t.AttachedProcessChanged?.Invoke(t, EventArgs.Empty);
            t.ConnectionType.RaiseConnectionStatusBarTextInvalidated(t);
        });
    } = 0xFFFFFFFF;

    public EndPoint? EndPoint => !this.IsClosed ? this.api.EndPoint : null;

    public event EventHandler? AttachedProcessChanged;

    private readonly Ps3ManagerApiV2 api;

    public ConsoleConnectionMAPI(Ps3ManagerApiV2 api) {
        this.api = api;
    }

    protected override void CloseOverride() {
        this.api.Disconnect();
    }

    public override Task<bool?> IsMemoryInvalidOrProtected(uint address, int count) {
        return Task.FromResult<bool?>(false);
    }

    protected override async Task ReadBytesCore(uint address, byte[] dstBuffer, int offset, int count) {
        await this.api.Memory_Get(this.AttachedProcess, address, dstBuffer, offset, count);
    }

    protected override Task WriteBytesCore(uint address, byte[] srcBuffer, int offset, int count) {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Finds the active game PID
    /// </summary>
    /// <returns>The PID, or zero, if no game is running</returns>
    public async Task<uint> FindGameProcessId() {
        List<(uint, string?)> result = await this.GetAllProcessesWithName();
        foreach ((uint pid, string? name) proc in result) {
            if (proc.name != null && !proc.name.Contains("dev_flash")) {
                return proc.pid;
            }
        }

        return 0;
    }
    
    public async Task<List<(uint, string?)>> GetAllProcessesWithName() {
        uint[] pids = await this.api.GetPidList();
        List<(uint, string?)> pidNameList = new List<(uint, string?)>();
        foreach (uint pid in pids) {
            pidNameList.Add((pid, await this.api.Process_GetName(pid)));
        }

        return pidNameList;
    }
    
    public async Task<uint[]> GetAllProcesses() {
        return await this.api.GetPidList();
    }
}