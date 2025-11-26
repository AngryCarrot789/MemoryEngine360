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
using MemEngine360.Engine;
using MemEngine360.ValueAbstraction;

namespace MemEngine360.BaseFrontEnd;

public class ScanResultCurrentValueConverter : IMultiValueConverter {
    public static ScanResultCurrentValueConverter Instance { get; } = new ScanResultCurrentValueConverter();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) {
        if (values[0] == AvaloniaProperty.UnsetValue || values[1] == AvaloniaProperty.UnsetValue) {
            return AvaloniaProperty.UnsetValue;
        }

        IDataValue value = (IDataValue) values[0]!;
        NumericDisplayType ndt = (NumericDisplayType) values[1]!;
        return DataValueUtils.GetStringFromDataValue(value, ndt, putStringInQuotes:true);
    }
}