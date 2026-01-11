
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class PokemonEditorViewModel
{
    // Met Info
    [ObservableProperty]
    private int _originGame;

    [ObservableProperty]
    private int _metLocation;

    [ObservableProperty]
    private int _eggLocation;

    [ObservableProperty]
    private int _metLevel;

    [ObservableProperty]
    private DateTimeOffset? _metDate;

    [ObservableProperty]
    private DateTimeOffset? _eggDate;

    // Dynamic Lists
    [ObservableProperty]
    private ObservableCollection<ComboItem> _metLocationList = [];

    [ObservableProperty]
    private ObservableCollection<ComboItem> _eggLocationList = [];

    partial void OnOriginGameChanged(int value)
    {
        if (_isLoading) return;
        UpdateMetDataLists();
    }

    private void UpdateMetDataLists()
    {
        MetLocationList.Clear();
        var context = _sav.Context;
        var locations = GameInfo.Sources.Met.GetLocationList((GameVersion)OriginGame, context);
        foreach (var item in locations)
            MetLocationList.Add(item);

        EggLocationList.Clear();
        var eggLocations = GameInfo.Sources.Met.GetLocationList((GameVersion)OriginGame, context, egg: true);
        foreach (var item in eggLocations)
            EggLocationList.Add(item);
    }

    partial void OnMetLocationChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnMetLevelChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnMetDateChanged(DateTimeOffset? value) { if (!_isLoading) Validate(); }
    partial void OnEggLocationChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnEggDateChanged(DateTimeOffset? value) { if (!_isLoading) Validate(); }
    partial void OnIsFatefulEncounterChanged(bool value) { if (!_isLoading) Validate(); }
}
