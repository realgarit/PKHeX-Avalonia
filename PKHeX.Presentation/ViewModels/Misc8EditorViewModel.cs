using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using CommunityToolkit.Mvvm.Messaging;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// Misc editor for Gen 8 Sword/Shield saves covering Battle Tower, Watts, BP.
/// </summary>
public partial class Misc8EditorViewModel : ViewModelBase, IDisposable
{
    private SAV8SWSH _sav;
    private SAV8SWSH _baseline;
    private readonly TrainerScBlockDataSession<SAV8SWSH> _session;
    private readonly IDialogService? _dialogs;
    private bool _closed;
    private int _epoch;
    [ObservableProperty] private string _error = string.Empty;
    public bool HasError => Error.Length != 0;
    public bool CanUndo => !_closed && _session.CanUndo;

    public Misc8EditorViewModel(SAV8SWSH sav, IDialogService? dialogs = null)
    {
        _session = new(sav); _sav = _session.Staged; _baseline = (SAV8SWSH)_sav.Clone(); _dialogs = dialogs;
        IsIoA = sav.SaveRevision >= 1; // Isle of Armor+
        LoadTrainerFields();
        LoadMisc();
        LoadBattleTower();
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, static (recipient, _) => ((Misc8EditorViewModel)recipient).RefreshLanguage());
    }

    public bool IsIoA { get; }

    #region Misc

    [ObservableProperty] private uint _watts;
    [ObservableProperty] private int _bp;

    private void LoadMisc()
    {
        Watts = _sav.MyStatus.Watt;
        Bp = _sav.Misc.BP;
    }

    private void SaveMisc()
    {
        if (Watts != _baseline.MyStatus.Watt)
        {
            _sav.MyStatus.Watt = Watts;
            if (_sav.GetRecord(Record8.WattTotal) < Watts) _sav.SetRecord(Record8.WattTotal, (int)Watts);
        }
        if (Bp != _baseline.Misc.BP) _sav.Misc.BP = Bp;
    }

    [RelayCommand]
    private void MaxWatts()
    {
        Watts = MyStatus8.MaxWatt;
    }

    [RelayCommand]
    private void MaxBP()
    {
        Bp = 9999;
    }

    #endregion

    #region Battle Tower

    [ObservableProperty] private uint _singlesWins;
    [ObservableProperty] private uint _doublesWins;
    [ObservableProperty] private ushort _singlesStreak;
    [ObservableProperty] private ushort _doublesStreak;

    private void LoadBattleTower()
    {
        SinglesWins = _sav.GetValue<uint>(SaveBlockAccessor8SWSH.KBattleTowerSinglesVictory);
        DoublesWins = _sav.GetValue<uint>(SaveBlockAccessor8SWSH.KBattleTowerDoublesVictory);
        SinglesStreak = _sav.GetValue<ushort>(SaveBlockAccessor8SWSH.KBattleTowerSinglesStreak);
        DoublesStreak = _sav.GetValue<ushort>(SaveBlockAccessor8SWSH.KBattleTowerDoublesStreak);
    }

    private void SaveBattleTower()
    {
        var singles = Math.Min(9_999_999u, SinglesWins);
        var doubles = Math.Min(9_999_999u, DoublesWins);
        if (SinglesWins != _baseline.GetValue<uint>(SaveBlockAccessor8SWSH.KBattleTowerSinglesVictory))
        { _sav.SetValue(SaveBlockAccessor8SWSH.KBattleTowerSinglesVictory, singles); _sav.SetRecord(RecordLists.G8BattleTowerSingleWin, (int)singles); }
        if (DoublesWins != _baseline.GetValue<uint>(SaveBlockAccessor8SWSH.KBattleTowerDoublesVictory))
        { _sav.SetValue(SaveBlockAccessor8SWSH.KBattleTowerDoublesVictory, doubles); _sav.SetRecord(RecordLists.G8BattleTowerDoubleWin, (int)doubles); }
        if (SinglesStreak != _baseline.GetValue<ushort>(SaveBlockAccessor8SWSH.KBattleTowerSinglesStreak)) _sav.SetValue(SaveBlockAccessor8SWSH.KBattleTowerSinglesStreak, (ushort)Math.Min(300, (int)SinglesStreak));
        if (DoublesStreak != _baseline.GetValue<ushort>(SaveBlockAccessor8SWSH.KBattleTowerDoublesStreak)) _sav.SetValue(SaveBlockAccessor8SWSH.KBattleTowerDoublesStreak, (ushort)Math.Min(300, (int)DoublesStreak));


    }

    #endregion

    #region Fashion

    [RelayCommand]
    private Task UnlockAllFashion()
    {
        if (Gender is not (0 or 1)) return Task.CompletedTask;
        int gender = Gender;
        return RunActionAsync("Fashion", save =>
        {
            byte original = save.Gender;
            try { save.Gender = (byte)gender; save.Fashion.UnlockAllLegal(); }
            finally { save.Gender = original; }
        }, () => Gender == gender);
    }

    #endregion

    #region Diglett (IoA)

    [RelayCommand]
    private Task CollectAllDiglett() => IsIoA ? RunActionAsync("Diglett", save => save.UnlockAllDiglett()) : Task.CompletedTask;

    #endregion

    #region Commands

    [RelayCommand]
    private void Save()
    {
        if (_closed || !CanSave) { Error = LocalizedStrings.Instance["Trainer7_InvalidValues"]; return; }
        _epoch++;
        if (!_session.TryCommit(staged => { _sav = staged; SaveTrainerFields(); SaveMisc(); SaveBattleTower(); }))
        { _sav = _session.Staged; Error = LocalizedStrings.Instance["LgpeTrainer_Conflict"]; return; }
        bool locksChanged = !HasSameTeamLocks(_baseline, _session.Staged);
        _sav = _session.Staged; _baseline = (SAV8SWSH)_sav.Clone(); LoadTrainerFields(); LoadMisc(); LoadBattleTower(); Error = string.Empty;
        OnPropertyChanged(nameof(CanUndo));
        NotifyBattleTeams();
        if (locksChanged) BattleLocksCommitted?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Battle Teams

    /// <summary>Raised after Apply wrote a Battle Team lock change to the live save.</summary>
    public event EventHandler? BattleLocksCommitted;

    /// <summary>True when any of the six staged Battle Teams is locked for a competition.</summary>
    public bool IsBattleTeamLocked => _session.Staged.TeamIndexes.IsAnyTeamLocked();
    private bool CanUnlockBattleTeams() => !_closed && IsBattleTeamLocked;

    /// <summary>Stages <see cref="TeamIndexes8.UnlockAllTeams"/>; Apply writes it, Undo/Reset discard it.</summary>
    [RelayCommand(CanExecute = nameof(CanUnlockBattleTeams))]
    private void UnlockBattleTeams()
    {
        if (_closed) return;
        _session.ApplyAction(static save => save.TeamIndexes.UnlockAllTeams());
        _sav = _session.Staged; _epoch++; Error = string.Empty;
        OnPropertyChanged(nameof(CanUndo));
        NotifyBattleTeams();
    }

    private static bool HasSameTeamLocks(SAV8SWSH a, SAV8SWSH b)
    {
        for (int team = 0; team < 6; team++)
            if (a.TeamIndexes.GetIsTeamLocked(team) != b.TeamIndexes.GetIsTeamLocked(team)) return false;
        return true;
    }

    private void NotifyBattleTeams()
    {
        OnPropertyChanged(nameof(IsBattleTeamLocked));
        UnlockBattleTeamsCommand.NotifyCanExecuteChanged();
    }

    #endregion
}
