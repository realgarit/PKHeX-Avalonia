using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class Roamer6EditorViewModel : ViewModelBase
{
    private readonly SAV6XY? _sav6xy;
    private readonly Roamer6? _roamer;

    private const int SpeciesOffset = 144; // Articuno = 144, Zapdos = 145, Moltres = 146

    public Roamer6EditorViewModel(SaveFile sav)
    {
        _sav6xy = sav as SAV6XY;
        IsSupported = _sav6xy?.Encount?.Roamer is not null;

        if (_sav6xy is not null)
            _roamer = _sav6xy.Encount.Roamer;

        // Build species list for the legendary birds
        var speciesNames = GameInfo.Strings.specieslist;
        SpeciesOptions.Add(speciesNames[(int)Species.Articuno]);
        SpeciesOptions.Add(speciesNames[(int)Species.Zapdos]);
        SpeciesOptions.Add(speciesNames[(int)Species.Moltres]);

        // Build roam status list
        StatusOptions.Add("Inactive");
        StatusOptions.Add("Roaming");
        StatusOptions.Add("Stationary");
        StatusOptions.Add("Defeated");
        StatusOptions.Add("Captured");

        if (_roamer is not null)
        {
            _selectedSpeciesIndex = _roamer.Species == 0 
                ? GetInitialIndex() 
                : _roamer.Species - SpeciesOffset;
            _timesEncountered = (int)_roamer.TimesEncountered;
            _selectedStatusIndex = (int)_roamer.RoamStatus;
        }
    }

    private int GetInitialIndex()
    {
        // If species not set, use starter choice to derive bird type
        if (_sav6xy?.EventWork is not null)
            return _sav6xy.EventWork.GetWork(48); // StarterChoiceIndex
        return 0;
    }

    public bool IsSupported { get; }

    [ObservableProperty]
    private ObservableCollection<string> _speciesOptions = [];

    [ObservableProperty]
    private ObservableCollection<string> _statusOptions = [];

    [ObservableProperty]
    private int _selectedSpeciesIndex;

    [ObservableProperty]
    private int _timesEncountered;

    [ObservableProperty]
    private int _selectedStatusIndex;

    partial void OnSelectedSpeciesIndexChanged(int value)
    {
        if (_roamer is not null)
            _roamer.Species = (ushort)(SpeciesOffset + value);
    }

    partial void OnTimesEncounteredChanged(int value)
    {
        if (_roamer is not null)
            _roamer.TimesEncountered = (uint)value;
    }

    partial void OnSelectedStatusIndexChanged(int value)
    {
        if (_roamer is not null)
            _roamer.RoamStatus = (Roamer6State)value;
    }
}
