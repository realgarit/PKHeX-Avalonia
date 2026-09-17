using System;
using System.Globalization;
using Avalonia.Data.Converters;
using PKHeX.Application.Abstractions;
using PKHeX.Presentation.Localization;

namespace PKHeX.Avalonia.Converters;

/// <summary>Maps density preferences to localized settings labels.</summary>
public sealed class AppDensityLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        AppDensity.Compact => LocalizedStrings.Instance["Settings_Density_Compact"],
        AppDensity.Comfortable => LocalizedStrings.Instance["Settings_Density_Comfortable"],
        _ => value?.ToString(),
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
