
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

    [ObservableProperty] private bool _isIllegal;
    [ObservableProperty] private ObservableCollection<ComboItem> _metLocationList = [];
    [ObservableProperty] private ObservableCollection<ComboItem> _eggLocationList = [];

    partial void OnOriginGameChanged(int value)
    {
        if (_isLoading) return;
        UpdateMetDataLists();
    }

    private void UpdateMetDataLists()
    {
        // Store current values before clearing to prevent binding race condition
        var currentMetLocation = MetLocation;
        var currentEggLocation = EggLocation;
        
        MetLocationList.Clear();
        var context = _sav.Context;
        var locations = GameInfo.Sources.Met.GetLocationList((GameVersion)OriginGame, context);
        foreach (var item in locations)
            MetLocationList.Add(item);

        EggLocationList.Clear();
        var eggLocations = GameInfo.Sources.Met.GetLocationList((GameVersion)OriginGame, context, egg: true);
        foreach (var item in eggLocations)
            EggLocationList.Add(item);
        
        // Restore values if they exist in new lists
        if (_isLoading) return; // Don't reset values during load, let LoadFromPKM handle it

        if (MetLocationList.Any(l => l.Value == currentMetLocation))
            MetLocation = currentMetLocation;
        else if (MetLocationList.Count > 0)
            MetLocation = MetLocationList[0].Value;
            
        if (EggLocationList.Any(l => l.Value == currentEggLocation))
            EggLocation = currentEggLocation;
        else if (EggLocationList.Count > 0)
            EggLocation = EggLocationList[0].Value;
    }

    partial void OnMetLocationChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnMetLevelChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnMetDateChanged(DateTimeOffset? value) { if (!_isLoading) Validate(); }
    partial void OnEggLocationChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnEggDateChanged(DateTimeOffset? value) { if (!_isLoading) Validate(); }
    partial void OnIsFatefulEncounterChanged(bool value) { if (!_isLoading) Validate(); }
}
