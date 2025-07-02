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
using System.Globalization;
using MemEngine360.Connections;
using MemEngine360.Engine.SavedAddressing;

namespace MemEngine360.Engine.Addressing;

public interface IMemoryAddress {
    /// <summary>
    /// Gets whether this address is static and can therefore be resolved without needing to be connected to a console
    /// </summary>
    bool IsStatic { get; }

    string ToString();
}

public static class MemoryAddressUtils {
    // Remove requirement for base pointer offset
    private static (IMemoryAddress?, string?) TryParseInternal(string? input) {
        if (string.IsNullOrWhiteSpace(input))
            return (null, "Input cannot be an empty string");
        if (uint.TryParse(input, NumberStyles.HexNumber, null, out uint staticAddress))
            return (new StaticAddress(staticAddress), null);

        // 820002CD
        // 820002CD->25C->40->118
        ReadOnlySpan<char> span;
        List<int> offsets = new List<int>();
        int firstOffset = input.IndexOf("->", StringComparison.Ordinal);
        if (firstOffset == -1)
            return (null, "Missing dereference tokens \"->\"" + input);
        
        if (!uint.TryParse(span = input.AsSpan(0, firstOffset), NumberStyles.HexNumber, null, out uint baseAddress))
            return (null, "Invalid base address: " + span.ToString());
        
        int idxPtrToken, idxBeginLastOffset = firstOffset + 2, offset;
        while ((idxPtrToken = input.IndexOf("->", idxBeginLastOffset, StringComparison.Ordinal)) != -1) {
            span = input.AsSpan(idxBeginLastOffset, idxPtrToken - idxBeginLastOffset);
            if (!int.TryParse(span, NumberStyles.HexNumber, null, out offset))
                return (null, "Invalid offset: " + span.ToString());

            offsets.Add(offset);
            idxBeginLastOffset = idxPtrToken + 2;
        }

        if (!int.TryParse(span = input.AsSpan(idxBeginLastOffset), NumberStyles.HexNumber, null, out offset))
            return (null, "Invalid offset: " + span.ToString());
        offsets.Add(offset);

        return (new DynamicAddress(baseAddress, offsets), null);
    }

    public static bool TryParse(string? input, [NotNullWhen(true)] out IMemoryAddress? address) {
        (IMemoryAddress?, string?) result = TryParseInternal(input);
        return (address = result.Item1) != null;
    }

    public static bool TryParse(string? input, [NotNullWhen(true)] out IMemoryAddress? address, out string? errorMessage) {
        (IMemoryAddress?, string?) result = TryParseInternal(input);
        errorMessage = result.Item2;
        return (address = result.Item1) != null;
    }

    public static async Task<uint?> TryResolveAddressFromATE(AddressTableEntry entry) {
        if (entry.MemoryAddress.IsStatic) {
            return ((StaticAddress) entry.MemoryAddress).Address;
        }

        IDisposable? token;
        IConsoleConnection? connection;
        MemoryEngine? engine = entry.AddressTableManager?.MemoryEngine;
        if (engine != null && (token = await engine.BeginBusyOperationAsync(250)) != null) {
            try {
                if ((connection = engine.Connection) != null) {
                    try {
                        return await entry.MemoryAddress.TryResolveAddress(connection) ?? 0;
                    }
                    catch {
                        // probably timeout or IO exception, so just ignore
                    }
                }
            }
            finally {
                token.Dispose();
            }
        }

        return null;
    }

    public static Task<uint?> TryResolveAddress(this IMemoryAddress address, IConsoleConnection connection) {
        if (address is StaticAddress)
            return Task.FromResult<uint?>(((StaticAddress) address).Address);
        if (address is DynamicAddress)
            return ((DynamicAddress) address).TryResolve(connection);

        throw new ArgumentException("Unknown memory address type");
    }
}