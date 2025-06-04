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

using System.Globalization;
using System.Xml.Serialization;
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;

namespace MemEngine360.Commands.ATM.XML;

[XmlInclude(typeof(XmlAddressEntry))]
[XmlInclude(typeof(XmlAddressEntryGroup))]
public abstract class XmlBaseAddressEntry {
    [XmlAttribute] public string? Description { get; set; }
}

public class XmlAddressEntry : XmlBaseAddressEntry {
    [XmlAttribute] public bool IsRefreshActive { get; set; }
    [XmlIgnore] public uint Address { get; set; }

    [XmlAttribute(nameof(Address))]
    public string AddressText {
        get => this.Address.ToString("X8");
        set => this.Address = uint.Parse(value, NumberStyles.HexNumber);
    }
    
    [XmlAttribute] public bool IsAddressAbsolute { get; set; } = true;
    [XmlAttribute] public NumericDisplayType NumericDisplayType { get; set; }
    [XmlAttribute] public DataType DataType { get; set; }
    [XmlAttribute] public StringType StringType { get; set; }
    [XmlAttribute] public int StringLength { get; set; }
}

public class XmlAddressEntryGroup : XmlBaseAddressEntry {
    [XmlArray("Items")]
    [XmlArrayItem("AddressEntry", typeof(XmlAddressEntry))]
    [XmlArrayItem("GroupEntry", typeof(XmlAddressEntryGroup))]
    public List<XmlBaseAddressEntry> Items { get; set; } = new List<XmlBaseAddressEntry>();

    [XmlAttribute] public bool IsAddressAbsolute { get; set; } = true;
    
    [XmlIgnore] public uint GroupAddress { get; set; }

    [XmlAttribute(nameof(GroupAddress))]
    public string GroupAddressText {
        get => this.GroupAddress.ToString("X8");
        set => this.GroupAddress = uint.Parse(value, NumberStyles.HexNumber);
    }
}