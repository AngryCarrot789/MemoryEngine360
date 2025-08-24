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
using PFXToolKitUI.DataTransfer;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils.Accessing;

namespace MemEngine360.XboxInfo;

public class MemoryRegionViewModel : BaseTransferableDataViewModel {
    // This class needs a re-work. We shouldn't use a raw address like this,
    // since cheat engine doesn't appear to do that (since you have use base address + a list of offsets)

    public static readonly DataKey<MemoryRegionViewModel> DataKey = DataKey<MemoryRegionViewModel>.Create("MemoryRegionViewModel");

    public static readonly DataParameter<uint> BaseAddressParameter = DataParameter.Register(new DataParameter<uint>(typeof(MemoryRegionViewModel), nameof(BaseAddress), 0, ValueAccessors.Reflective<uint>(typeof(MemoryRegionViewModel), nameof(baseAddress))));
    public static readonly DataParameter<uint> SizeParameter = DataParameter.Register(new DataParameter<uint>(typeof(MemoryRegionViewModel), nameof(Size), 0, ValueAccessors.Reflective<uint>(typeof(MemoryRegionViewModel), nameof(size))));
    public static readonly DataParameter<uint> ProtectionParameter = DataParameter.Register(new DataParameter<uint>(typeof(MemoryRegionViewModel), nameof(Protection), 0, ValueAccessors.Reflective<uint>(typeof(MemoryRegionViewModel), nameof(protection))));
    public static readonly DataParameter<uint> PhysicalAddressParameter = DataParameter.Register(new DataParameter<uint>(typeof(MemoryRegionViewModel), nameof(PhysicalAddress), 0, ValueAccessors.Reflective<uint>(typeof(MemoryRegionViewModel), nameof(physicalAddress))));

    private uint baseAddress, size, protection, physicalAddress;

    public uint BaseAddress {
        get => this.baseAddress;
        set => DataParameter.SetValueHelper(this, BaseAddressParameter, ref this.baseAddress, value);
    }
    
    public uint Size {
        get => this.size;
        set => DataParameter.SetValueHelper(this, SizeParameter, ref this.size, value);
    }
    
    public uint Protection {
        get => this.protection;
        set => DataParameter.SetValueHelper(this, ProtectionParameter, ref this.protection, value);
    }
    
    public uint PhysicalAddress {
        get => this.physicalAddress;
        set => DataParameter.SetValueHelper(this, PhysicalAddressParameter, ref this.physicalAddress, value);
    }

    public MemoryRegionViewModel() {
        
    }

    public MemoryRegionViewModel(MemoryRegion region) {
        this.baseAddress = region.BaseAddress;
        this.size = region.Size;
        this.protection = region.Protection;
        this.physicalAddress = region.PhysicalAddress;
    }

    static MemoryRegionViewModel() {
        RegisterParametersAsObservable(BaseAddressParameter, SizeParameter, ProtectionParameter, PhysicalAddressParameter);
    }
}