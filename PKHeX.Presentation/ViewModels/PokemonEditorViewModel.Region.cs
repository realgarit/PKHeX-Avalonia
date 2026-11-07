using CommunityToolkit.Mvvm.ComponentModel;
using PKHeX.Core;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// 3DS console region settings stored in Gen 6/7 entities (<see cref="IRegionOrigin"/>).
/// </summary>
public partial class PokemonEditorViewModel
{
    public bool HasRegionOrigin => _pk is IRegionOrigin;

    [ObservableProperty] private int _originCountry;
    [ObservableProperty] private int _originSubRegion;
    [ObservableProperty] private int _originConsoleRegion;
    [ObservableProperty] private IReadOnlyList<ComboItem> _originCountryList = [];
    [ObservableProperty] private IReadOnlyList<ComboItem> _originSubRegionList = [];
    [ObservableProperty] private IReadOnlyList<ComboItem> _originConsoleRegionList = [];

    private bool _loadingRegionOrigin;

    private void LoadRegionOrigin()
    {
        var region = _pk is IRegionOrigin origin ? origin.GetRegionOrigin() : default;
        _loadingRegionOrigin = true;
        try
        {
            // Build the lists first so each stored value, known or not, has a matching choice.
            OriginCountryList = RegionChoices.Countries(region.Country);
            OriginSubRegionList = RegionChoices.SubRegions(region.Country, region.Region);
            OriginConsoleRegionList = RegionChoices.ConsoleRegions(region.ConsoleRegion);
            OriginCountry = region.Country;
            OriginSubRegion = region.Region;
            OriginConsoleRegion = region.ConsoleRegion;
            // Replacing a list clears the dropdown selection even when the value is unchanged.
            OnPropertyChanged(nameof(OriginCountry));
            OnPropertyChanged(nameof(OriginSubRegion));
            OnPropertyChanged(nameof(OriginConsoleRegion));
        }
        finally { _loadingRegionOrigin = false; }
        OnPropertyChanged(nameof(HasRegionOrigin));
    }

    private void ApplyRegionOrigin()
    {
        if (_pk is not IRegionOrigin origin) return;
        origin.Country = (byte)OriginCountry;
        origin.Region = (byte)OriginSubRegion;
        origin.ConsoleRegion = (byte)OriginConsoleRegion;
    }

    partial void OnOriginCountryChanged(int value)
    {
        if (_loadingRegionOrigin) return;
        // Sub-region IDs are per country: a user-picked country resets an ID it does not define.
        if (!RegionChoices.IsKnownSubRegion(value, OriginSubRegion)) OriginSubRegion = 0;
        OriginSubRegionList = RegionChoices.SubRegions(value, OriginSubRegion);
        OnPropertyChanged(nameof(OriginSubRegion)); // replacing the list cleared the dropdown selection
        if (!_isLoading) Validate();
    }

    partial void OnOriginSubRegionChanged(int value)
    {
        if (_loadingRegionOrigin) return;
        if (OriginSubRegionList.All(item => item.Value != value))
        {
            OriginSubRegionList = RegionChoices.SubRegions(OriginCountry, value);
            OnPropertyChanged(nameof(OriginSubRegion));
        }
        if (!_isLoading) Validate();
    }

    partial void OnOriginConsoleRegionChanged(int value)
    {
        if (_loadingRegionOrigin) return;
        if (OriginConsoleRegionList.All(item => item.Value != value))
        {
            OriginConsoleRegionList = RegionChoices.ConsoleRegions(value);
            OnPropertyChanged(nameof(OriginConsoleRegion));
        }
        if (!_isLoading) Validate();
    }
}
