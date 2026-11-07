using System;
using System.Collections.Concurrent;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace PKHeX.Avalonia.Converters;

/// <summary>
/// Converts a framework-free "#RRGGBB" color string from a ViewModel into a shared immutable brush.
/// </summary>
public sealed class HexColorToBrushConverter : IValueConverter
{
    public static readonly HexColorToBrushConverter Instance = new();

    private static readonly ConcurrentDictionary<string, IBrush> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || !Color.TryParse(hex, out var color))
            return null;
        return Cache.GetOrAdd(hex, _ => new ImmutableSolidColorBrush(color));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
