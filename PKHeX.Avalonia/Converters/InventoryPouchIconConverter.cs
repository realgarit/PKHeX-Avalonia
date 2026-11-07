using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using PKHeX.Core;

namespace PKHeX.Avalonia.Converters;

/// <summary>Maps a Core pouch category to its shared vector icon in <c>Styles/Icons.axaml</c>.</summary>
public sealed class InventoryPouchIconConverter : IValueConverter
{
    /// <summary>Resource key of the icon for <paramref name="type"/>. Gem-like pouches share one gem glyph.</summary>
    public static string GetIconKey(InventoryType type) => type switch
    {
        InventoryType.KeyItems => "Icon.Pouch.KeyItems",
        InventoryType.TMHMs => "Icon.Pouch.TMHMs",
        InventoryType.Medicine => "Icon.Pouch.Medicine",
        InventoryType.Berries => "Icon.Pouch.Berries",
        InventoryType.Balls => "Icon.Pouch.Balls",
        InventoryType.BattleItems => "Icon.Pouch.BattleItems",
        InventoryType.MailItems => "Icon.Pouch.MailItems",
        InventoryType.PCItems => "Icon.Pouch.PCItems",
        InventoryType.FreeSpace => "Icon.Pouch.FreeSpace",
        InventoryType.ZCrystals or InventoryType.MegaStones or InventoryType.Treasure => "Icon.Pouch.Gem",
        InventoryType.Candy => "Icon.Pouch.Candy",
        InventoryType.Ingredients => "Icon.Pouch.Ingredients",
        _ => "Icon.Pouch.Items",
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not InventoryType type)
            return null;
        var app = global::Avalonia.Application.Current;
        return app is not null && app.TryFindResource(GetIconKey(type), out var resource) && resource is Geometry geometry
            ? geometry
            : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
