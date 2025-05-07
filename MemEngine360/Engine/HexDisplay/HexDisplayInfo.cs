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

using PFXToolKitUI.DataTransfer;
using PFXToolKitUI.Utils.Accessing;

namespace MemEngine360.Engine.HexDisplay;

public class HexDisplayInfo : ITransferableData {
    public static readonly DataParameterString CaptionParameter = DataParameter.Register(new DataParameterString(typeof(HexDisplayInfo), nameof(Caption), "A message here", ValueAccessors.Reflective<string?>(typeof(HexDisplayInfo), nameof(caption))));
    public static readonly DataParameterNumber<uint> StartAddressParameter = DataParameter.Register(new DataParameterNumber<uint>(typeof(HexDisplayInfo), nameof(StartAddress), default(uint), ValueAccessors.Reflective<uint>(typeof(HexDisplayInfo), nameof(startAddress))));
    public static readonly DataParameterNumber<uint> LengthParameter = DataParameter.Register(new DataParameterNumber<uint>(typeof(HexDisplayInfo), nameof(Length), 0x10000, ValueAccessors.Reflective<uint>(typeof(HexDisplayInfo), nameof(length))));
    public static readonly DataParameterNumber<uint> AutoRefreshStartAddressParameter = DataParameter.Register(new DataParameterNumber<uint>(typeof(HexDisplayInfo), nameof(AutoRefreshStartAddress), 0, ValueAccessors.Reflective<uint>(typeof(HexDisplayInfo), nameof(autoRefreshStartAddress))));
    public static readonly DataParameterNumber<uint> AutoRefreshLengthParameter = DataParameter.Register(new DataParameterNumber<uint>(typeof(HexDisplayInfo), nameof(AutoRefreshLength), 0, ValueAccessors.Reflective<uint>(typeof(HexDisplayInfo), nameof(autoRefreshLength))));
    public static readonly DataParameter<uint> BytesPerRowParameter = DataParameter.Register(new DataParameter<uint>(typeof(HexDisplayInfo), nameof(BytesPerRow), 32, ValueAccessors.Reflective<uint>(typeof(HexDisplayInfo), nameof(bytesPerRow))));
    
    private string? caption;
    private uint startAddress, autoRefreshStartAddress;
    private uint length, autoRefreshLength;
    private uint bytesPerRow;

    public string? Caption {
        get => this.caption;
        set => DataParameter.SetValueHelper(this, CaptionParameter, ref this.caption, value);
    }

    public uint StartAddress {
        get => this.startAddress;
        set => DataParameter.SetValueHelper(this, StartAddressParameter, ref this.startAddress, value);
    }

    public uint Length {
        get => this.length;
        set => DataParameter.SetValueHelper(this, LengthParameter, ref this.length, value);
    }
    
    public uint AutoRefreshStartAddress {
        get => this.autoRefreshStartAddress;
        set => DataParameter.SetValueHelper(this, AutoRefreshStartAddressParameter, ref this.autoRefreshStartAddress, value);
    }

    public uint AutoRefreshLength {
        get => this.autoRefreshLength;
        set => DataParameter.SetValueHelper(this, AutoRefreshLengthParameter, ref this.autoRefreshLength, value);
    }

    public uint BytesPerRow {
        get => this.bytesPerRow;
        set => DataParameter.SetValueHelper(this, BytesPerRowParameter, ref this.bytesPerRow, value);
    }
    
    /// <summary>
    /// Gets whether the hex editor allows values to be modified and therefore call <see cref="WriteDataAsync"/>
    /// </summary>
    public bool IsReadOnly { get; init; }

    public TransferableData TransferableData { get; }
    
    public MemoryEngine360 MemoryEngine360 { get; }

    public HexDisplayInfo(MemoryEngine360 memoryEngine360) {
        this.MemoryEngine360 = memoryEngine360;
        this.TransferableData = new TransferableData(this);
        this.caption = CaptionParameter.GetDefaultValue(this);
        this.startAddress = StartAddressParameter.GetDefaultValue(this);
        this.length = LengthParameter.GetDefaultValue(this);
        this.autoRefreshStartAddress = AutoRefreshStartAddressParameter.GetDefaultValue(this);
        this.autoRefreshLength = AutoRefreshLengthParameter.GetDefaultValue(this);
        this.bytesPerRow = BytesPerRowParameter.GetDefaultValue(this);
    }
}