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

namespace MemEngine360.Connections.Impl;

public readonly struct ConsoleResponse {
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
    public readonly ResponseType ResponseType;

    private ConsoleResponse(string raw, string message, ResponseType responseType) {
        this.RawMessage = raw;
        this.Message = message;
        this.ResponseType = responseType;
    }

    public static ConsoleResponse FromFirstLine(string line) {
        return new ConsoleResponse(line, line.Substring(5), (ResponseType) int.Parse(line.AsSpan(0, 3)));
    }
}