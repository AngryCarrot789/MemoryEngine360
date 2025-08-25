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

using AvaloniaHex.Base.Document;
using MemEngine360.Connections;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.HexEditing;

public delegate void MemoryViewerEventHandler(MemoryViewer sender);

public class MemoryViewer {
    public const uint MinimumBytesPerRow = 8;
    public const uint MaximumBytesPerRow = 256;

    private uint offset = 0x82000000;
    private uint autoRefreshStartAddress, autoRefreshLength;
    private uint bytesPerRow = 32;
    private Endianness inspectorEndianness = Endianness.BigEndian;

    /// <summary>
    /// Gets or sets the address that autorefresh should start at
    /// </summary>
    public uint AutoRefreshStartAddress {
        get => this.autoRefreshStartAddress;
        set => PropertyHelper.SetAndRaiseINE(ref this.autoRefreshStartAddress, value, this, static t => t.AutoRefreshStartAddressChanged?.Invoke(t));
    }

    public uint Offset {
        get => this.offset;
        set => PropertyHelper.SetAndRaiseINE(ref this.offset, value, this, static t => t.OffsetChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets the number of bytes to read during autorefresh
    /// </summary>
    public uint AutoRefreshLength {
        get => this.autoRefreshLength;
        set => PropertyHelper.SetAndRaiseINE(ref this.autoRefreshLength, value, this, static t => t.AutoRefreshLengthChanged?.Invoke(t));
    }

    public uint BytesPerRow {
        get => this.bytesPerRow;
        set {
            if (value < MinimumBytesPerRow || value > MaximumBytesPerRow)
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Value must be between {MinimumBytesPerRow} and {MaximumBytesPerRow}");
            PropertyHelper.SetAndRaiseINE(ref this.bytesPerRow, value, this, static t => t.BytesPerRowChanged?.Invoke(t));
        }
    }

    public Endianness InspectorEndianness {
        get => this.inspectorEndianness;
        set => PropertyHelper.SetAndRaiseINE(ref this.inspectorEndianness, value, this, static t => t.InspectorEndiannessChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets the source used by the hex editor control. Do not set this manually, because it will not affect the control
    /// </summary>
    public IBinarySource? BinarySource { get; set; }

    /// <summary>
    /// Gets the memory engine associated with this memory viewer
    /// </summary>
    public MemoryEngine MemoryEngine { get; }

    public event MemoryViewerEventHandler? RestartAutoRefresh;
    public event MemoryViewerEventHandler? AutoRefreshStartAddressChanged;
    public event MemoryViewerEventHandler? OffsetChanged;
    public event MemoryViewerEventHandler? AutoRefreshLengthChanged;
    public event MemoryViewerEventHandler? BytesPerRowChanged;
    public event MemoryViewerEventHandler? InspectorEndiannessChanged;

    public MemoryViewer(MemoryEngine memoryEngine) {
        this.MemoryEngine = memoryEngine;
        if (memoryEngine.Connection is IConsoleConnection connection) {
            this.inspectorEndianness = connection.IsLittleEndian ? Endianness.LittleEndian : Endianness.BigEndian;
        }
    }

    public void RaiseRestartAutoRefresh() => this.RestartAutoRefresh?.Invoke(this);
}