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
using MemEngine360.Ps3Base;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.PS3.MAPI;

public class ConsoleConnectionMAPI : BaseConsoleConnection, IPs3ConsoleConnection {
    public override RegisteredConnectionType ConnectionType => ConnectionTypePS3MAPI.Instance;
    
    public override bool IsLittleEndian => false;

    public override AddressRange AddressableRange => new AddressRange(0, uint.MaxValue);

    public Ps3Process AttachedProcess {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, static t => {
            t.AttachedProcessChanged?.Invoke(t, EventArgs.Empty);
            t.ConnectionType.RaiseConnectionStatusBarTextInvalidated(t);
        });
    } = Ps3Process.Default;

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
        await this.api.ReadMemory(this.AttachedProcess.ProcessId, address, dstBuffer, offset, count);
    }

    protected override async Task WriteBytesCore(uint address, byte[] srcBuffer, int offset, int count) {
        await this.api.WriteMemory(this.AttachedProcess.ProcessId, address, srcBuffer, offset, count);
    }
    
    public async Task<string?> GetProcessName(uint processId) {
        try {
            return await this.api.GetProcessName(processId);
        }
        catch (Exception e) when (e is TimeoutException || e is IOException) {
            this.Close();
            throw;
        }
    }

    /// <summary>
    /// Finds the active game PID
    /// </summary>
    /// <returns>The PID, or zero, if no game is running</returns>
    public async Task<Ps3Process> FindGameProcessId() {
        try {
            Ps3Process[] processes = await this.GetAllProcessesWithName();
            foreach (Ps3Process process in processes) {
                if (process.ProcessName != null && !process.ProcessName.Contains("dev_flash")) {
                    return process;
                }
            }

            return Ps3Process.Empty;
        }
        catch (Exception e) when (e is TimeoutException || e is IOException) {
            this.Close();
            throw;
        }
    }
    
    public async Task<Ps3Process[]> GetAllProcessesWithName() {
        try {
            uint[] pids = await this.api.GetProcessList().ConfigureAwait(false);
            Ps3Process[] processes = new  Ps3Process[pids.Length];
            for (int i = 0; i < pids.Length; i++) {
                processes[i] = new Ps3Process(pids[i], await this.api.GetProcessName(pids[i]).ConfigureAwait(false));
            }

            return processes;
        }
        catch (Exception e) when (e is TimeoutException || e is IOException) {
            this.Close();
            throw;
        }
    }
    
    public async Task<uint[]> GetAllProcesses() {
        try {
            return await this.api.GetProcessList();
        }
        catch (Exception e) when (e is TimeoutException || e is IOException) {
            this.Close();
            throw;
        }
    }
}