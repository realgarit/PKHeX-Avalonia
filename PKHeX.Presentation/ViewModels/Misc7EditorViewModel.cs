using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PKHeX.Presentation.Models;
using PKHeX.Core;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// Misc editor for Gen 7 saves covering Battle Tree, Poké Finder, Stamps, and Fly destinations.
/// </summary>
public partial class Misc7EditorViewModel : ViewModelBase, IDisposable
{
    private SAV7 _sav;
    private SAV7 _baseline;
    private readonly TrainerBlockDataSession<SAV7> _session;
    private bool _closed;
    private int _epoch;
    private readonly IDialogService? _dialogs;
    public bool CanUndoFashion => !_closed && _session.CanUndo;
    public bool HasError => Error.Length != 0;
    [ObservableProperty] private string _error = string.Empty;

    public Misc7EditorViewModel(SAV7 sav, IDialogService? dialogs = null)
    {
        _session = new(sav);
        _dialogs = dialogs;
        _sav = _session.Staged;
        _baseline = (SAV7)_sav.Clone();
        LoadTrainerFields();
        LoadBattleTree();
        LoadPokeFinder();
        LoadStamps();
        LoadFlyDestinations();
        if (sav is SAV7USUM)
            LoadUltraData();
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, static (recipient, _) => ((Misc7EditorViewModel)recipient).RefreshLanguage());
    }

    public bool IsUSUM => _sav is SAV7USUM;
    public bool IsSM => _sav is SAV7SM;

    #region Battle Tree

    // Regular mode (current/max)
    [ObservableProperty] private int _singleCurrentStreak;
    [ObservableProperty] private int _singleMaxStreak;
    [ObservableProperty] private int _doubleCurrentStreak;
    [ObservableProperty] private int _doubleMaxStreak;
    [ObservableProperty] private int _multiCurrentStreak;
    [ObservableProperty] private int _multiMaxStreak;

    // Super mode (current/max)
    [ObservableProperty] private int _superSingleCurrentStreak;
    [ObservableProperty] private int _superSingleMaxStreak;
    [ObservableProperty] private int _superDoubleCurrentStreak;
    [ObservableProperty] private int _superDoubleMaxStreak;
    [ObservableProperty] private int _superMultiCurrentStreak;
    [ObservableProperty] private int _superMultiMaxStreak;

    // Unlock flags
    [ObservableProperty] private bool _superSingleUnlocked;
    [ObservableProperty] private bool _superDoubleUnlocked;
    [ObservableProperty] private bool _superMultiUnlocked;

    private void LoadBattleTree()
    {
        var bt = _sav.BattleTree;

        // Regular
        SingleCurrentStreak = bt.GetTreeStreak(0, super: false, max: false);
        SingleMaxStreak = bt.GetTreeStreak(0, super: false, max: true);
        DoubleCurrentStreak = bt.GetTreeStreak(1, super: false, max: false);
        DoubleMaxStreak = bt.GetTreeStreak(1, super: false, max: true);
        MultiCurrentStreak = bt.GetTreeStreak(2, super: false, max: false);
        MultiMaxStreak = bt.GetTreeStreak(2, super: false, max: true);

        // Super
        SuperSingleCurrentStreak = bt.GetTreeStreak(0, super: true, max: false);
        SuperSingleMaxStreak = bt.GetTreeStreak(0, super: true, max: true);
        SuperDoubleCurrentStreak = bt.GetTreeStreak(1, super: true, max: false);
        SuperDoubleMaxStreak = bt.GetTreeStreak(1, super: true, max: true);
        SuperMultiCurrentStreak = bt.GetTreeStreak(2, super: true, max: false);
        SuperMultiMaxStreak = bt.GetTreeStreak(2, super: true, max: true);

        // Unlock flags
        SuperSingleUnlocked = _sav.EventWork.GetEventFlag(333);
        SuperDoubleUnlocked = _sav.EventWork.GetEventFlag(334);
        SuperMultiUnlocked = _sav.EventWork.GetEventFlag(335);
    }

    private void SaveBattleTree()
    {
        var bt = _sav.BattleTree;

        // Regular
        if (SingleCurrentStreak != _baseline.BattleTree.GetTreeStreak(0, super: false, max: false)) bt.SetTreeStreak(SingleCurrentStreak, 0, super: false, max: false);
        if (SingleMaxStreak != _baseline.BattleTree.GetTreeStreak(0, super: false, max: true)) bt.SetTreeStreak(SingleMaxStreak, 0, super: false, max: true);
        if (DoubleCurrentStreak != _baseline.BattleTree.GetTreeStreak(1, super: false, max: false)) bt.SetTreeStreak(DoubleCurrentStreak, 1, super: false, max: false);
        if (DoubleMaxStreak != _baseline.BattleTree.GetTreeStreak(1, super: false, max: true)) bt.SetTreeStreak(DoubleMaxStreak, 1, super: false, max: true);
        if (MultiCurrentStreak != _baseline.BattleTree.GetTreeStreak(2, super: false, max: false)) bt.SetTreeStreak(MultiCurrentStreak, 2, super: false, max: false);
        if (MultiMaxStreak != _baseline.BattleTree.GetTreeStreak(2, super: false, max: true)) bt.SetTreeStreak(MultiMaxStreak, 2, super: false, max: true);

        // Super
        if (SuperSingleCurrentStreak != _baseline.BattleTree.GetTreeStreak(0, super: true, max: false)) bt.SetTreeStreak(SuperSingleCurrentStreak, 0, super: true, max: false);
        if (SuperSingleMaxStreak != _baseline.BattleTree.GetTreeStreak(0, super: true, max: true)) bt.SetTreeStreak(SuperSingleMaxStreak, 0, super: true, max: true);
        if (SuperDoubleCurrentStreak != _baseline.BattleTree.GetTreeStreak(1, super: true, max: false)) bt.SetTreeStreak(SuperDoubleCurrentStreak, 1, super: true, max: false);
        if (SuperDoubleMaxStreak != _baseline.BattleTree.GetTreeStreak(1, super: true, max: true)) bt.SetTreeStreak(SuperDoubleMaxStreak, 1, super: true, max: true);
        if (SuperMultiCurrentStreak != _baseline.BattleTree.GetTreeStreak(2, super: true, max: false)) bt.SetTreeStreak(SuperMultiCurrentStreak, 2, super: true, max: false);
        if (SuperMultiMaxStreak != _baseline.BattleTree.GetTreeStreak(2, super: true, max: true)) bt.SetTreeStreak(SuperMultiMaxStreak, 2, super: true, max: true);

        // Unlock flags
        if (SuperSingleUnlocked != _baseline.EventWork.GetEventFlag(333)) _sav.EventWork.SetEventFlag(333, SuperSingleUnlocked);
        if (SuperDoubleUnlocked != _baseline.EventWork.GetEventFlag(334)) _sav.EventWork.SetEventFlag(334, SuperDoubleUnlocked);
        if (SuperMultiUnlocked != _baseline.EventWork.GetEventFlag(335)) _sav.EventWork.SetEventFlag(335, SuperMultiUnlocked);
    }

    [RelayCommand]
    private void UnlockAllBattleTreeModes()
    {
        SuperSingleUnlocked = true;
        SuperDoubleUnlocked = true;
        SuperMultiUnlocked = true;
    }

    #endregion

    #region Poké Finder

    [ObservableProperty] private uint _snapCount;
    [ObservableProperty] private uint _thumbsTotal;
    [ObservableProperty] private uint _thumbsRecord;
    [ObservableProperty] private int _cameraVersion;
    [ObservableProperty] private bool _gyroEnabled;

    public IReadOnlyList<ComboItem> CameraVersions
    {
        get
        {
            string name = Localization.LocalizedStrings.Instance["Misc7Editor_PokeFinder"];
            var values = new List<ComboItem> { new(name, 0), new(name + " 2.0", 1), new(name + " 2.1", 2) };
            if (CameraVersion > 2) values.Add(new(Localization.LocalizedStrings.Instance.Format("RaidSession_UnknownType", CameraVersion), CameraVersion));
            return values;
        }
    }

    private void LoadPokeFinder()
    {
        SnapCount = _sav.PokeFinder.SnapCount;
        ThumbsTotal = _sav.PokeFinder.ThumbsTotalValue;
        ThumbsRecord = _sav.PokeFinder.ThumbsHighValue;
        CameraVersion = _sav.PokeFinder.CameraVersion;
        GyroEnabled = _sav.PokeFinder.GyroFlag;
    }

    private void SavePokeFinder()
    {
        if (SnapCount != _baseline.PokeFinder.SnapCount) _sav.PokeFinder.SnapCount = SnapCount;
        if (ThumbsTotal != _baseline.PokeFinder.ThumbsTotalValue) _sav.PokeFinder.ThumbsTotalValue = ThumbsTotal;
        if (ThumbsRecord != _baseline.PokeFinder.ThumbsHighValue) _sav.PokeFinder.ThumbsHighValue = ThumbsRecord;
        if (CameraVersion != _baseline.PokeFinder.CameraVersion) _sav.PokeFinder.CameraVersion = (ushort)CameraVersion;
        if (GyroEnabled != _baseline.PokeFinder.GyroFlag) _sav.PokeFinder.GyroFlag = GyroEnabled;
    }

    [RelayCommand]
    private void MaxPokeFinder()
    {
        SnapCount = 999999;
        ThumbsTotal = 9999999;
        ThumbsRecord = 9999999;
        CameraVersion = 2;
    }

    #endregion

    #region Stamps

    [ObservableProperty]
    private ObservableCollection<StampViewModel> _stamps = [];

    private void LoadStamps()
    {
        var stampNames = Enum.GetNames<Stamp7>();
        uint stampBits = _sav.Misc.Stamps;

        Stamps.Clear();
        for (int i = 0; i < stampNames.Length; i++)
        {
            bool obtained = (stampBits & (1u << i)) != 0;
            Stamps.Add(new StampViewModel(i, Localization.LocalizedStrings.Instance["Trainer7_Stamp_" + i], obtained));
        }
    }

    private void SaveStamps()
    {
        uint bits = 0;
        for (int i = 0; i < Stamps.Count; i++)
        {
            if (Stamps[i].IsObtained)
                bits |= 1u << i;
        }
        if (bits != _baseline.Misc.Stamps) _sav.Misc.Stamps = bits;
    }

    [RelayCommand]
    private void UnlockAllStamps()
    {
        foreach (var stamp in Stamps)
            stamp.IsObtained = true;
    }

    #endregion

    #region Fly Destinations

    [ObservableProperty]
    private ObservableCollection<FlyDestination7ViewModel> _flyDestinations = [];

    public ObservableCollection<FlyDestination7ViewModel> MapUnmask { get; } = [];

    private void LoadFlyDestinations()
    {
        FlyDestinations.Clear(); MapUnmask.Clear();
        foreach (var item in Gen7TrainerMapFlags.GetFlyDestinations(_sav))
            FlyDestinations.Add(new(item.FlagIndex, GetMapFlagName(item), _sav.EventWork.GetEventFlag(item.FlagIndex)));
        foreach (var item in Gen7TrainerMapFlags.GetMapUnmask(_sav))
            MapUnmask.Add(new(item.FlagIndex, GetMapFlagName(item), _sav.EventWork.GetEventFlag(item.FlagIndex)));
    }

    private static string GetMapFlagName(Gen7TrainerMapFlag item)
    {
        if (item.AlternateName != Gen7MapAlternateName.None)
            return Localization.LocalizedStrings.Instance["Trainer7_Map_" + item.AlternateName];
        return GameInfo.GetLocationList(GameVersion.US, EntityContext.Gen7, false).FirstOrDefault(location => location.Value == item.LocationId)?.Text
            ?? Localization.LocalizedStrings.Instance.Format("RaidSession_UnknownType", item.LocationId);
    }

    private void SaveFlyDestinations()
    {
        foreach (var row in FlyDestinations.Concat(MapUnmask))
            if (row.IsUnlocked != _baseline.EventWork.GetEventFlag(row.Index))
                _sav.EventWork.SetEventFlag(row.Index, row.IsUnlocked);
    }

    [RelayCommand]
    private void UnlockAllFlyDestinations()
    {
        foreach (var dest in FlyDestinations)
            dest.IsUnlocked = true;
    }

    #endregion

    #region Ultra Data (USUM only)

    [ObservableProperty] private int _mantineSurf0;
    [ObservableProperty] private int _mantineSurf1;
    [ObservableProperty] private int _mantineSurf2;
    [ObservableProperty] private int _mantineSurf3;

    private void LoadUltraData()
    {
        MantineSurf0 = _sav.Misc.GetSurfScore(0);
        MantineSurf1 = _sav.Misc.GetSurfScore(1);
        MantineSurf2 = _sav.Misc.GetSurfScore(2);
        MantineSurf3 = _sav.Misc.GetSurfScore(3);
    }

    private void SaveUltraData()
    {
        if (_sav is not SAV7USUM) return;

        if (MantineSurf0 != _baseline.Misc.GetSurfScore(0)) _sav.Misc.SetSurfScore(0, MantineSurf0);
        if (MantineSurf1 != _baseline.Misc.GetSurfScore(1)) _sav.Misc.SetSurfScore(1, MantineSurf1);
        if (MantineSurf2 != _baseline.Misc.GetSurfScore(2)) _sav.Misc.SetSurfScore(2, MantineSurf2);
        if (MantineSurf3 != _baseline.Misc.GetSurfScore(3)) _sav.Misc.SetSurfScore(3, MantineSurf3);
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void Save()
    {
        if (_closed) return;
        if (!CanSave) { Error = Localization.LocalizedStrings.Instance["Trainer7_InvalidValues"]; return; }
        _epoch++;
        if (!_session.TryCommit(staged =>
        {
            _sav = staged;
            SaveTrainerFields(); SaveBattleTree(); SavePokeFinder(); SaveStamps(); SaveFlyDestinations(); SaveUltraData();
        }))
        { _sav = _session.Staged; Error = Localization.LocalizedStrings.Instance["LgpeTrainer_Conflict"]; return; }
        bool locksChanged = !HasSameTeamLocks(_baseline, _session.Staged);
        _sav = _session.Staged; _baseline = (SAV7)_sav.Clone();
        LoadTrainerFields(); LoadBattleTree(); LoadPokeFinder(); LoadStamps(); LoadFlyDestinations(); if (IsUSUM) LoadUltraData();
        Error = string.Empty;
        OnPropertyChanged(nameof(CanUndoFashion));
        NotifyBattleTeams();
        if (locksChanged) BattleLocksCommitted?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Reset()
    {
        if (_closed) return;
        _epoch++;
        _session.Reset(); _sav = _session.Staged; _baseline = (SAV7)_sav.Clone();
        LoadTrainerFields(); LoadBattleTree(); LoadPokeFinder(); LoadStamps(); LoadFlyDestinations();
        if (IsUSUM) LoadUltraData(); Error = string.Empty;
        OnPropertyChanged(nameof(CanUndoFashion));
        NotifyBattleTeams();
    }
    [RelayCommand]
    private async Task FashionAsync(Gen7FashionMode mode)
    {
        if (_closed || _dialogs is null || Gender is not (0 or 1)) return;
        int epoch = _epoch, gender = Gender;
        var strings = Localization.LocalizedStrings.Instance;
        if (!await _dialogs.ShowConfirmationAsync(strings["Trainer7_Fashion"], strings["Trainer7_FashionConfirm"], strings["TrainerEditor_ApplyChanges"], strings["Common_Cancel"])) return;
        if (_closed || epoch != _epoch || gender != Gender) return;
        _session.ApplyAction(save => Gen7TrainerFashion.Apply(save, gender, mode));
        _sav = _session.Staged; _epoch++; Error = string.Empty;
        OnPropertyChanged(nameof(CanUndoFashion));
    }
    [RelayCommand]
    private void UndoFashion()
    {
        if (_closed) return;
        _session.Undo(); _sav = _session.Staged; _epoch++;
        OnPropertyChanged(nameof(CanUndoFashion));
        NotifyBattleTeams();
    }

    #region Battle Teams

    /// <summary>Raised after Apply wrote a Battle Team lock change to the live save.</summary>
    public event EventHandler? BattleLocksCommitted;

    /// <summary>True when any of the six staged Battle Teams is locked for a competition.</summary>
    public bool IsBattleTeamLocked => _session.Staged.BoxLayout.IsAnyTeamLocked();
    private bool CanUnlockBattleTeams() => !_closed && IsBattleTeamLocked;

    /// <summary>Stages <see cref="BoxLayout7.UnlockAllTeams"/>; Apply writes it, Undo/Reset discard it.</summary>
    [RelayCommand(CanExecute = nameof(CanUnlockBattleTeams))]
    private void UnlockBattleTeams()
    {
        if (_closed) return;
        _session.ApplyAction(static save => save.BoxLayout.UnlockAllTeams());
        _sav = _session.Staged; _epoch++; Error = string.Empty;
        OnPropertyChanged(nameof(CanUndoFashion));
        NotifyBattleTeams();
    }

    private static bool HasSameTeamLocks(SAV7 a, SAV7 b)
    {
        for (int team = 0; team < 6; team++)
            if (a.BoxLayout.GetIsTeamLocked(team) != b.BoxLayout.GetIsTeamLocked(team)) return false;
        return true;
    }

    private void NotifyBattleTeams()
    {
        OnPropertyChanged(nameof(IsBattleTeamLocked));
        UnlockBattleTeamsCommand.NotifyCanExecuteChanged();
    }

    #endregion
    public void Dispose() { _closed = true; _epoch++; WeakReferenceMessenger.Default.UnregisterAll(this); UnlockBattleTeamsCommand.NotifyCanExecuteChanged(); }
    public void RefreshLanguage()
    {
        int country = Country, region = SubRegion, language = Language, gender = Gender, console = ConsoleRegion, skin = SkinColor, style = BattleStyle, camera = CameraVersion, vivillon = Vivillon;
        ulong time = AlolaOffset;
        _loadingRegions = true;
        try
        {
            Countries = RegionChoices.Countries(country); Regions = RegionChoices.SubRegions(country, region);
            VivillonPatterns = RegionChoices.VivillonPatterns(vivillon, EntityContext.Gen7);
            foreach (var property in new[] { nameof(Genders), nameof(Languages), nameof(ConsoleRegions), nameof(SkinColors), nameof(BattleStyles), nameof(TimeOffsets), nameof(CameraVersions) }) OnPropertyChanged(property);
            Country = country; SubRegion = region; Language = language; Gender = gender; ConsoleRegion = console; SkinColor = skin; BattleStyle = style; CameraVersion = camera; AlolaOffset = time; Vivillon = vivillon;
            // Replacing these lists clears the dropdown selection even when the stored value is unchanged.
            OnPropertyChanged(nameof(Country)); OnPropertyChanged(nameof(SubRegion)); OnPropertyChanged(nameof(Vivillon));
        }
        finally { _loadingRegions = false; }
        foreach (var row in StyleFlags) row.Name = Localization.LocalizedStrings.Instance["Trainer7_Style_" + row.Id];
        foreach (var row in Stamps) row.Name = Localization.LocalizedStrings.Instance["Trainer7_Stamp_" + row.Index];
        foreach (var definition in Gen7TrainerMapFlags.GetFlyDestinations(_sav).Concat(Gen7TrainerMapFlags.GetMapUnmask(_sav)))
        {
            var row = FlyDestinations.Concat(MapUnmask).First(item => item.Index == definition.FlagIndex);
            row.Name = GetMapFlagName(definition);
        }
    }
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (args.PropertyName != nameof(CanSave)) base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(CanSave)));
    }
    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));
    #endregion
}

public partial class StampViewModel : ObservableObject
{
    public StampViewModel(int index, string name, bool isObtained)
    {
        Index = index;
        Name = name;
        _isObtained = isObtained;
    }

    public int Index { get; }
    [ObservableProperty] private string _name = string.Empty;

    [ObservableProperty]
    private bool _isObtained;
}

public partial class FlyDestination7ViewModel : ObservableObject
{
    public FlyDestination7ViewModel(int index, string name, bool isUnlocked)
    {
        Index = index;
        Name = name;
        _isUnlocked = isUnlocked;
    }

    public int Index { get; }
    [ObservableProperty] private string _name = string.Empty;

    [ObservableProperty]
    private bool _isUnlocked;
}
