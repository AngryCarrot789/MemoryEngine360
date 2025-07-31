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
using System.Net;
using System.Text;

namespace MemEngine360.Xbox360XBDM.Views;

public class DiscoveredConsole {
    /// <summary>
    /// The console's endpoint
    /// </summary>
    public IPEndPoint EndPoint { get; }

    /// <summary>
    /// The name of the console
    /// </summary>
    public string Name { get; }

    public DiscoveredConsole(IPEndPoint endPoint, string name) {
        this.EndPoint = endPoint;
        this.Name = name;
    }

    public static bool TryParse(byte[] datagram, IPEndPoint endPoint, [NotNullWhen(true)] out DiscoveredConsole? console) {
        if (datagram[0] != 0x02) {
            console = null;
            return false;
        }

        int strlen = datagram[1];
        if ((datagram.Length - 2) < strlen) {
            console = null;
            return false;
        }

        console = new DiscoveredConsole(endPoint, Encoding.ASCII.GetString(datagram, 2, strlen));
        return true;
    }
}