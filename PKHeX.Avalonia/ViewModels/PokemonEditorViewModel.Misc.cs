
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class PokemonEditorViewModel
{
    // Contest Stats (Gen 3-6)
    [ObservableProperty]
    private int _contestCool;

    [ObservableProperty]
    private int _contestBeauty;

    [ObservableProperty]
    private int _contestCute;

    [ObservableProperty]
    private int _contestSmart;

    [ObservableProperty]
    private int _contestTough;

    [ObservableProperty]
    private int _contestSheen;

    public bool HasContestStats => _pk is IContestStatsReadOnly;

    // Markings (all generations support some subset)
    [ObservableProperty]
    private bool _markingCircle;

    [ObservableProperty]
    private bool _markingTriangle;

    [ObservableProperty]
    private bool _markingSquare;

    [ObservableProperty]
    private bool _markingHeart;

    [ObservableProperty]
    private bool _markingStar;

    [ObservableProperty]
    private bool _markingDiamond;

    public bool HasMarkings => _pk is IAppliedMarkings;
    public bool HasSixMarkings => _pk is IAppliedMarkings4 or IAppliedMarkings7;

    // Memories (Gen 6+)
    [ObservableProperty]
    private int _otMemory;

    [ObservableProperty]
    private int _otMemoryIntensity;

    [ObservableProperty]
    private int _otMemoryFeeling;

    [ObservableProperty]
    private int _otMemoryVariable;

    [ObservableProperty]
    private int _htMemory;

    [ObservableProperty]
    private int _htMemoryIntensity;

    [ObservableProperty]
    private int _htMemoryFeeling;

    [ObservableProperty]
    private int _htMemoryVariable;

    public bool HasMemories => _pk is IMemoryOT;

    // Ribbons
    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<RibbonItemViewModel> _ribbons = [];
    
    public bool HasRibbons => Ribbons.Count > 0;
    public int RibbonCount => Ribbons.Count(r => r.IsBooleanRibbon ? r.HasRibbon : r.RibbonCount > 0);
    
    private void LoadRibbons()
    {
        var ribbonInfos = RibbonInfo.GetRibbonInfo(_pk);
        Ribbons = new System.Collections.ObjectModel.ObservableCollection<RibbonItemViewModel>(
            ribbonInfos.Select(r => new RibbonItemViewModel(_pk, r))
        );
        OnPropertyChanged(nameof(HasRibbons));
        OnPropertyChanged(nameof(RibbonCount));
    }

    [RelayCommand]
    private void SetAllRibbons()
    {
        foreach (var ribbon in Ribbons)
        {
            if (ribbon.IsBooleanRibbon)
                ribbon.HasRibbon = true;
            else
                ribbon.RibbonCount = ribbon.MaxCount;
        }
        OnPropertyChanged(nameof(RibbonCount));
    }

    [RelayCommand]
    private void ClearRibbons()
    {
        foreach (var ribbon in Ribbons)
        {
            if (ribbon.IsBooleanRibbon)
                ribbon.HasRibbon = false;
            else
                ribbon.RibbonCount = 0;
        }
        OnPropertyChanged(nameof(RibbonCount));
    }

    // PID/EC
    [ObservableProperty]
    private string _pid = string.Empty;

    [ObservableProperty]
    private string _encryptionConstant = string.Empty;

    // EXP & Friendship
    [ObservableProperty]
    private long _exp;

    [ObservableProperty]
    private int _happiness;

    // Pokerus
    [ObservableProperty]
    private int _pkrsStrain;

    [ObservableProperty]
    private int _pkrsDays;
    
    [ObservableProperty]
    private bool _isPokerusInfected;

    [ObservableProperty]
    private bool _isPokerusCured;

    // OT Info
    [ObservableProperty]
    private string _originalTrainerName = string.Empty;

    [ObservableProperty]
    private long _trainerID;

    [ObservableProperty]
    private int _originalTrainerGender;

    [ObservableProperty]
    private int _originalTrainerFriendship;

    [ObservableProperty]
    private string _handlingTrainerName = string.Empty;

    [ObservableProperty]
    private int _handlingTrainerGender;

    [ObservableProperty]
    private int _handlingTrainerFriendship;

    [ObservableProperty]
    private int _currentHandler;
    
    // Group 4: Misc
    [ObservableProperty]
    private int _abilityNumber;

    partial void OnOriginalTrainerGenderChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnTrainerIDChanged(long value) { if (!_isLoading) Validate(); }
}
