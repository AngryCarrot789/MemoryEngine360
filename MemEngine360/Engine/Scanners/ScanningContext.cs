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

using MemEngine360.Connections;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.Scanners;

/// <summary>
/// The base context info for a specific type of scanning operation
/// </summary>
public abstract class ScanningContext {
    public readonly ScanningProcessor Processor;
    internal readonly uint startAddress, scanLength, scanEndAddress;
    internal readonly uint alignment;
    internal readonly bool pauseConsoleDuringScan;
    internal readonly bool scanMemoryPages;
    internal bool isConnectionLittleEndian;
    
    public Exception? ConnectionException { get; set; }

    /// <summary>
    /// Gets or sets if the scan encountered an IO error while reading data from the console
    /// </summary>
    public bool HasConnectionError => this.ConnectionException != null;

    /// <summary>
    /// Gets the maximum amount of overlap required for cross-chunk scanning to work successfully
    /// </summary>
    public abstract uint Overlap { get; }

    /// <summary>
    /// Fired when a result is found. When scanning for the next value, it fires with a pre-existing result
    /// </summary>
    public abstract event ScanningContextResultEventHandler? ResultFound;

    protected ScanningContext(ScanningProcessor processor) {
        this.Processor = processor;
        this.startAddress = processor.StartAddress;
        this.scanLength = processor.ScanLength;
        this.scanEndAddress = this.startAddress + this.scanLength;
        this.alignment = processor.Alignment;
        this.pauseConsoleDuringScan = processor.PauseConsoleDuringScan;
        this.scanMemoryPages = processor.ScanMemoryPages;
    }

    internal Task<bool> Setup(IConsoleConnection connection) {
        this.isConnectionLittleEndian = connection.IsLittleEndian;
        return this.SetupCore(connection);
    }
    
    internal abstract Task<bool> SetupCore(IConsoleConnection connection);

    /// <summary>
    /// Performs the first scan.
    /// </summary>
    /// <param name="connection">The connection to read values from</param>
    /// <param name="busyTokenRef">A reference to the busy token</param>
    internal abstract Task PerformFirstScan(IConsoleConnection connection, Reference<IDisposable?> busyTokenRef);

    /// <summary>
    /// Check whether the next scan is allowed to run based on the current scan results.
    /// Usually always returns true. This method may show error dialogs and return false.
    /// </summary>
    /// <param name="srcList"></param>
    /// <returns></returns>
    public abstract Task<bool> CanRunNextScan(List<ScanResultViewModel> srcList);

    /// <summary>
    /// Performs a next scan. Requirements: the src list's items are guaranteed to have the
    /// same data types, and it equals our internal scanning data type
    /// </summary>
    /// <param name="connection">The connection to read values from</param>
    /// <param name="srcList">The source list of items</param>
    /// <param name="busyTokenRef"></param>
    internal abstract Task PerformNextScan(IConsoleConnection connection, List<ScanResultViewModel> srcList, Reference<IDisposable?> busyTokenRef);
    
    internal abstract void ProcessMemoryBlockForFirstScan(uint baseAddress, ReadOnlySpan<byte> buffer);
}