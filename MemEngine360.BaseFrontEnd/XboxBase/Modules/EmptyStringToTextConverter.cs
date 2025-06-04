using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace MemEngine360.BaseFrontEnd.XboxBase.Modules;

public class EmptyStringToTextConverter : IValueConverter {
    public static EmptyStringToTextConverter Instance { get; } = new EmptyStringToTextConverter();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value == AvaloniaProperty.UnsetValue)
            return value;
        return value is string text && !string.IsNullOrWhiteSpace(text) ? text : parameter?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}