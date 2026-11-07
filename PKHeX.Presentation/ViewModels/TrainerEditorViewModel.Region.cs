using CommunityToolkit.Mvvm.ComponentModel;
using PKHeX.Core;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// 3DS region settings and the save's Vivillon pattern for saves edited by the generic trainer
/// form (Gen 6). Gen 7 edits the same fields in its dedicated workspace.
/// Values are written only when changed, so a no-op Apply leaves the stored bytes untouched.
/// </summary>
public partial class TrainerEditorViewModel
{
    [ObservableProperty] private bool _hasRegionSettings;
    [ObservableProperty] private int _country;
    [ObservableProperty] private int _subRegion;
    [ObservableProperty] private int _consoleRegion;
    [ObservableProperty] private IReadOnlyList<ComboItem> _countryList = [];
    [ObservableProperty] private IReadOnlyList<ComboItem> _subRegionList = [];
    [ObservableProperty] private IReadOnlyList<ComboItem> _consoleRegionList = [];

    [ObservableProperty] private bool _hasVivillon;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private int _vivillon;
    [ObservableProperty] private IReadOnlyList<ComboItem> _vivillonList = [];

    private GeoRegion3DS _loadedRegion;
    private int _loadedVivillon;
    private bool _loadingRegions;

    /// <summary>The demo save keeps a Vivillon byte that the game never uses.</summary>
    private SAV6? VivillonSave => _sav is SAV6 sav6 and not SAV6AODemo ? sav6 : null;

    private void LoadRegionSettings()
    {
        _loadingRegions = true;
        try
        {
            HasRegionSettings = _sav is IRegionOrigin && !HasDedicatedTrainerWorkspace;
            _loadedRegion = _sav is IRegionOrigin origin ? origin.GetRegionOrigin() : default;
            Country = _loadedRegion.Country;
            SubRegion = _loadedRegion.Region;
            ConsoleRegion = _loadedRegion.ConsoleRegion;

            HasVivillon = VivillonSave is not null && !HasDedicatedTrainerWorkspace;
            _loadedVivillon = VivillonSave?.Vivillon ?? 0;
            Vivillon = _loadedVivillon;
            RefreshRegionLists();
        }
        finally { _loadingRegions = false; }
    }

    private void RefreshRegionLists()
    {
        bool wasLoading = _loadingRegions;
        _loadingRegions = true;
        try
        {
            int country = Country, subRegion = SubRegion, console = ConsoleRegion, vivillon = Vivillon;
            CountryList = RegionChoices.Countries(country);
            SubRegionList = RegionChoices.SubRegions(country, subRegion);
            ConsoleRegionList = RegionChoices.ConsoleRegions(console);
            VivillonList = HasVivillon ? RegionChoices.VivillonPatterns(vivillon, _sav.Context) : [];
            // Replacing a list clears the dropdown selection; restore and re-announce the stored values.
            Country = country; SubRegion = subRegion; ConsoleRegion = console; Vivillon = vivillon;
            OnPropertyChanged(nameof(Country)); OnPropertyChanged(nameof(SubRegion));
            OnPropertyChanged(nameof(ConsoleRegion)); OnPropertyChanged(nameof(Vivillon));
        }
        finally { _loadingRegions = wasLoading; }
    }

    partial void OnCountryChanged(int value)
    {
        if (_loadingRegions) return;
        // Sub-region IDs are per country: a user-picked country resets an ID it does not define.
        if (!RegionChoices.IsKnownSubRegion(value, SubRegion)) SubRegion = 0;
        SubRegionList = RegionChoices.SubRegions(value, SubRegion);
        OnPropertyChanged(nameof(SubRegion)); // replacing the list cleared the dropdown selection
    }

    partial void OnSubRegionChanged(int value)
    {
        if (!_loadingRegions && SubRegionList.All(item => item.Value != value))
        {
            SubRegionList = RegionChoices.SubRegions(Country, value);
            OnPropertyChanged(nameof(SubRegion));
        }
    }

    partial void OnConsoleRegionChanged(int value)
    {
        if (!_loadingRegions && ConsoleRegionList.All(item => item.Value != value))
        {
            ConsoleRegionList = RegionChoices.ConsoleRegions(value);
            OnPropertyChanged(nameof(ConsoleRegion));
        }
    }

    partial void OnVivillonChanged(int value)
    {
        if (!_loadingRegions && HasVivillon && VivillonList.All(item => item.Value != value))
        {
            VivillonList = RegionChoices.VivillonPatterns(value, _sav.Context);
            OnPropertyChanged(nameof(Vivillon));
        }
    }

    private bool IsRegionInputValid =>
        (!HasRegionSettings || Country is >= 0 and <= byte.MaxValue && SubRegion is >= 0 and <= byte.MaxValue && ConsoleRegion is >= 0 and <= byte.MaxValue) &&
        (!HasVivillon || Vivillon == _loadedVivillon || Vivillon is >= 0 and <= 19);

    private void SaveRegionSettings()
    {
        if (HasRegionSettings && _sav is IRegionOrigin origin)
        {
            if (Country != _loadedRegion.Country) origin.Country = (byte)Country;
            if (SubRegion != _loadedRegion.Region) origin.Region = (byte)SubRegion;
            if (ConsoleRegion != _loadedRegion.ConsoleRegion) origin.ConsoleRegion = (byte)ConsoleRegion;
            _loadedRegion = origin.GetRegionOrigin();
        }
        if (HasVivillon && VivillonSave is { } sav6 && Vivillon != _loadedVivillon)
        {
            sav6.Vivillon = Vivillon;
            _loadedVivillon = sav6.Vivillon;
        }
    }
}
