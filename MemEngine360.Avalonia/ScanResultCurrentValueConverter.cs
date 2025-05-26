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

using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using MemEngine360.Engine;
using MemEngine360.ValueAbstraction;

namespace MemEngine360.Avalonia;

public class ScanResultCurrentValueConverter : IMultiValueConverter {
    public static ScanResultCurrentValueConverter Instance { get; } = new ScanResultCurrentValueConverter();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) {
        if (values[0] == AvaloniaProperty.UnsetValue || values[1] == AvaloniaProperty.UnsetValue) {
            return AvaloniaProperty.UnsetValue;
        }

        IDataValue value = (IDataValue) values[0]!;
        NumericDisplayType ndt = (NumericDisplayType) values[1]!;
        return MemoryEngine360.GetStringFromDataValue(value, ndt, putStringInQuotes:true);
    }
}