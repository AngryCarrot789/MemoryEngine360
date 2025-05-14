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
    [XmlElement] public bool IsRefreshActive { get; set; }
    [XmlElement] public uint Address { get; set; }
    [XmlElement] public bool IsAddressAbsolute { get; set; } = true;
    [XmlElement] public NumericDisplayType NumericDisplayType { get; set; }
    [XmlElement] public DataType DataType { get; set; }
    [XmlElement] public StringType StringType { get; set; }
    [XmlElement] public uint StringLength { get; set; }
}

public class XmlAddressEntryGroup : XmlBaseAddressEntry {
    [XmlArray("Items")]
    [XmlArrayItem("AddressEntry", typeof(XmlAddressEntry))]
    [XmlArrayItem("GroupEntry", typeof(XmlAddressEntryGroup))]
    public List<XmlBaseAddressEntry> Items { get; set; } = new List<XmlBaseAddressEntry>();

    [XmlElement] public uint? GroupAddress { get; set; }
}