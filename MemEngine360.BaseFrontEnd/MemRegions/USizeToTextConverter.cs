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

using System.Globalization;
using Avalonia.Data.Converters;
using PFXToolKitUI.Interactivity.Formatting;

namespace MemEngine360.BaseFrontEnd.MemRegions;

public class USizeToTextConverter : IValueConverter {
    public static readonly USizeToTextConverter Instance = new USizeToTextConverter();
    
    internal static readonly AutoMemoryValueFormatter ByteFormatter = new AutoMemoryValueFormatter() {
        SourceFormat = MemoryFormatType.Byte,
        NonEditingRoundedPlaces = 1,
        AllowedFormats = [MemoryFormatType.Byte, MemoryFormatType.KiloByte1000, MemoryFormatType.MegaByte1000, MemoryFormatType.GigaByte1000, MemoryFormatType.TeraByte1000]
    };
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        uint size = (uint) value!;
        return $"{size:X8} ({ByteFormatter.ToString(size, false)})";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}