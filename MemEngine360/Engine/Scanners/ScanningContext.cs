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
    
    public Exception? ConnectionException { get; set; }

    /// <summary>
    /// Gets or sets if the scan encountered an IO error while reading data from the console
    /// </summary>
    public bool HasConnectionError => this.ConnectionException != null;

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

    internal abstract Task<bool> Setup();

    /// <summary>
    /// Performs the first scan. 
    /// </summary>
    /// <param name="connection">The connection to read values from</param>
    /// <param name="busyToken">The busy token</param>
    /// <returns>A task containing the busy token, or a new token if the one passed in the parameter was disposed (may be null when token could not be re-acquired)</returns>
    internal abstract Task<IDisposable?> PerformFirstScan(IConsoleConnection connection, IDisposable busyToken);

    /// <summary>
    /// Performs a next scan. Requirements: the src list's items are guaranteed to have the
    /// same data types, and it equals our internal scanning data type
    /// </summary>
    /// <param name="connection">The connection to read values from</param>
    /// <param name="srcList">The source list of items</param>
    /// <param name="busyToken">The busy token</param>
    /// <returns>A task containing the busy token, or a new token if the one passed in the parameter was disposed (may be null when token could not be re-acquired)</returns>
    internal abstract Task<IDisposable?> PerformNextScan(IConsoleConnection connection, List<ScanResultViewModel> srcList, IDisposable busyToken);
    
    internal abstract void ProcessMemoryBlockForFirstScan(uint baseAddress, ReadOnlySpan<byte> buffer);
}