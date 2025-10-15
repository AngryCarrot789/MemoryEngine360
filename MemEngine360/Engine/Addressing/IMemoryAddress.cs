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

/// <summary>
/// Represents an address to some data. Semi-immutable; the address part is constant, however, internal caching may be used
/// </summary>
public interface IMemoryAddress {
    string ToString();

    /// <summary>
    /// Returns whether the address part of the current and other instance are equal,
    /// ignoring other properties (e.g. <see cref="DynamicAddress.StaticOffsetCount"/>)
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    bool AddressEquals(IMemoryAddress other);

    static bool AddressEquals(IMemoryAddress? a, IMemoryAddress? b) {
        if (ReferenceEquals(a, b))
            return true;
        if (a == null || b == null)
            return false;
        return a.AddressEquals(b);
    }
}

public static class MemoryAddressUtils {
    private static (IMemoryAddress?, string?) TryParseInternal(string? input) {
        if (string.IsNullOrWhiteSpace(input))
            return (null, "Input string is empty");

        // 820002CD
        if (TryParseHexUInt32(input, out uint staticAddress))
            return (new StaticAddress(staticAddress), null);

        // 820002CD->25C->40->118
        int firstOffset = input.IndexOf("->", StringComparison.Ordinal);
        if (firstOffset == -1)
            return (null, "Invalid address: " + input);

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

        span = input.AsSpan(idxBeginLastOffset);
        int idxStaticDepth = span.IndexOf('[');

        ReadOnlySpan<char> remainingOffset = idxStaticDepth == -1 ? span : span.Slice(0, idxStaticDepth);
        if (!TryParseHexInt32(remainingOffset, out offset))
            return (null, "Invalid offset: " + remainingOffset.ToString());

        int staticOffsetCount = 0;
        if (idxStaticDepth != -1) {            
            int idxEnd = span.IndexOf(']');
            if (idxEnd == -1 || idxEnd < idxStaticDepth)
                return (null, "Invalid static depth value");

            span = span.Slice(idxStaticDepth + 1, idxEnd - idxStaticDepth - 1);
            if (!uint.TryParse(span, out uint maxStaticDepth) || maxStaticDepth > int.MaxValue) {
                return (null, "Invalid static depth value: " + span.ToString());
            }

            staticOffsetCount = (int) maxStaticDepth;
        }
        
        offsets.Add(offset);
        return (new DynamicAddress(baseAddress, offsets, staticOffsetCount), null);
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

        IBusyToken? token;
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

    public static Task<uint?> TryResolveAddress(this IMemoryAddress address, IConsoleConnection connection, bool invalidateCache = false) {
        if (address is StaticAddress)
            return Task.FromResult<uint?>(((StaticAddress) address).Address);
        if (address is DynamicAddress)
            return ((DynamicAddress) address).TryResolve(connection, invalidateCache);

        throw new ArgumentException("Unknown memory address type");
    }

    public static bool TryParseHexUInt32(string? input, out uint result) {
        return TryParseHexUInt32(input.AsSpan(), out result);
    }

    public static bool TryParseHexUInt32(ReadOnlySpan<char> input, out uint result) {
        return AddressParsing.TryParse32(input.ToString(), out result, out _, canParseAsExpression: true);
    }

    public static bool TryParseHexInt32(string? input, out int result) {
        return TryParseHexInt32(input.AsSpan(), out result);
    }

    public static bool TryParseHexInt32(ReadOnlySpan<char> input, out int result) {
        while (input.Length > 0) {
            if (input.StartsWith("-0x", StringComparison.OrdinalIgnoreCase))
                input = input.Slice(3);
            else if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                input = input.Slice(2);
            else
                break;
        }

        return int.TryParse(input, NumberStyles.HexNumber, null, out result);
    }
}