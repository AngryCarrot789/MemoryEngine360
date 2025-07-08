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
public class InvalidResponseException : IOException {
    public ResponseType Actual { get; }

    public ResponseType Expected { get; }

    private InvalidResponseException(ResponseType actual, ResponseType expected, string message) : base(message) {
        this.Actual = actual;
        this.Expected = expected;
    }

    public static InvalidResponseException ForCommand(string commandName, ResponseType actual, ResponseType expected) {
        int idx = commandName.IndexOf(' ');
        commandName = idx == -1 ? commandName : commandName.Substring(0, idx);
        return new InvalidResponseException(actual, expected, $"Xbox responded to command '{commandName}' with {actual} instead of {expected}, which is unexpected");
    }
    
    public static InvalidResponseException WithMessage(string message, ResponseType actual, ResponseType expected) {
        return new InvalidResponseException(actual, expected, message);
    }
    
    public static InvalidResponseException WithResponseTypes(ResponseType actual, ResponseType expected) {
        return new InvalidResponseException(actual, expected, $"Xbox responded with {actual} instead of {expected}, which is unexpected");
    }
}