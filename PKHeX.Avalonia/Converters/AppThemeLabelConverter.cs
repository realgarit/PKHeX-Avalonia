using System;
using System.Globalization;
using Avalonia.Data.Converters;
using PKHeX.Application.Abstractions;
using PKHeX.Presentation.Localization;

namespace PKHeX.Avalonia.Converters;

/// <summary>Maps <see cref="AppTheme"/> values to user-facing labels.</summary>
public sealed class AppThemeLabelConverter : IValueConverter, IMultiValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        AppTheme.Dark => LocalizedStrings.Instance["Settings_Theme_Dark"],
        AppTheme.Light => LocalizedStrings.Instance["Settings_Theme_Light"],
        AppTheme.HighContrast => "High Contrast",
        AppTheme.System => "Follow System",
        _ => value?.ToString(),
    };

    // The second binding is CurrentLanguage; its notifications refresh enum labels in open pickers.
    public object? Convert(System.Collections.Generic.IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => Convert(values.Count > 0 ? values[0] : null, targetType, parameter, culture);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
