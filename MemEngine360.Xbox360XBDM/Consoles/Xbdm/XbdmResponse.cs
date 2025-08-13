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

namespace MemEngine360.Xbox360XBDM.Consoles.Xbdm;

public readonly struct XbdmResponse {
    /// <summary>
    /// Gets the first response line as raw
    /// </summary>
    public readonly string RawMessage;

    /// <summary>
    /// Gets the first line's response message
    /// </summary>
    public readonly string Message;

    /// <summary>
    /// Gets the response type
    /// </summary>
    public readonly XbdmResponseType ResponseType;

    private XbdmResponse(string raw, XbdmResponseType responseType, string message) {
        this.RawMessage = raw;
        this.Message = message;
        this.ResponseType = responseType;
    }

    public static XbdmResponse FromLine(string line) {
        if (line.Length > 4 && int.TryParse(line.AsSpan(0, 3), out int responseType)) {
            return new XbdmResponse(line, (XbdmResponseType) responseType, line.Substring(5));
        }

        throw new IOException("Invalid response: " + line);
    }

    public static bool TryParseFromLine(string line, out XbdmResponse xbdmResponse) {
        if (line.Length > 4 && int.TryParse(line.AsSpan(0, 3), out int responseType)) {
            xbdmResponse = new XbdmResponse(line, (XbdmResponseType) responseType, line.Substring(5));
            return true;
        }

        xbdmResponse = default;
        return false;
    }

    public override string ToString() {
        string intResponse = ((int) this.ResponseType).ToString();
        string strResponse = this.ResponseType.ToString();

        return ((intResponse == strResponse) ? strResponse : $"{strResponse} ({intResponse})") + " - " + this.Message;
    }
}