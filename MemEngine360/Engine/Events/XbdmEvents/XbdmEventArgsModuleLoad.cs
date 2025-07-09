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

namespace MemEngine360.Engine.Events.XbdmEvents;

public class XbdmEventArgsModuleLoad : XbdmEventArgs {
    public string ModuleName { get; init; }
    public uint BaseAddress { get; init; }
    public uint ModuleSize { get; init; }
    public uint TimeStamp { get; init; }
    public uint CheckSum { get; init; }
    public ModLoadFlags Flags { get; init; }

    public XbdmEventArgsModuleLoad(string rawMessage) : base(rawMessage) {
    }
}

[Flags]
public enum ModLoadFlags {
    None            = 0x0000,
    DMN_MODFLAG_XBE = 0x0001,
    DMN_MODFLAG_TLS = 0x0002,
}