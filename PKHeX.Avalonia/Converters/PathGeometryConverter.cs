using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PKHeX.Avalonia.Converters;

/// <summary>Converts the presentation-layer Fluent icon path data into a native Avalonia geometry.</summary>
public sealed class PathGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
            return null;

        return StreamGeometry.Parse(path);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
