using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;


namespace PKHeX.Presentation.ViewModels;

public partial class TrainerEditorViewModel : ViewModelBase, IDisposable
{
    private SaveFile _sav;
    public Misc7bEditorViewModel? LgpeEditor { get; }
    private SAV7b? _lgpeParentBaseline;
    public Misc7EditorViewModel? Gen7Editor { get; }
    private SAV7? _gen7ParentBaseline;
    public Misc8EditorViewModel? SwshEditor { get; }
    private SAV8SWSH? _swshParentBaseline;
    public bool IsSWSH => SwshEditor is not null;
    public Misc8aEditorViewModel? PlaEditor { get; }
    private SAV8LA? _plaParentBaseline;
    public bool IsPLA => PlaEditor is not null;
    public BdspTrainerEditorViewModel? BdspEditor { get; }
    private SAV8BS? _bdspParentBaseline;
    public bool IsBDSP => BdspEditor is not null;
    public bool IsGen7 => Gen7Editor is not null;
    public bool IsLGPE => LgpeEditor is not null;
    public bool HasDedicatedTrainerWorkspace => IsZA || IsLGPE || IsGen7 || IsSWSH || IsPLA || IsBDSP;

    public TrainerEditorViewModel(SaveFile sav, IDialogService? dialogs = null, IImageCodec? imageCodec = null)
    {
        _zaSession = sav is SAV9ZA za ? new ZaTrainerDataSession(za) : null;
        _sav = _zaSession?.Staged ?? sav;
        _zaDialogs = dialogs;
        _zaImageCodec = imageCodec;
        LgpeEditor = sav is SAV7b lgpe ? new Misc7bEditorViewModel(lgpe, dialogs) : null;
        _lgpeParentBaseline = sav is SAV7b lgpeBaseline ? (SAV7b)lgpeBaseline.Clone() : null;
        Gen7Editor = sav is SAV7 gen7 ? new Misc7EditorViewModel(gen7, dialogs) : null;
        _gen7ParentBaseline = sav is SAV7 baseline7 ? (SAV7)baseline7.Clone() : null;
        SwshEditor = sav is SAV8SWSH swsh ? new Misc8EditorViewModel(swsh, dialogs) : null;
        _swshParentBaseline = sav is SAV8SWSH swshBaseline ? (SAV8SWSH)swshBaseline.Clone() : null;
        PlaEditor = sav is SAV8LA pla ? new Misc8aEditorViewModel(pla) : null;
        _plaParentBaseline = sav is SAV8LA plaBaseline ? (SAV8LA)plaBaseline.Clone() : null;
        BdspEditor = sav is SAV8BS bdsp ? new BdspTrainerEditorViewModel(bdsp) : null;
        _bdspParentBaseline = sav is SAV8BS bdspBaseline ? (SAV8BS)bdspBaseline.Clone() : null;
        InitializeBattleBox(sav);
        LoadFromSave();
    }

    // Basic Info
    [ObservableProperty]
    private string _trainerName = string.Empty;

    [ObservableProperty]
    private int _gender;

    [ObservableProperty]
    private uint _money;

    [ObservableProperty]
    private ushort _tid16;

    [ObservableProperty]
    private ushort _sid16;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private uint _displayTid;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private uint _displaySid;

    [ObservableProperty]
    private int _language;

    // Play Time
    [ObservableProperty]
    private int _playedHours;

    [ObservableProperty]
    private int _playedMinutes;

    [ObservableProperty]
    private int _playedSeconds;

    // Game Info (read-only)
    public string GameVersion => _sav.Version.ToString();
    public int Generation => _sav.Generation;
    public string SaveType => _sav.GetType().Name;

    // Data sources
    [ObservableProperty] private IReadOnlyList<ComboItem> _languageList = [];
    public IReadOnlyList<ComboItem> GenderList => [
        new ComboItem(Localization.LocalizedStrings.Instance["Pokedex5Editor_Male"], 0),
        new ComboItem(Localization.LocalizedStrings.Instance["Pokedex5Editor_Female"], 1)
    ];

    // Max values for validation
    public int MaxMoney => _sav.MaxMoney;

    // Badges
    [ObservableProperty]
    private bool _hasBadges;

    [ObservableProperty]
    private bool _isBadgeCount;

    [ObservableProperty]
    private int _badgeCount;

    public ObservableCollection<BadgeItemViewModel> Badges { get; } = [];

    public int MaxBadgeCount => 8;

    public bool UsesSixDigitTrainerIds => _sav.TrainerIDDisplayFormat == TrainerIDFormat.SixDigit;
    public uint MaxDisplayTid => UsesSixDigitTrainerIds ? 999999u : ushort.MaxValue;
    public uint MaxDisplaySid => UsesSixDigitTrainerIds
        ? (uint.MaxValue - Math.Min(DisplayTid, MaxDisplayTid)) / 1_000_000u
        : ushort.MaxValue;
    public string DisplayTidFormat => UsesSixDigitTrainerIds ? "000000" : "00000";
    public string DisplaySidFormat => UsesSixDigitTrainerIds ? "0000" : "00000";

    public bool IsDisplayIdValid => !UsesSixDigitTrainerIds || _sav.IsValidTrainerID7(DisplaySid, DisplayTid);

    private bool CanSave() => IsDisplayIdValid && IsRegionInputValid && (!IsZA || (!_zaClosed && IsZaInputValid));

    partial void OnDisplayTidChanged(uint value)
    {
        OnPropertyChanged(nameof(MaxDisplaySid));
        OnPropertyChanged(nameof(IsDisplayIdValid));
    }

    partial void OnDisplaySidChanged(uint value) => OnPropertyChanged(nameof(IsDisplayIdValid));

    private PropertyInfo? _badgesProperty;
    
    // Adventure Info
    [ObservableProperty]
    private bool _hasAdventureInfo;

    [ObservableProperty]
    private long _secondsToFame;

    [ObservableProperty]
    private long _secondsToStart;

    private PropertyInfo? _secondsToFameProperty;
    private PropertyInfo? _secondsToStartProperty;

    // Map Coordinates
    [ObservableProperty]
    private bool _hasCoordinates;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _z;

    private PropertyInfo? _xProperty;
    private PropertyInfo? _yProperty;
    private PropertyInfo? _zProperty;

    // Currencies
    [ObservableProperty] private bool _hasCoins;
    [ObservableProperty] private uint _coins;

    [ObservableProperty] private bool _hasBP;
    [ObservableProperty] private uint _bP;

    [ObservableProperty] private bool _hasWatts;
    [ObservableProperty] private uint _watts;

    [ObservableProperty] private bool _hasLP;
    [ObservableProperty] private uint _lP;

    [ObservableProperty] private bool _hasBlueberryPoints;
    [ObservableProperty] private uint _blueberryPoints;

    [ObservableProperty] private bool _hasFestaCoins;
    [ObservableProperty] private uint _festaCoins;

    [ObservableProperty] private bool _hasMeritPoints;
    [ObservableProperty] private uint _meritPoints;

    [ObservableProperty] private bool _hasPokeMiles;
    [ObservableProperty] private uint _pokeMiles;

    [ObservableProperty] private bool _hasGimmighoulCoins;
    [ObservableProperty] private uint _gimmighoulCoins;

    [ObservableProperty] private bool _hasRoyalePoints;
    [ObservableProperty] private uint _royalePoints;

    [ObservableProperty] private bool _hasRoyalePointsInfinite;
    [ObservableProperty] private uint _royalePointsInfinite;

    [ObservableProperty] private bool _hasHyperspacePoints;
    [ObservableProperty] private uint _hyperspacePoints;

    [ObservableProperty] private bool _hasStreetName;
    [ObservableProperty] private string _streetName = string.Empty;

    [ObservableProperty] private bool _hasMeritEarned;
    [ObservableProperty] private uint _meritEarned;

    [ObservableProperty] private bool _hasExpeditionRank;
    [ObservableProperty] private uint _expeditionRank;

    [ObservableProperty] private bool _hasSatchelUpgrades;
    [ObservableProperty] private uint _satchelUpgrades;

    [ObservableProperty] private bool _anyCurrencyVisible;

    // Battle Chateau (Gen 6 XY)
    [ObservableProperty] private bool _hasBattleChateau;
    [ObservableProperty] private int _chateauRank;
    [ObservableProperty] private int _chateauPoints;

    public IReadOnlyList<ComboItem> ChateauRankList { get; } = BuildChateauRankList();

    private static IReadOnlyList<ComboItem> BuildChateauRankList()
    {
        var names = Enum.GetNames<BattleChateauRank6>();
        var values = Enum.GetValues<BattleChateauRank6>();
        var list = new List<ComboItem>(names.Length);
        for (int i = 0; i < names.Length; i++)
            list.Add(new ComboItem(names[i], Convert.ToInt32(values[i])));
        return list;
    }

    private void LoadFromSave()
    {
        TrainerName = _sav.OT;
        Gender = _sav.Gender;
        // SCBlock-based saves (LA, SV, ZA) throw when the block type is None on a blank save.
        // Wrap each property that routes through GetBlockValue so the editor still loads.
        try { Money = _sav.Money; } catch { }
        Tid16 = _sav.TID16;
        Sid16 = _sav.SID16;
        DisplayTid = _sav.DisplayTID;
        DisplaySid = _sav.DisplaySID;
        Language = _sav.Language;

        try { PlayedHours = _sav.PlayedHours; } catch { }
        try { PlayedMinutes = _sav.PlayedMinutes; } catch { }
        try { PlayedSeconds = _sav.PlayedSeconds; } catch { }

        // Initialize language list based on generation
        LanguageList = GameInfo.Sources.LanguageDataSource(_sav.Generation, _sav.Context);
        OnPropertyChanged(nameof(GenderList));

        LoadBadges();
        LoadAdventureInfo();
        LoadCoordinates();
        LoadCurrencies();
        LoadBattleChateau();
        LoadRegionSettings();
        LoadZaFields();
    }

    private void LoadBattleChateau()
    {
        if (_sav is SAV6XY xy)
        {
            HasBattleChateau = true;
            var sube = xy.SUBE;
            ChateauRank = sube.ChateauRank;
            ChateauPoints = sube.ChateauPoints;
        }
        else
        {
            HasBattleChateau = false;
        }
    }

    private void LoadCurrencies()
    {
        var type = _sav.GetType();

        // SV owns this value as Blueberry Points; do not expose it through the generic BP field.
        if (_sav is SAV9SV sv)
        {
            // SCBlocks in blank/uninitialized SV saves can have Type=None and throw.
            // Wrap each block read individually so a missing block doesn't hide the rest.
            // Set visibility flags AFTER the read succeeds to avoid showing (and saving) default 0.
            try { BlueberryPoints = (uint)sv.Blocks.GetBlockValue(SaveBlockAccessor9SV.KBlueberryPoints); HasBlueberryPoints = true; } catch { }
            try { LP = (uint)sv.Blocks.GetBlockValue(SaveBlockAccessor9SV.KLeaguePoints); HasLP = true; } catch { }
            try { GimmighoulCoins = sv.Items.GetItemQuantity(1985); HasGimmighoulCoins = true; } catch { }
        }
        else
        {
            // BP (Multiple Gen-specific locations)
            var bpProp = type.GetProperty("BP");
            if (bpProp != null)
            {
                HasBP = true;
                BP = Convert.ToUInt32(bpProp.GetValue(_sav));
            }
            else if (_sav is SAV8BS bs)
            {
                HasBP = true;
                BP = bs.BattleTower.BP;
            }
            else if (_sav is SAV5 sav5)
            {
                HasBP = true;
                BP = (uint)sav5.BattleSubway.BP;
            }
        }

        // Coins (Gen 1-4)
        var coinProp = type.GetProperty("Coin");
        if (coinProp != null)
        {
            HasCoins = true;
            Coins = Convert.ToUInt32(coinProp.GetValue(_sav));
        }

        // Watts (Gen 8 SWSH)
        if (_sav is SAV8SWSH swsh)
        {
            HasWatts = true;
            Watts = swsh.MyStatus.Watt;
        }

        // Festa Coins (Gen 7)
        if (_sav is SAV7 sav7)
        {
            HasFestaCoins = true;
            FestaCoins = (uint)sav7.Festa.FestaCoins;
        }

        // Merit Points (Gen 8 LA)
        // SCBlocks in blank/uninitialized LA saves can have Type=None and throw.
        if (_sav is SAV8LA la)
        {
            try { MeritPoints = (uint)la.Accessor.GetBlockValue(SaveBlockAccessor8LA.KMeritCurrent); HasMeritPoints = true; } catch { }
            try { MeritEarned = (uint)la.Accessor.GetBlockValue(SaveBlockAccessor8LA.KMeritEarnedTotal); HasMeritEarned = true; } catch { }
            try { ExpeditionRank = (uint)la.Accessor.GetBlockValue(SaveBlockAccessor8LA.KExpeditionTeamRank); HasExpeditionRank = true; } catch { }
            try { SatchelUpgrades = (uint)la.Accessor.GetBlockValue(SaveBlockAccessor8LA.KSatchelUpgrades); HasSatchelUpgrades = true; } catch { }
        }

        // Poké Miles (Gen 6)
        if (_sav is ITrainerStatRecord statSav && _sav.Generation == 6)
        {
            HasPokeMiles = true;
            PokeMiles = (uint)statSav.GetRecord(63);
        }

        // PLZA (SAV9ZA)
        // SCBlocks in blank/uninitialized ZA saves can have Type=None and throw.
        if (_sav is SAV9ZA za)
        {
            try { RoyalePoints = za.TicketPointsRoyale; HasRoyalePoints = true; } catch { }
            try { RoyalePointsInfinite = za.TicketPointsRoyaleInfinite; HasRoyalePointsInfinite = true; } catch { }

            try
            {
                var b = za.Blocks.GetBlock(SaveBlockAccessor9ZA.KStreetName);
                StreetName = za.GetString(b.Data);
                HasStreetName = true;
            }
            catch { }
        }

        AnyCurrencyVisible = HasBP || HasCoins || HasWatts || HasLP || HasBlueberryPoints || HasFestaCoins || HasMeritPoints || HasPokeMiles || HasGimmighoulCoins || HasRoyalePoints || HasRoyalePointsInfinite || HasHyperspacePoints;
    }

    private void LoadCoordinates()
    {
        var type = _sav.GetType();
        _xProperty = type.GetProperty("X");
        _yProperty = type.GetProperty("Y");
        _zProperty = type.GetProperty("Z");

        HasCoordinates = _xProperty != null && _yProperty != null && _zProperty != null;

        if (HasCoordinates)
        {
            X = Convert.ToDouble(_xProperty!.GetValue(_sav));
            Y = Convert.ToDouble(_yProperty!.GetValue(_sav));
            Z = Convert.ToDouble(_zProperty!.GetValue(_sav));
        }
    }

    private void LoadAdventureInfo()
    {
        var type = _sav.GetType();
        _secondsToFameProperty = type.GetProperty("SecondsToFame");
        _secondsToStartProperty = type.GetProperty("SecondsToStart");

        HasAdventureInfo = _secondsToFameProperty != null || _secondsToStartProperty != null;

        if (_secondsToFameProperty != null)
        {
            var val = _secondsToFameProperty.GetValue(_sav);
            SecondsToFame = Convert.ToInt64(val);
        }

        if (_secondsToStartProperty != null)
        {
            var val = _secondsToStartProperty.GetValue(_sav);
            SecondsToStart = Convert.ToInt64(val);
        }
    }

    private void LoadBadges()
    {
        Badges.Clear();
        _badgesProperty = _sav.GetType().GetProperty("Badges");
        
        if (_badgesProperty != null && _badgesProperty.PropertyType == typeof(int))
        {
            HasBadges = true;

            if (_sav is SAV8SWSH)
            {
                IsBadgeCount = true;
                BadgeCount = Math.Clamp((int)_badgesProperty.GetValue(_sav)!, 0, MaxBadgeCount);
                return;
            }

            IsBadgeCount = false;
            int badgeFlags = (int)_badgesProperty.GetValue(_sav)!;
            
            // Create 16 badge wrappers (standard max, usually 8 used)
            for (int i = 0; i < 16; i++)
            {
                bool isSet = (badgeFlags & (1 << i)) != 0;
                var badge = new BadgeItemViewModel(i + 1, isSet);
                // Listen for changes to update the flag logic later if needed, 
                // but simpler to just rebuild the int on Save.
                Badges.Add(badge);
            }
        }
        else
        {
            HasBadges = false;
            IsBadgeCount = false;
            BadgeCount = 0;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (BdspEditor is not null && _bdspParentBaseline is { } bdspBaseline)
        {
            if (TrainerName != bdspBaseline.OT) BdspEditor.TrainerName = TrainerName;
            if (Gender != bdspBaseline.Gender) BdspEditor.Gender = Gender;
            if (Language != bdspBaseline.Language) BdspEditor.Language = Language;
            if (Money != bdspBaseline.Money) BdspEditor.Money = Money;
            if (DisplayTid != bdspBaseline.DisplayTID) BdspEditor.DisplayTid = DisplayTid;
            if (DisplaySid != bdspBaseline.DisplaySID) BdspEditor.DisplaySid = DisplaySid;
            if (PlayedHours != bdspBaseline.PlayedHours) BdspEditor.PlayedHours = PlayedHours;
            if (PlayedMinutes != bdspBaseline.PlayedMinutes) BdspEditor.PlayedMinutes = PlayedMinutes;
            if (PlayedSeconds != bdspBaseline.PlayedSeconds) BdspEditor.PlayedSeconds = PlayedSeconds;
            if (HasBP && BP != bdspBaseline.BattleTower.BP) BdspEditor.Bp = BP;
            BdspEditor.SaveCommand.Execute(null);
            if (!BdspEditor.HasError) { _bdspParentBaseline = (SAV8BS)_sav.Clone(); LoadFromSave(); }
            return;
        }
        if (PlaEditor is not null && _plaParentBaseline is { } plaBaseline)
        {
            if (TrainerName != plaBaseline.OT) PlaEditor.TrainerName = TrainerName;
            if (Gender != plaBaseline.Gender) PlaEditor.Gender = Gender;
            if (plaBaseline.Blocks.GetBlock(SaveBlockAccessor8LA.KMoney).Type == SCTypeCode.UInt32 && Money != plaBaseline.Money) PlaEditor.Money = Money;
            if (Language != plaBaseline.Language) PlaEditor.Language = Language;
            if (PlayedHours != plaBaseline.PlayedHours) PlaEditor.PlayedHours = PlayedHours;
            if (PlayedMinutes != plaBaseline.PlayedMinutes) PlaEditor.PlayedMinutes = PlayedMinutes;
            if (PlayedSeconds != plaBaseline.PlayedSeconds) PlaEditor.PlayedSeconds = PlayedSeconds;
            if (DisplayTid != plaBaseline.DisplayTID) PlaEditor.DisplayTid = DisplayTid;
            if (DisplaySid != plaBaseline.DisplaySID) PlaEditor.DisplaySid = DisplaySid;
            if (HasMeritPoints && plaBaseline.Blocks.GetBlock(SaveBlockAccessor8LA.KMeritCurrent).Type == SCTypeCode.UInt32 && MeritPoints != (uint)plaBaseline.Blocks.GetBlockValue(SaveBlockAccessor8LA.KMeritCurrent)) PlaEditor.MeritCurrent = MeritPoints;
            if (HasMeritEarned && plaBaseline.Blocks.GetBlock(SaveBlockAccessor8LA.KMeritEarnedTotal).Type == SCTypeCode.UInt32 && MeritEarned != (uint)plaBaseline.Blocks.GetBlockValue(SaveBlockAccessor8LA.KMeritEarnedTotal)) PlaEditor.MeritEarned = MeritEarned;
            PlaEditor.SaveCommand.Execute(null);
            if (!PlaEditor.HasError) { _plaParentBaseline = (SAV8LA)_sav.Clone(); LoadFromSave(); }
            return;
        }
        if (SwshEditor is not null && _swshParentBaseline is { } swshBaseline)
        {
            if (TrainerName != swshBaseline.OT) SwshEditor.TrainerName = TrainerName;
            if (Gender != swshBaseline.Gender) SwshEditor.Gender = Gender;
            if (Money != swshBaseline.Money) SwshEditor.Money = Money;
            if (Language != swshBaseline.Language) SwshEditor.Language = Language;
            if (PlayedHours != swshBaseline.PlayedHours) SwshEditor.PlayedHours = PlayedHours;
            if (PlayedMinutes != swshBaseline.PlayedMinutes) SwshEditor.PlayedMinutes = PlayedMinutes;
            if (PlayedSeconds != swshBaseline.PlayedSeconds) SwshEditor.PlayedSeconds = PlayedSeconds;
            if (DisplayTid != swshBaseline.DisplayTID) SwshEditor.DisplayTid = DisplayTid;
            if (DisplaySid != swshBaseline.DisplaySID) SwshEditor.DisplaySid = DisplaySid;
            if (HasWatts && Watts != swshBaseline.MyStatus.Watt) SwshEditor.Watts = Watts;
            // The field shows the stored count clamped to 0-8; compare against that, so an
            // untouched out-of-range value (e.g. 255) is not rewritten on a no-op Apply.
            if (IsBadgeCount && BadgeCount != Math.Clamp(swshBaseline.Badges, 0, MaxBadgeCount)) SwshEditor.BadgeCount = BadgeCount;
            SwshEditor.SaveCommand.Execute(null);
            if (!SwshEditor.HasError) { _swshParentBaseline = (SAV8SWSH)_sav.Clone(); LoadFromSave(); }
            return;
        }
        if (Gen7Editor is not null && _gen7ParentBaseline is { } gen7Baseline)
        {
            if (TrainerName != gen7Baseline.OT) Gen7Editor.TrainerName = TrainerName;
            if (Gender != gen7Baseline.Gender) Gen7Editor.Gender = Gender;
            if (Money != gen7Baseline.Money) Gen7Editor.Money = Money;
            if (Language != gen7Baseline.Language) Gen7Editor.Language = Language;
            if (PlayedHours != gen7Baseline.PlayedHours) Gen7Editor.PlayedHours = PlayedHours;
            if (PlayedMinutes != gen7Baseline.PlayedMinutes) Gen7Editor.PlayedMinutes = PlayedMinutes;
            if (PlayedSeconds != gen7Baseline.PlayedSeconds) Gen7Editor.PlayedSeconds = PlayedSeconds;
            if (DisplayTid != gen7Baseline.DisplayTID || DisplaySid != gen7Baseline.DisplaySID)
            {
                uint id = checked(DisplaySid * 1_000_000 + DisplayTid);
                Gen7Editor.Tid16 = (ushort)id; Gen7Editor.Sid16 = (ushort)(id >> 16);
            }
            Gen7Editor.SaveCommand.Execute(null);
            if (!Gen7Editor.HasError) { _gen7ParentBaseline = (SAV7)_sav.Clone(); LoadFromSave(); }
            return;
        }
        if (LgpeEditor is not null && _lgpeParentBaseline is { } lgpeBaseline)
        {
            if (TrainerName != lgpeBaseline.OT) LgpeEditor.TrainerName = TrainerName;
            if (Gender != lgpeBaseline.Gender) LgpeEditor.Gender = Gender;
            if (Money != lgpeBaseline.Money) LgpeEditor.Money = Money;
            if (DisplayTid != lgpeBaseline.DisplayTID) LgpeEditor.DisplayTid = DisplayTid;
            if (DisplaySid != lgpeBaseline.DisplaySID) LgpeEditor.DisplaySid = DisplaySid;
            if (Language != lgpeBaseline.Language) LgpeEditor.Language = Language;
            if (PlayedHours != lgpeBaseline.PlayedHours) LgpeEditor.PlayedHours = PlayedHours;
            if (PlayedMinutes != lgpeBaseline.PlayedMinutes) LgpeEditor.PlayedMinutes = PlayedMinutes;
            if (PlayedSeconds != lgpeBaseline.PlayedSeconds) LgpeEditor.PlayedSeconds = PlayedSeconds;
            if (!LgpeEditor.CanSave) return;
            LgpeEditor.SaveCommand.Execute(null);
            if (!LgpeEditor.HasError) { _lgpeParentBaseline = (SAV7b)_sav.Clone(); LoadFromSave(); }
            return;
        }
        if (IsZA) { SaveZaFields(); return; }
        // Commit the staged Battle Box first: a live conflict must leave every field unwritten.
        if (!TryCommitBattleBox(out bool battleBoxChanged)) return;
        _sav.OT = TrainerName;
        _sav.Gender = (byte)Gender;
        _sav.Money = Money;
        _sav.SetDisplayID(DisplayTid, DisplaySid);
        Tid16 = _sav.TID16;
        Sid16 = _sav.SID16;
        _sav.Language = Language;

        _sav.PlayedHours = PlayedHours;
        _sav.PlayedMinutes = PlayedMinutes;
        _sav.PlayedSeconds = PlayedSeconds;

        SaveBadges();
        SaveAdventureInfo();
        SaveCoordinates();
        SaveCurrencies();
        SaveBattleChateau();
        SaveRegionSettings();
        if (battleBoxChanged) RaiseSaveSlotsChanged();
    }

    private void SaveBattleChateau()
    {
        if (!HasBattleChateau) return;

        if (_sav is SAV6XY xy)
        {
            xy.SUBE.SetChateau((ushort)ChateauRank, (ushort)ChateauPoints);
        }
    }

    [RelayCommand]
    private void SetChateauPointsForRank()
    {
        // GetChateauPointsForRank only defines values for the named ranks (Baron..GrandDuke).
        // Guard against higher raw rank values (ChateauRankMax is 0xF) which would throw.
        if (ChateauRank <= (int)BattleChateauRank6.GrandDuke)
            ChateauPoints = SubEventLog6XY.GetChateauPointsForRank((ushort)ChateauRank);
    }

    private void SaveCurrencies()
    {
        var type = _sav.GetType();

        // BP
        var bpProp = type.GetProperty("BP");
        if (bpProp != null && HasBP)
        {
            bpProp.SetValue(_sav, Convert.ChangeType(BP, bpProp.PropertyType));
        }
        else if (_sav is SAV8BS bs && HasBP)
        {
            bs.BattleTower.BP = BP;
        }
        else if (_sav is SAV5 sav5 && HasBP)
        {
            sav5.BattleSubway.BP = (ushort)BP;
        }

        if (_sav is SAV9SV sv9)
        {
            if (HasBlueberryPoints) sv9.Blocks.SetBlockValue(SaveBlockAccessor9SV.KBlueberryPoints, BlueberryPoints);
            if (HasLP) sv9.Blocks.SetBlockValue(SaveBlockAccessor9SV.KLeaguePoints, LP);
            if (HasGimmighoulCoins) sv9.Items.SetItemQuantity(1985, (int)GimmighoulCoins);
        }

        // Coins
        var coinProp = type.GetProperty("Coin");
        if (coinProp != null && HasCoins)
        {
            coinProp.SetValue(_sav, Convert.ChangeType(Coins, coinProp.PropertyType));
        }

        // Watts
        if (_sav is SAV8SWSH swsh && HasWatts)
        {
            swsh.MyStatus.Watt = Watts;
        }

        // Festa Coins
        if (_sav is SAV7 sav7 && HasFestaCoins)
        {
            sav7.Festa.FestaCoins = (int)FestaCoins;
        }

        // Merit Points
        if (_sav is SAV8LA la && HasMeritPoints)
        {
            la.Accessor.SetBlockValue(SaveBlockAccessor8LA.KMeritCurrent, MeritPoints);
            la.Accessor.SetBlockValue(SaveBlockAccessor8LA.KMeritEarnedTotal, MeritEarned);
            la.Accessor.SetBlockValue(SaveBlockAccessor8LA.KExpeditionTeamRank, ExpeditionRank);
            la.Accessor.SetBlockValue(SaveBlockAccessor8LA.KSatchelUpgrades, SatchelUpgrades);
        }

        // Poké Miles
        if (_sav is ITrainerStatRecord statSav && _sav.Generation == 6 && HasPokeMiles)
        {
            statSav.SetRecord(63, (int)PokeMiles);
        }

        // PLZA
        if (_sav is SAV9ZA za)
        {
            if (HasRoyalePoints) za.TicketPointsRoyale = RoyalePoints;
            if (HasRoyalePointsInfinite) za.TicketPointsRoyaleInfinite = RoyalePointsInfinite;

            if (HasStreetName)
            {
                var b = za.Blocks.GetBlock(SaveBlockAccessor9ZA.KStreetName);
                b.Data.Clear();
                za.SetString(b.Data, StreetName, b.Data.Length, StringConverterOption.None);
            }
        }
    }

    private void SaveCoordinates()
    {
        if (!HasCoordinates) return;

        // X
        object valX = Convert.ChangeType(X, _xProperty!.PropertyType);
        _xProperty.SetValue(_sav, valX);

        // Y
        object valY = Convert.ChangeType(Y, _yProperty!.PropertyType);
        _yProperty.SetValue(_sav, valY);

        // Z
        object valZ = Convert.ChangeType(Z, _zProperty!.PropertyType);
        _zProperty.SetValue(_sav, valZ);
    }

    private void SaveAdventureInfo()
    {
        if (!HasAdventureInfo) return;

        if (_secondsToFameProperty != null)
        {
             // Handle type conversion back to the property's type (could be uint or ulong or int)
             // Core usually uses uint or long. Safe to Convert.ChangeType
             object val = Convert.ChangeType(SecondsToFame, _secondsToFameProperty.PropertyType);
             _secondsToFameProperty.SetValue(_sav, val);
        }

        if (_secondsToStartProperty != null)
        {
             object val = Convert.ChangeType(SecondsToStart, _secondsToStartProperty.PropertyType);
             _secondsToStartProperty.SetValue(_sav, val);
        }
    }

    private void SaveBadges()
    {
        if (!HasBadges || _badgesProperty == null) return;

        if (IsBadgeCount)
        {
            _badgesProperty.SetValue(_sav, Math.Clamp(BadgeCount, 0, MaxBadgeCount));
            return;
        }

        int badgeFlags = 0;
        for (int i = 0; i < Badges.Count; i++)
        {
            if (Badges[i].IsObtained)
            {
                badgeFlags |= (1 << i);
            }
        }
        _badgesProperty.SetValue(_sav, badgeFlags);
    }

    [RelayCommand]
    private void MaxMoney_Click()
    {
        Money = (uint)MaxMoney;
    }

    [RelayCommand]
    private void Reset()
    {
        if (BdspEditor is not null) { BdspEditor.ResetCommand.Execute(null); _bdspParentBaseline = (SAV8BS)_sav.Clone(); LoadFromSave(); return; }
        if (PlaEditor is not null) { PlaEditor.ResetCommand.Execute(null); _plaParentBaseline = (SAV8LA)_sav.Clone(); LoadFromSave(); return; }
        if (SwshEditor is not null) { SwshEditor.ResetCommand.Execute(null); _swshParentBaseline = (SAV8SWSH)_sav.Clone(); LoadFromSave(); return; }
        if (Gen7Editor is not null) { Gen7Editor.ResetCommand.Execute(null); _gen7ParentBaseline = (SAV7)_sav.Clone(); LoadFromSave(); return; }
        if (LgpeEditor is not null) { LgpeEditor.ResetCommand.Execute(null); _lgpeParentBaseline = (SAV7b)_sav.Clone(); LoadFromSave(); return; }
        if (_zaClosed) return;
        if (_zaSession is not null) { _zaEpoch++; _zaSession.Reset(); _sav = _zaSession.Staged; }
        ResetBattleBox();
        LoadFromSave();
    }

    public void RefreshLanguage()
    {
        BdspEditor?.RefreshLanguage();
        LgpeEditor?.RefreshLanguage();
        SwshEditor?.RefreshLanguage();
        PlaEditor?.RefreshLanguage();
        LanguageList = GameInfo.Sources.LanguageDataSource(_sav.Generation, _sav.Context);
        OnPropertyChanged(nameof(GenderList));
        if (HasRegionSettings || HasVivillon) RefreshRegionLists();
        if (_sav is SAV9ZA za) LoadZaImages(za);
    }
}

public partial class BadgeItemViewModel : ObservableObject
{
    public int Index { get; }
    
    [ObservableProperty]
    private bool _isObtained;

    public string DisplayName => $"Badge {Index}";

    public BadgeItemViewModel(int index, bool isObtained)
    {
        Index = index;
        IsObtained = isObtained;
    }
}
