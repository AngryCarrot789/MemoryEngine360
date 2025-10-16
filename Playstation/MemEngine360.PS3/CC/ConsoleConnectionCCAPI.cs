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

using System.Runtime.Versioning;
using MemEngine360.Connections;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Utils;

namespace MemEngine360.PS3.CC;

[SupportedOSPlatform("windows")]
public class ConsoleConnectionCCAPI : BaseConsoleConnection {
    private readonly ConsoleControlAPI api;
    
    public override RegisteredConnectionType ConnectionType => ConnectionTypePS3CCAPI.Instance;
    
    public override bool IsLittleEndian => false;

    public override AddressRange AddressableRange => new AddressRange(0, uint.MaxValue);

    public ConsoleConnectionCCAPI(ConsoleControlAPI api) {
        this.api = api;
        this.api.NativeFailure += this.ApiOnNativeFailure;
    }

    protected override void CloseOverride() {
        base.CloseOverride();
        
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

    public override Task<bool?> IsMemoryInvalidOrProtected(uint address, uint count) {
        return Task.FromResult<bool?>(false);
    }

    protected override Task ReadBytesCore(uint address, byte[] dstBuffer, int offset, int count) {
        return this.api.ReadMemory(address, dstBuffer, offset, count);
    }

    protected override Task WriteBytesCore(uint address, byte[] srcBuffer, int offset, int count) {
        return this.api.WriteMemory(address, srcBuffer, offset, count);
    }
    
    /// <summary>
    /// Sets the attached PID for reading and writing memory
    /// </summary>
    /// <param name="processId">The new PID</param>
    /// <returns>The previous PID</returns>
    public Task<uint> AttachToProcess(uint processId) {
        return this.api.AttachToProcess(processId);
    }

    /// <summary>
    /// Finds the active game PID
    /// </summary>
    /// <returns>The PID, or zero, if no game is running</returns>
    public Task<(uint, string?)> FindGameProcessId() {
        return this.api.FindGameProcessId();
    }
    public Task<List<(uint, string?)>> GetAllProcesses() {
        return this.api.GetAllProcesses();
    }
}