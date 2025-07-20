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
    string ToString();
}

public static class MemoryAddressUtils {
    private static (IMemoryAddress?, string?) TryParseInternal(string? input) {
        if (string.IsNullOrWhiteSpace(input))
            return (null, "Input string is empty");

        // 820002CD
        if (uint.TryParse(input, NumberStyles.HexNumber, null, out uint staticAddress))
            return (new StaticAddress(staticAddress), null);

        // 820002CD->25C->40->118
        int firstOffset = input.IndexOf("->", StringComparison.Ordinal);
        if (firstOffset == -1)
            return (null, "Missing dereference token(s) \"->\"" + input);

        ReadOnlySpan<char> span = input.AsSpan(0, firstOffset);
        if (!TryParseHexUInt32(span, out uint baseAddress))
            return (null, "Invalid base address: " + span.ToString());

        List<int> offsets = new List<int>();
        int idxPtrToken, idxBeginLastOffset = firstOffset + 2, offset;
        while ((idxPtrToken = input.IndexOf("->", idxBeginLastOffset, StringComparison.Ordinal)) != -1) {
            span = input.AsSpan(idxBeginLastOffset, idxPtrToken - idxBeginLastOffset);
            if (!TryParseHexInt32(span, out offset))
                return (null, "Invalid offset: " + span.ToString());

            offsets.Add(offset);
            idxBeginLastOffset = idxPtrToken + 2;
        }

        if (!TryParseHexInt32(span = input.AsSpan(idxBeginLastOffset), out offset))
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
        if (entry.MemoryAddress is StaticAddress staticAddress) {
            return staticAddress.Address;
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

    public static bool TryParseHexUInt32(string? input, out uint result) {
        return TryParseHexUInt32(input.AsSpan(), out result);
    }

    public static bool TryParseHexUInt32(ReadOnlySpan<char> input, out uint result) {
        if (input.StartsWith("0x", StringComparison.Ordinal))
            input = input.Slice(2);
        return uint.TryParse(input, NumberStyles.HexNumber, null, out result);
    }
    
    public static bool TryParseHexInt32(string? input, out int result) {
        return TryParseHexInt32(input.AsSpan(), out result);
    }

    public static bool TryParseHexInt32(ReadOnlySpan<char> input, out int result) {
        if (input.StartsWith("-0x", StringComparison.Ordinal))
            input = input.Slice(3);
        else if (input.StartsWith("0x", StringComparison.Ordinal))
            input = input.Slice(2);
        
        return int.TryParse(input, NumberStyles.HexNumber, null, out result);
    }
}