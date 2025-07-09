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

public abstract class XbdmEventArgsSection : XbdmEventArgs {
    public string Name { get; set; }
    public uint BaseAddress { get; set; }
    public uint Size { get; set; }
    public uint? Index { get; set; }
    public uint? Flags { get; set; }

    public XbdmEventArgsSection(string rawMessage) : base(rawMessage) {
    }
}

public class XbdmEventArgsSectionLoad : XbdmEventArgsSection {
    public XbdmEventArgsSectionLoad(string rawMessage) : base(rawMessage) {
    }
}

public class XbdmEventArgsSectionUnload : XbdmEventArgsSection {
    public XbdmEventArgsSectionUnload(string rawMessage) : base(rawMessage) {
    }
}