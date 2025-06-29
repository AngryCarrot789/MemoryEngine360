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

/// <summary>
/// An IO exception thrown when the console responds with an invalid response to a command.
/// </summary>
public class UnexpectedResponseException : IOException {
    public string CommandName { get; }

    public ResponseType Actual { get; }

    public ResponseType Expected { get; }

    public UnexpectedResponseException(string commandName, ResponseType actual, ResponseType expected) :
        base($"Xbox responded to {commandName} with {actual} instead of {expected}, which is unexpected") {
        this.CommandName = commandName;
        this.Actual = actual;
        this.Expected = expected;
    }
}