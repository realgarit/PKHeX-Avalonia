using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// Gen 6 trainer fields that WinForms' trainer editor shows and the generic form did not: the five
/// PSS messages, the Game Sync ID, the Mega Evolution unlocks, and the Battle Maison streaks.
/// Values are written only when changed, so a no-op Apply leaves the stored bytes untouched.
/// </summary>
public partial class TrainerEditorViewModel
{
    private const int GameSyncIdLength = MyStatus6.GameSyncIDSize;

    /// <summary>Any Gen 6 save, including the OR/AS demo: the status block holds the PSS messages and Mega flags.</summary>
    [ObservableProperty] private bool _hasGen6Status;

    /// <summary>Mega Rayquaza only exists in OR/AS.</summary>
    [ObservableProperty] private bool _hasMegaRayquaza;

    /// <summary>X/Y and OR/AS retail (not the demo) have a Battle Maison.</summary>
    [ObservableProperty] private bool _hasMaison;

    [ObservableProperty] private string _pssMessage1 = string.Empty;
    [ObservableProperty] private string _pssMessage2 = string.Empty;
    [ObservableProperty] private string _pssMessage3 = string.Empty;
    [ObservableProperty] private string _pssMessage4 = string.Empty;
    [ObservableProperty] private string _pssMessage5 = string.Empty;

    [ObservableProperty] private bool _isMegaEvolutionUnlocked;
    [ObservableProperty] private bool _isMegaRayquazaUnlocked;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGameSyncIdValid))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _gameSyncId = string.Empty;

    /// <summary>
    /// Each PSS message is 0x22 bytes of UTF-16: 16 characters plus a terminator.
    /// (<c>SAV6.LongStringLength</c> is not public, so the size is restated here.)
    /// </summary>
    public int PssMessageMaxLength => 16;
    public int GameSyncIdMaxLength => GameSyncIdLength;

    /// <summary>
    /// <see cref="MyStatus6.GameSyncID"/> throws unless it gets exactly 16 hex digits, so Apply is
    /// blocked until the field is valid.
    /// </summary>
    public bool IsGameSyncIdValid => !HasGen6Status || IsValidGameSyncId(GameSyncId);

    public ObservableCollection<MaisonStyleRowViewModel> MaisonRows { get; } = [];

    private static bool IsValidGameSyncId(string? value)
        => value is { Length: GameSyncIdLength } && value.All(char.IsAsciiHexDigit);

    private void LoadGen6Status()
    {
        MaisonRows.Clear();
        if (_sav is not SAV6 sav6)
        {
            HasGen6Status = HasMegaRayquaza = HasMaison = false;
            OnPropertyChanged(nameof(IsGameSyncIdValid));
            return;
        }

        var status = sav6.Status;
        HasGen6Status = true;
        HasMegaRayquaza = sav6 is SAV6AO;
        PssMessage1 = status.Saying1;
        PssMessage2 = status.Saying2;
        PssMessage3 = status.Saying3;
        PssMessage4 = status.Saying4;
        PssMessage5 = status.Saying5;
        IsMegaEvolutionUnlocked = status.IsMegaEvolutionUnlocked;
        IsMegaRayquazaUnlocked = status.IsMegaRayquazaUnlocked;
        GameSyncId = status.GameSyncID;
        OnPropertyChanged(nameof(IsGameSyncIdValid));

        HasMaison = sav6 is ISaveBlock6Main;
        if (sav6 is ISaveBlock6Main main)
        {
            foreach (var style in Enum.GetValues<BattleStyle6>())
                MaisonRows.Add(new MaisonStyleRowViewModel(style, main.Maison));
        }
    }

    private void SaveGen6Status()
    {
        if (_sav is not SAV6 sav6 || !HasGen6Status)
            return;

        var status = sav6.Status;
        if (PssMessage1 != status.Saying1) status.Saying1 = PssMessage1;
        if (PssMessage2 != status.Saying2) status.Saying2 = PssMessage2;
        if (PssMessage3 != status.Saying3) status.Saying3 = PssMessage3;
        if (PssMessage4 != status.Saying4) status.Saying4 = PssMessage4;
        if (PssMessage5 != status.Saying5) status.Saying5 = PssMessage5;

        if (IsMegaEvolutionUnlocked != status.IsMegaEvolutionUnlocked)
            status.IsMegaEvolutionUnlocked = IsMegaEvolutionUnlocked;
        if (HasMegaRayquaza && IsMegaRayquazaUnlocked != status.IsMegaRayquazaUnlocked)
            status.IsMegaRayquazaUnlocked = IsMegaRayquazaUnlocked;

        if (IsValidGameSyncId(GameSyncId) && !string.Equals(GameSyncId, status.GameSyncID, StringComparison.OrdinalIgnoreCase))
            status.GameSyncID = GameSyncId.ToUpperInvariant();

        if (sav6 is ISaveBlock6Main main)
        {
            foreach (var row in MaisonRows)
                row.Save(main.Maison);
        }
    }
}

/// <summary>
/// One Battle Maison style (Single, Double, Triple, Rotation, Multi): the current and best streaks for
/// the normal and Super battles. In Core's flat index, bit 1 selects the best streak and bit 0 Super.
/// </summary>
public partial class MaisonStyleRowViewModel : ViewModelBase
{
    private readonly ushort _loadedNormalCurrent, _loadedNormalBest, _loadedSuperCurrent, _loadedSuperBest;

    public MaisonStyleRowViewModel(BattleStyle6 style, MaisonBlock maison)
    {
        Style = style;
        _loadedNormalCurrent = maison.GetMaisonStat(style, streak: false, super: false);
        _loadedNormalBest = maison.GetMaisonStat(style, streak: true, super: false);
        _loadedSuperCurrent = maison.GetMaisonStat(style, streak: false, super: true);
        _loadedSuperBest = maison.GetMaisonStat(style, streak: true, super: true);
        NormalCurrent = _loadedNormalCurrent;
        NormalBest = _loadedNormalBest;
        SuperCurrent = _loadedSuperCurrent;
        SuperBest = _loadedSuperBest;
    }

    public BattleStyle6 Style { get; }
    public string Name => LocalizedStrings.Instance[$"TrainerEditor_MaisonStyle{Style}"];

    [ObservableProperty] private int _normalCurrent;
    [ObservableProperty] private int _normalBest;
    [ObservableProperty] private int _superCurrent;
    [ObservableProperty] private int _superBest;

    internal void Save(MaisonBlock maison)
    {
        Write(maison, streak: false, super: false, NormalCurrent, _loadedNormalCurrent);
        Write(maison, streak: true, super: false, NormalBest, _loadedNormalBest);
        Write(maison, streak: false, super: true, SuperCurrent, _loadedSuperCurrent);
        Write(maison, streak: true, super: true, SuperBest, _loadedSuperBest);
    }

    private void Write(MaisonBlock maison, bool streak, bool super, int value, ushort loaded)
    {
        var clamped = (ushort)Math.Clamp(value, 0, ushort.MaxValue);
        if (clamped != loaded)
            maison.SetMaisonStat(Style, streak, super, clamped);
    }
}
