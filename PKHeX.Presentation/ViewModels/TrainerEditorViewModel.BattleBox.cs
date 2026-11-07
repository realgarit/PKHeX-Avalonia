using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// Gen 6 Battle Box unlock and the shared "box/party slots changed" notification for the
/// Gen 7 and Sword/Shield child workspaces. The unlock is staged on a cloned save and
/// reaches the live save only on Apply.
/// </summary>
public partial class TrainerEditorViewModel
{
    private TrainerBlockDataSession<SAV6>? _battleBoxSession;

    /// <summary>Raised after Apply wrote a Battle Box / Battle Team lock change to the live save.</summary>
    public event EventHandler? SaveSlotsChanged;

    [ObservableProperty] private string _battleBoxError = string.Empty;
    public bool HasBattleBoxError => BattleBoxError.Length != 0;
    partial void OnBattleBoxErrorChanged(string value) => OnPropertyChanged(nameof(HasBattleBoxError));

    /// <summary>X/Y and OR/AS (not the OR/AS demo) have a lockable Battle Box.</summary>
    public bool HasBattleBox => _battleBoxSession is not null;
    public bool IsBattleBoxLocked => GetBattleBoxLocked(_battleBoxSession?.Staged);
    public bool CanUndoBattleBox => _battleBoxSession?.CanUndo == true;

    private static bool GetBattleBoxLocked(SAV6? sav) => sav is ISaveBlock6Main main && main.BattleBox.Locked;

    private void InitializeBattleBox(SaveFile sav)
    {
        if (sav is SAV6 sav6 and ISaveBlock6Main)
            _battleBoxSession = new TrainerBlockDataSession<SAV6>(sav6);
        if (Gen7Editor is not null) Gen7Editor.BattleLocksCommitted += OnChildBattleLocksCommitted;
        if (SwshEditor is not null) SwshEditor.BattleLocksCommitted += OnChildBattleLocksCommitted;
    }

    private void DisposeBattleBox()
    {
        if (Gen7Editor is not null) Gen7Editor.BattleLocksCommitted -= OnChildBattleLocksCommitted;
        if (SwshEditor is not null) SwshEditor.BattleLocksCommitted -= OnChildBattleLocksCommitted;
        _battleBoxSession = null;
        NotifyBattleBox();
    }

    private void OnChildBattleLocksCommitted(object? sender, EventArgs e) => SaveSlotsChanged?.Invoke(this, EventArgs.Empty);

    private bool CanUnlockBattleBox() => IsBattleBoxLocked;

    [RelayCommand(CanExecute = nameof(CanUnlockBattleBox))]
    private void UnlockBattleBox()
    {
        if (_battleBoxSession is null) return;
        _battleBoxSession.ApplyAction(static save => ((ISaveBlock6Main)save).BattleBox.Locked = false);
        BattleBoxError = string.Empty;
        NotifyBattleBox();
    }

    [RelayCommand]
    private void UndoBattleBox()
    {
        if (_battleBoxSession is null) return;
        _battleBoxSession.Undo();
        NotifyBattleBox();
    }

    /// <summary>Commits the staged Battle Box; returns false (and writes nothing) on a live conflict.</summary>
    private bool TryCommitBattleBox(out bool lockChanged)
    {
        lockChanged = false;
        if (_battleBoxSession is null) return true;
        var staged = ((ISaveBlock6Main)_battleBoxSession.Staged).BattleBox;
        var live = ((ISaveBlock6Main)_sav).BattleBox;
        lockChanged = live.LockedWiFiTournament != staged.LockedWiFiTournament
                   || live.LockedLiveTournament != staged.LockedLiveTournament;
        if (!_battleBoxSession.TryCommit())
        {
            lockChanged = false;
            BattleBoxError = Localization.LocalizedStrings.Instance["LgpeTrainer_Conflict"];
            return false;
        }
        BattleBoxError = string.Empty;
        NotifyBattleBox();
        return true;
    }

    private void ResetBattleBox()
    {
        _battleBoxSession?.Reset();
        BattleBoxError = string.Empty;
        NotifyBattleBox();
    }

    private void NotifyBattleBox()
    {
        OnPropertyChanged(nameof(HasBattleBox));
        OnPropertyChanged(nameof(IsBattleBoxLocked));
        OnPropertyChanged(nameof(CanUndoBattleBox));
        UnlockBattleBoxCommand.NotifyCanExecuteChanged();
    }

    private void RaiseSaveSlotsChanged() => SaveSlotsChanged?.Invoke(this, EventArgs.Empty);
}
