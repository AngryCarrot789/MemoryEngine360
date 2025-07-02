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

using System.Diagnostics.CodeAnalysis;
using MemEngine360.Engine.Addressing;
using MemEngine360.ValueAbstraction;

namespace MemEngine360.Sequencing;

public sealed class CachedConditionData {
    private Dictionary<IMemoryAddress, uint>? resolvedAddresses;
    private Dictionary<TypedAddress, IDataValue>? dataValues;

    public uint? this[IMemoryAddress address] {
        get {
            if (address is StaticAddress staticAddress)
                return staticAddress.Address;
            
            if (this.resolvedAddresses == null) return null;
            if (!this.resolvedAddresses.TryGetValue(address, out uint addr)) 
                return null;

            return addr;
        }
        set {
            if (address is StaticAddress) 
                return;
            
            this.resolvedAddresses ??= new Dictionary<IMemoryAddress, uint>();
            if (value.HasValue)
                this.resolvedAddresses[address] = value.Value;
            else
                this.resolvedAddresses.Remove(address);
        }
    }
    
    public IDataValue? this[TypedAddress address] {
        get => this.dataValues?.GetValueOrDefault(address);
        set {
            this.dataValues ??= new Dictionary<TypedAddress, IDataValue>();
            if (value != null)
                this.dataValues[address] = value;
            else
                this.dataValues.Remove(address);
        }
    }

    public CachedConditionData() {
    }

    public bool TryGetAddress(IMemoryAddress key, out uint value) {
        if (key is StaticAddress staticAddress) {
            value = staticAddress.Address;
            return true;
        }

        if (this.resolvedAddresses == null) {
            value = 0;
            return false;
        }
        
        return this.resolvedAddresses.TryGetValue(key, out value);
    }

    public bool TryGetDataValue(TypedAddress key, [NotNullWhen(true)] out IDataValue? value) {
        if (this.dataValues == null) {
            value = null;
            return false;
        }
        
        return this.dataValues.TryGetValue(key, out value);
    }
}