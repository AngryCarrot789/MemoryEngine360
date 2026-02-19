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

using System.Net;
using System.Runtime.Versioning;
using MemEngine360.Connections;
using MemEngine360.Ps3Base;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.PS3.CCAPI;

[SupportedOSPlatform("windows")]
public class ConsoleConnectionCCAPI : BaseConsoleConnection, IPs3ConsoleConnection {
    private readonly ConsoleControlAPI api;

    public override RegisteredConnectionType ConnectionType => ConnectionTypePS3CCAPI.Instance;

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

    public ConsoleConnectionCCAPI(ConsoleControlAPI api) {
        this.api = api;
        this.api.NativeFailure += this.ApiOnNativeFailure;
    }

    protected override void CloseOverride() {
        Task.Run(() => {
            try {
                this.api.DisconnectFromConsole(true);
            }
            catch (Exception e) {
                // ignored
            }

            try {
                this.api.Dispose();
            }
            catch (Exception e) {
                // ignored
            }
        });
    }

    private void ApiOnNativeFailure(object? sender, EventArgs e) {
        AppLogger.Instance.WriteLine("CCAPI console connection error");
        AppLogger.Instance.WriteLine(this.api.FailureException!.GetToString());

        this.Close();
    }

    public override Task<bool?> IsMemoryInvalidOrProtected(uint address, int count) {
        return Task.FromResult<bool?>(false);
    }

    protected override Task ReadBytesCore(uint address, byte[] dstBuffer, int offset, int count) {
        return this.api.ReadMemory(this.AttachedProcess.ProcessId, address, dstBuffer, offset, count);
    }

    protected override Task WriteBytesCore(uint address, byte[] srcBuffer, int offset, int count) {
        return this.api.WriteMemory(this.AttachedProcess.ProcessId, address, srcBuffer, offset, count);
    }

    public async Task<string?> GetProcessName(uint processId) {
        try {
            // TODO: implement a command for getting process name
            foreach (Ps3Process process in await this.api.GetAllProcesses()) {
                if (process.ProcessId == processId && process.ProcessName != null) {
                    // there shouldn't be duplicate process ids but who knows what MAPI might send us
                    return process.ProcessName;
                }
            }

            return null;
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
            return await this.api.FindGameProcessId();
        }
        catch (Exception e) when (e is TimeoutException || e is IOException) {
            this.Close();
            throw;
        }
    }

    public async Task<Ps3Process[]> GetAllProcessesWithName() {
        return (await this.api.GetAllProcesses()).ToArray();
    }

    public async Task<uint[]> GetAllProcesses() {
        return (await this.api.GetAllProcesses()).Select(x => x.ProcessId).ToArray();
    }
}