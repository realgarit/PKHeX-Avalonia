
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
    private DateTime? _metDate;
    [ObservableProperty]
    private DateTime? _eggDate;

    [ObservableProperty] private bool _isIllegal;
    [ObservableProperty] private ObservableCollection<ComboItem> _metLocationList = [];
    [ObservableProperty] private ObservableCollection<ComboItem> _eggLocationList = [];

    public bool HasMetDate => _sav.Generation >= 4;

    partial void OnOriginGameChanged(int value)
    {
        if (_isLoading) return;
        UpdateMetDataLists();
    }

    private void UpdateMetDataLists(bool preserveSelection = true)
    {
        // Store current values
        var currentMetLocation = MetLocation;
        var currentEggLocation = EggLocation;
        
        var newMetList = new ObservableCollection<ComboItem>();
        var context = _sav.Context;
        var locations = GameInfo.Sources.Met.GetLocationList((GameVersion)OriginGame, context);
        foreach (var item in locations)
            newMetList.Add(item);
        MetLocationList = newMetList;

        var newEggList = new ObservableCollection<ComboItem>();
        var eggLocations = GameInfo.Sources.Met.GetLocationList((GameVersion)OriginGame, context, egg: true);
        foreach (var item in eggLocations)
            newEggList.Add(item);
        EggLocationList = newEggList;
        
        if (_isLoading || !preserveSelection) return; 

        // Restore values if they exist in new lists
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
    partial void OnMetDateChanged(DateTime? value) { if (!_isLoading) Validate(); }
    partial void OnEggLocationChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnEggDateChanged(DateTime? value) { if (!_isLoading) Validate(); }
    partial void OnIsFatefulEncounterChanged(bool value) { if (!_isLoading) Validate(); }
}
