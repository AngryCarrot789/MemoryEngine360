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
using Avalonia;
using Avalonia.Data.Converters;

namespace MemEngine360.BaseFrontEnd.MemRegions;

public class MemoryRegionProtectionConverter : IMultiValueConverter {
    public static MemoryRegionProtectionConverter Instance { get; } = new MemoryRegionProtectionConverter();
    
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) {
        if (values == null || values.Count != 2) {
            throw new ArgumentException("values must be of length 2");
        }

        if (!(values[0] is XboxMemoryRegionViewerUIControl control)) {
            throw new Exception("Value[0] is not " + nameof(XboxMemoryRegionViewerUIControl));
        }

        if (values[1] == AvaloniaProperty.UnsetValue) {
            return AvaloniaProperty.UnsetValue;
        }

        if (!(values[1] is uint protection)) {
            throw new Exception("Value[1] is not uint");
        }

        if (control.Info != null && control.Info.RegionFlagsToTextConverter != null) {
            return control.Info.RegionFlagsToTextConverter(protection);
        }

        return protection.ToString("X8");
    }
}