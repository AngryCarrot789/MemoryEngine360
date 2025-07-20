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
using PFXToolKitUI.DataTransfer;
using PFXToolKitUI.Utils.Accessing;

namespace MemEngine360.Engine.HexEditing;

public class HexEditorInfo : ITransferableData {
    public static readonly DataParameterString CaptionParameter = DataParameter.Register(new DataParameterString(typeof(HexEditorInfo), nameof(Caption), "A message here", ValueAccessors.Reflective<string?>(typeof(HexEditorInfo), nameof(caption))));
    public static readonly DataParameterNumber<uint> StartAddressParameter = DataParameter.Register(new DataParameterNumber<uint>(typeof(HexEditorInfo), nameof(StartAddress), 0x82600000, ValueAccessors.Reflective<uint>(typeof(HexEditorInfo), nameof(startAddress))));
    public static readonly DataParameterNumber<uint> LengthParameter = DataParameter.Register(new DataParameterNumber<uint>(typeof(HexEditorInfo), nameof(Length), 0x100000, ValueAccessors.Reflective<uint>(typeof(HexEditorInfo), nameof(length))));
    public static readonly DataParameterNumber<uint> AutoRefreshStartAddressParameter = DataParameter.Register(new DataParameterNumber<uint>(typeof(HexEditorInfo), nameof(AutoRefreshStartAddress), 0, ValueAccessors.Reflective<uint>(typeof(HexEditorInfo), nameof(autoRefreshStartAddress))));
    public static readonly DataParameterNumber<uint> AutoRefreshLengthParameter = DataParameter.Register(new DataParameterNumber<uint>(typeof(HexEditorInfo), nameof(AutoRefreshLength), 0, ValueAccessors.Reflective<uint>(typeof(HexEditorInfo), nameof(autoRefreshLength))));
    public static readonly DataParameterNumber<uint> BytesPerRowParameter = DataParameter.Register(new DataParameterNumber<uint>(typeof(HexEditorInfo), nameof(BytesPerRow), 32, 8, 256, ValueAccessors.Reflective<uint>(typeof(HexEditorInfo), nameof(bytesPerRow))));
    public static readonly DataParameter<Endianness> InspectorEndiannessParameter = DataParameter.Register(new DataParameter<Endianness>(typeof(HexEditorInfo), nameof(InspectorEndianness), Endianness.BigEndian, ValueAccessors.Reflective<Endianness>(typeof(HexEditorInfo), nameof(inspectorEndianness))));
    
    private string? caption;
    private uint startAddress, autoRefreshStartAddress;
    private uint length, autoRefreshLength;
    private uint bytesPerRow;
    private Endianness inspectorEndianness;

    public string? Caption {
        get => this.caption;
        set => DataParameter.SetValueHelper(this, CaptionParameter, ref this.caption, value);
    }

    /// <summary>
    /// Gets or sets the address to Read All from, and only for Read All.
    /// </summary>
    public uint StartAddress {
        get => this.startAddress;
        set => DataParameter.SetValueHelper(this, StartAddressParameter, ref this.startAddress, value);
    }

    /// <summary>
    /// Gets or sets the amount of bytes to read during Read All, and only for Read All
    /// </summary>
    public uint Length {
        get => this.length;
        set => DataParameter.SetValueHelper(this, LengthParameter, ref this.length, value);
    }
    
    /// <summary>
    /// Gets or sets the address that autorefresh should start at
    /// </summary>
    public uint AutoRefreshStartAddress {
        get => this.autoRefreshStartAddress;
        set => DataParameter.SetValueHelper(this, AutoRefreshStartAddressParameter, ref this.autoRefreshStartAddress, value);
    }

    /// <summary>
    /// Gets or sets the number of bytes to read during autorefresh
    /// </summary>
    public uint AutoRefreshLength {
        get => this.autoRefreshLength;
        set => DataParameter.SetValueHelper(this, AutoRefreshLengthParameter, ref this.autoRefreshLength, value);
    }

    public uint BytesPerRow {
        get => this.bytesPerRow;
        set => DataParameter.SetValueHelper(this, BytesPerRowParameter, ref this.bytesPerRow, value);
    }
    
    public Endianness InspectorEndianness {
        get => this.inspectorEndianness;
        set => DataParameter.SetValueHelper(this, InspectorEndiannessParameter, ref this.inspectorEndianness, value);
    }
    
    /// <summary>
    /// Gets the document used by the hex editor control. Do not set this manually, because it will not affect the control
    /// </summary>
    public IBinaryDocument? Document { get; set; }
    
    /// <summary>
    /// Gets whether the hex editor allows values to be modified and therefore call <see cref="WriteDataAsync"/>
    /// </summary>
    public bool IsReadOnly { get; init; }

    public TransferableData TransferableData { get; }
    
    public MemoryEngine MemoryEngine { get; }

    public event EventHandler? RestartAutoRefresh;
    
    public HexEditorInfo(MemoryEngine memoryEngine) {
        this.MemoryEngine = memoryEngine;
        this.TransferableData = new TransferableData(this);
        this.caption = CaptionParameter.GetDefaultValue(this);
        this.startAddress = StartAddressParameter.GetDefaultValue(this);
        this.length = LengthParameter.GetDefaultValue(this);
        this.autoRefreshStartAddress = AutoRefreshStartAddressParameter.GetDefaultValue(this);
        this.autoRefreshLength = AutoRefreshLengthParameter.GetDefaultValue(this);
        this.bytesPerRow = BytesPerRowParameter.GetDefaultValue(this);
        if (memoryEngine.Connection is IConsoleConnection connection) {
            this.inspectorEndianness = connection.IsLittleEndian ? Endianness.LittleEndian : Endianness.BigEndian;
        }
        else {
            this.inspectorEndianness = InspectorEndiannessParameter.GetDefaultValue(this);
        }
    }

    public void RaiseRestartAutoRefresh() => this.RestartAutoRefresh?.Invoke(this, EventArgs.Empty);
}