using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;
using PKHeX.Core;

namespace PKHeX.Presentation.ViewModels;

public partial class Misc7EditorViewModel
{
    [ObservableProperty] private string _trainerName = string.Empty;
    [ObservableProperty] private uint _money = 0;
    [ObservableProperty] private int _gender = 0;
    [ObservableProperty] private int _language = 0;
    [ObservableProperty] private ushort _tid16 = 0;
    [ObservableProperty] private ushort _sid16 = 0;
    [ObservableProperty] private uint _displayTid;
    [ObservableProperty] private uint _displaySid;
    [ObservableProperty] private int _playedHours = 0;
    [ObservableProperty] private int _playedMinutes = 0;
    [ObservableProperty] private int _playedSeconds = 0;
    [ObservableProperty] private int _country = 0;
    [ObservableProperty] private int _subRegion = 0;
    [ObservableProperty] private int _consoleRegion = 0;
    [ObservableProperty] private int _vivillon = 0;
    [ObservableProperty] private uint _battlePoints = 0;
    [ObservableProperty] private int _festivalCoins = 0;
    [ObservableProperty] private string _festivalName = string.Empty;
    [ObservableProperty] private ulong _alolaOffset = 0;
    [ObservableProperty] private int _mapId = 0;
    [ObservableProperty] private int _skinColor = 0;
    [ObservableProperty] private int _daysFromRefresh = 0;
    [ObservableProperty] private int _battleStyle = 0;
    [ObservableProperty] private bool _megaUnlocked = false;
    [ObservableProperty] private bool _zMoveUnlocked = false;
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _z;
    [ObservableProperty] private double _rotation;
    [ObservableProperty] private DateTimeOffset? _startedDate;
    [ObservableProperty] private TimeSpan? _startedTime;
    [ObservableProperty] private int _startedSecond;
    [ObservableProperty] private DateTimeOffset? _fameDate;
    [ObservableProperty] private TimeSpan? _fameTime;
    [ObservableProperty] private int _fameSecond;
    [ObservableProperty] private DateTimeOffset? _lastSavedDate;
    [ObservableProperty] private TimeSpan? _lastSavedTime;
    [ObservableProperty] private int _lastSavedSecond;
    [ObservableProperty] private IReadOnlyList<ComboItem> _countries = [];
    [ObservableProperty] private IReadOnlyList<ComboItem> _regions = [];
    [ObservableProperty] private IReadOnlyList<ComboItem> _vivillonPatterns = [];
    private bool _loadingRegions;
    public IReadOnlyList<ComboItem> Genders => WithUnknown([new(Localization.LocalizedStrings.Instance["Pokedex5Editor_Male"], 0), new(Localization.LocalizedStrings.Instance["Pokedex5Editor_Female"], 1)], Gender);
    public IReadOnlyList<ComboItem> Languages => WithUnknown(GameInfo.Sources.LanguageDataSource(7, EntityContext.Gen7), Language);
    public IReadOnlyList<ComboItem> ConsoleRegions => RegionChoices.ConsoleRegions(ConsoleRegion);
    public IReadOnlyList<ComboItem> SkinColors => WithUnknown(Enumerable.Range(0, 8).Select(i => new ComboItem(Localization.LocalizedStrings.Instance["Trainer7_Skin_" + i], i)).ToArray(), SkinColor);
    public IReadOnlyList<ComboItem> BattleStyles => WithUnknown(Enumerable.Range(0, IsUSUM ? 9 : 8).Select(i => new ComboItem(Localization.LocalizedStrings.Instance["Trainer7_Style_" + i], i)).ToArray(), BattleStyle);
    public IReadOnlyList<Gen7TimeOffsetChoice> TimeOffsets
    {
        get
        {
            var strings = Localization.LocalizedStrings.Instance;
            var values = Enumerable.Range(1, 23).Select(hour => new Gen7TimeOffsetChoice(hour == 12 ? strings["Trainer7_MoonTime"] : strings.Format("Trainer7_OffsetHours", hour), (ulong)hour * 3600)).ToList();
            values.Insert(0, new(strings["Trainer7_SunTime"], 86400));
            if (values.All(item => item.Value != AlolaOffset)) values.Add(new(strings.Format("RaidSession_UnknownType", AlolaOffset), AlolaOffset));
            return values;
        }
    }
    private static IReadOnlyList<ComboItem> WithUnknown(IEnumerable<ComboItem> choices, int selected)
    {
        var result = choices.ToList();
        if (result.All(item => item.Value != selected)) result.Add(new(Localization.LocalizedStrings.Instance.Format("RaidSession_UnknownType", selected), selected));
        return result;
    }
    public ObservableCollection<Gen7TrainerRecordViewModel> TrainerRecords { get; } = [];
    public ObservableCollection<Gen7BattleStyleFlagViewModel> StyleFlags { get; } = [];
    [ObservableProperty] private Gen7TrainerRecordViewModel? _selectedRecord;
    private readonly Dictionary<string, string> _coordinateText = new();
    private readonly HashSet<string> _invalidCoordinateText = new();
    private bool _editingCoordinateText;
    public string XText { get => CoordinateText("X", X); set => SetCoordinateText("X", value); }
    public string YText { get => CoordinateText("Y", Y); set => SetCoordinateText("Y", value); }
    public string ZText { get => CoordinateText("Z", Z); set => SetCoordinateText("Z", value); }
    private string CoordinateText(string axis, double value) => _coordinateText.GetValueOrDefault(axis) ?? value.ToString("R", CultureInfo.CurrentCulture);
    private void SetCoordinateText(string axis, string text)
    {
        _coordinateText[axis] = text;
        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var value))
        {
            _invalidCoordinateText.Remove(axis); _editingCoordinateText = true;
            try { if (axis == "X") X = value; else if (axis == "Y") Y = value; else Z = value; }
            finally { _editingCoordinateText = false; }
        }
        else _invalidCoordinateText.Add(axis);
        OnPropertyChanged(axis + "Text"); OnPropertyChanged(nameof(CanSave));
    }
    private void CoordinateChanged(string axis)
    {
        if (!_editingCoordinateText) { _coordinateText.Remove(axis); _invalidCoordinateText.Remove(axis); OnPropertyChanged(axis + "Text"); }
    }
    partial void OnXChanged(double value) => CoordinateChanged("X");
    partial void OnYChanged(double value) => CoordinateChanged("Y");
    partial void OnZChanged(double value) => CoordinateChanged("Z");

    public bool CanSave => !_closed && DisplayTid <= 999999 && (ulong)DisplaySid * 1_000_000 + DisplayTid <= uint.MaxValue && _invalidCoordinateText.Count == 0 && TrainerName.Length <= _sav.MaxStringLengthTrainer && FestivalName.Length <= 20 &&
        (Money == _baseline.Money || Money <= _sav.MaxMoney) &&
        SameOrRange(Gender, _baseline.Gender, 0, 1) && SameOrRange(Language, _baseline.Language, 1, 10) &&
        Country is >= 0 and <= 255 && SubRegion is >= 0 and <= 255 && ConsoleRegion is >= 0 and <= 255 && SameOrRange(Vivillon, _baseline.Misc.Vivillon, 0, 19) &&
        PlayedHours is >= 0 and <= 65535 && SameOrRange(PlayedMinutes, _baseline.PlayedMinutes, 0, 59) && SameOrRange(PlayedSeconds, _baseline.PlayedSeconds, 0, 59) &&
        (BattlePoints == _baseline.Misc.BP || BattlePoints <= 9999) && SameOrRange(FestivalCoins, _baseline.Festa.FestaCoins, 0, 9999999) &&
        (AlolaOffset == _baseline.GameTime.AlolaTime || AlolaOffset is >= 3600 and <= 86400 && AlolaOffset % 3600 == 0) &&
        MapId is >= 0 and <= 65535 && ValidPosition(X, _baseline.Situation.X) && ValidPosition(Y, _baseline.Situation.Y) && ValidPosition(Z, _baseline.Situation.Z) &&
        (Rotation.Equals(Math.Atan2(_baseline.Situation.RZ, _baseline.Situation.RW) * 360.0 / Math.PI) || double.IsFinite(Rotation) && Math.Abs(Rotation) <= 360) &&
        SkinColor is >= 0 and <= 7 && DaysFromRefresh is >= 0 and <= 255 && SameOrRange(BattleStyle, _baseline.MyStatus.BallThrowType, 0, IsUSUM ? 8 : 7) &&
        ValidDate(StartedDate, StartedTime, StartedSecond, ReadSeconds(_baseline.SecondsToStart), true) &&
        ValidDate(FameDate, FameTime, FameSecond, ReadSeconds(_baseline.SecondsToFame), true) &&
        ValidDate(LastSavedDate, LastSavedTime, 0, ReadLastSaved(_baseline), false) &&
        (SnapCount == _baseline.PokeFinder.SnapCount || SnapCount <= 9999999) &&
        (ThumbsRecord == _baseline.PokeFinder.ThumbsHighValue || ThumbsRecord <= 9999999) &&
        SameOrRange(CameraVersion, _baseline.PokeFinder.CameraVersion, 0, 2) && TrainerRecords.All(record => record.Value == record.Original || record.Value >= 0 && record.Value <= record.Maximum) &&
        new[] { SingleCurrentStreak, SingleMaxStreak, DoubleCurrentStreak, DoubleMaxStreak, MultiCurrentStreak, MultiMaxStreak,
            SuperSingleCurrentStreak, SuperSingleMaxStreak, SuperDoubleCurrentStreak, SuperDoubleMaxStreak, SuperMultiCurrentStreak, SuperMultiMaxStreak }.All(value => value is >= 0 and <= 65535);

    private static bool SameOrRange(int value, int original, int min, int max) => value == original || value >= min && value <= max;
    private static bool ValidPosition(double value, float original) => value.Equals(original / 60.0) || double.IsFinite(value) && Math.Abs(value * 60) <= float.MaxValue;
    private static bool ValidDate(DateTimeOffset? date, TimeSpan? time, int second, DateTime? original, bool secondsEpoch)
    {
        if (date is null && time is null) return original is null;
        if (date is null || time is null || second is < 0 or > 59 || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1)) return false;
        var value = date.Value.Date + new TimeSpan(time.Value.Hours, time.Value.Minutes, second);
        return secondsEpoch ? value >= new DateTime(2000, 1, 1) && (value - new DateTime(2000, 1, 1)).TotalSeconds <= uint.MaxValue : value.Year <= 4095;
    }

    private void LoadTrainerFields()
    {
        _coordinateText.Clear(); _invalidCoordinateText.Clear();
        TrainerName = _sav.OT;
        Money = _sav.Money;
        Gender = _sav.Gender;
        Language = _sav.Language;
        Tid16 = _sav.TID16;
        Sid16 = _sav.SID16;
        DisplayTid = _sav.DisplayTID; DisplaySid = _sav.DisplaySID;
        PlayedHours = _sav.PlayedHours;
        PlayedMinutes = _sav.PlayedMinutes;
        PlayedSeconds = _sav.PlayedSeconds;
        _loadingRegions = true;
        try { Country = _sav.Country; SubRegion = _sav.Region; }
        finally { _loadingRegions = false; }
        ConsoleRegion = _sav.ConsoleRegion;
        Vivillon = _sav.Misc.Vivillon;
        BattlePoints = _sav.Misc.BP;
        FestivalCoins = _sav.Festa.FestaCoins;
        FestivalName = _sav.Festa.FestivalPlazaName;
        AlolaOffset = _sav.GameTime.AlolaTime;
        MapId = _sav.Situation.M;
        SkinColor = _sav.MyStatus.DressUpSkinColor;
        DaysFromRefresh = _sav.Misc.DaysFromRefreshed;
        BattleStyle = _sav.MyStatus.BallThrowType;
        MegaUnlocked = _sav.MyStatus.MegaUnlocked;
        ZMoveUnlocked = _sav.MyStatus.ZMoveUnlocked;
        X = _sav.Situation.X / 60.0; Y = _sav.Situation.Y / 60.0; Z = _sav.Situation.Z / 60.0;
        Rotation = Math.Atan2(_sav.Situation.RZ, _sav.Situation.RW) * 360.0 / Math.PI;
        SetDateParts(ReadSeconds(_sav.SecondsToStart), true); SetDateParts(ReadSeconds(_sav.SecondsToFame), false);
        var last = ReadLastSaved(_sav);
        LastSavedDate = last is { } value ? new DateTimeOffset(value.Date, TimeSpan.Zero) : null;
        LastSavedTime = last?.TimeOfDay; LastSavedSecond = 0;
        Countries = RegionChoices.Countries(Country); Regions = RegionChoices.SubRegions(Country, SubRegion);
        VivillonPatterns = RegionChoices.VivillonPatterns(Vivillon, EntityContext.Gen7);
        TrainerRecords.Clear();
        foreach (var record in RecordLists.RecordList_7)
            if (IsUSUM || record.Key is not (70 or 72 or 73 or 74))
                TrainerRecords.Add(new(record.Key, record.Value, _sav.GetRecord(record.Key), _sav.GetRecordMax(record.Key)));
        SelectedRecord = TrainerRecords.FirstOrDefault();
        StyleFlags.Clear();
        if (IsSM)
            for (int i = 0; i < 8; i++) StyleFlags.Add(new(i, Localization.LocalizedStrings.Instance["Trainer7_Style_" + i],
                i < 2 || _sav.EventWork.GetEventFlag(292 + i), i == 0 || _sav.EventWork.GetEventFlag(3479 + i)));
        OnPropertyChanged(nameof(XText)); OnPropertyChanged(nameof(YText)); OnPropertyChanged(nameof(ZText));
    }

    partial void OnCountryChanged(int value)
    {
        // Sub-region IDs are per country: a user-picked country resets an ID it does not define.
        if (!_loadingRegions && !RegionChoices.IsKnownSubRegion(value, SubRegion)) SubRegion = 0;
        Regions = RegionChoices.SubRegions(value, SubRegion);
        if (!_loadingRegions) OnPropertyChanged(nameof(SubRegion)); // replacing the list cleared the dropdown selection
    }
    partial void OnSubRegionChanged(int value)
    {
        if (Regions.All(item => item.Value != value)) { Regions = RegionChoices.SubRegions(Country, value); OnPropertyChanged(nameof(SubRegion)); }
    }
    partial void OnVivillonChanged(int value)
    {
        if (VivillonPatterns.All(item => item.Value != value)) { VivillonPatterns = RegionChoices.VivillonPatterns(value, EntityContext.Gen7); OnPropertyChanged(nameof(Vivillon)); }
    }

    private static DateTime? ReadLastSaved(SAV7 save)
    { try { return save.Played.LastSavedDate; } catch (ArgumentOutOfRangeException) { return null; } }

    private static DateTime? ReadSeconds(uint value) => value == 0 ? null : new DateTime(2000, 1, 1).AddSeconds(value);
    private void SetDateParts(DateTime? value, bool started)
    {
        var date = value is { } d ? new DateTimeOffset(d.Date, TimeSpan.Zero) : (DateTimeOffset?)null;
        if (started) { StartedDate = date; StartedTime = value?.TimeOfDay; StartedSecond = value?.Second ?? 0; }
        else { FameDate = date; FameTime = value?.TimeOfDay; FameSecond = value?.Second ?? 0; }
    }

    private void SaveTrainerFields()
    {
        if (TrainerName != _baseline.OT) _sav.OT = TrainerName;
        if (Money != _baseline.Money) _sav.Money = Money;
        if (Gender != _baseline.Gender) _sav.Gender = (byte)Gender;
        if (Language != _baseline.Language) _sav.Language = Language;
        if (Tid16 != _baseline.TID16) _sav.TID16 = Tid16;
        if (Sid16 != _baseline.SID16) _sav.SID16 = Sid16;
        if (DisplayTid != _baseline.DisplayTID || DisplaySid != _baseline.DisplaySID) _sav.SetDisplayID(DisplayTid, DisplaySid);
        if (PlayedHours != _baseline.PlayedHours) _sav.PlayedHours = PlayedHours;
        if (PlayedMinutes != _baseline.PlayedMinutes) _sav.PlayedMinutes = PlayedMinutes;
        if (PlayedSeconds != _baseline.PlayedSeconds) _sav.PlayedSeconds = PlayedSeconds;
        if (Country != _baseline.Country) _sav.Country = (byte)Country;
        if (SubRegion != _baseline.Region) _sav.Region = (byte)SubRegion;
        if (ConsoleRegion != _baseline.ConsoleRegion) _sav.ConsoleRegion = (byte)ConsoleRegion;
        if (Vivillon != _baseline.Misc.Vivillon) _sav.Misc.Vivillon = Vivillon;
        if (BattlePoints != _baseline.Misc.BP) _sav.Misc.BP = BattlePoints;
        if (FestivalCoins != _baseline.Festa.FestaCoins) _sav.Festa.FestaCoins = FestivalCoins;
        if (FestivalName != _baseline.Festa.FestivalPlazaName) _sav.Festa.FestivalPlazaName = FestivalName;
        if (AlolaOffset != _baseline.GameTime.AlolaTime) _sav.GameTime.AlolaTime = AlolaOffset;
        if (MapId != _baseline.Situation.M) _sav.Situation.M = MapId;
        if (SkinColor != _baseline.MyStatus.DressUpSkinColor) _sav.MyStatus.DressUpSkinColor = SkinColor;
        if (DaysFromRefresh != _baseline.Misc.DaysFromRefreshed) _sav.Misc.DaysFromRefreshed = DaysFromRefresh;
        if (BattleStyle != _baseline.MyStatus.BallThrowType) _sav.MyStatus.BallThrowType = (byte)BattleStyle;
        if (MegaUnlocked != _baseline.MyStatus.MegaUnlocked) _sav.MyStatus.MegaUnlocked = MegaUnlocked;
        if (ZMoveUnlocked != _baseline.MyStatus.ZMoveUnlocked) _sav.MyStatus.ZMoveUnlocked = ZMoveUnlocked;
        bool moved = false;
        if (!X.Equals(_baseline.Situation.X / 60.0)) { _sav.Situation.X = (float)(X * 60); moved = true; }
        if (!Y.Equals(_baseline.Situation.Y / 60.0)) { _sav.Situation.Y = (float)(Y * 60); moved = true; }
        if (!Z.Equals(_baseline.Situation.Z / 60.0)) { _sav.Situation.Z = (float)(Z * 60); moved = true; }
        if (Math.Abs(Rotation - Math.Atan2(_baseline.Situation.RZ, _baseline.Situation.RW) * 360.0 / Math.PI) > 0.00000001)
        {
            double angle = Rotation * Math.PI / 360.0; _sav.Situation.RX = 0; _sav.Situation.RY = 0;
            _sav.Situation.RZ = (float)Math.Sin(angle); _sav.Situation.RW = (float)Math.Cos(angle); moved = true;
        }
        if (moved) _sav.Situation.UpdateOverworldCoordinates();
        if (Combine(StartedDate, StartedTime, StartedSecond) is { } started && started != ReadSeconds(_baseline.SecondsToStart))
            _sav.SecondsToStart = checked((uint)(started - new DateTime(2000, 1, 1)).TotalSeconds);
        if (Combine(FameDate, FameTime, FameSecond) is { } fame && fame != ReadSeconds(_baseline.SecondsToFame))
            _sav.SecondsToFame = checked((uint)(fame - new DateTime(2000, 1, 1)).TotalSeconds);
        if (Combine(LastSavedDate, LastSavedTime, 0) is { } last && last != ReadLastSaved(_baseline))
            _sav.Played.LastSavedDate = last;
        foreach (var record in TrainerRecords)
            if (record.Value != record.Original) _sav.SetRecord(record.Id, record.Value);
        foreach (var style in StyleFlags)
        {
            if (style.CanUnlock && style.Unlocked != _baseline.EventWork.GetEventFlag(292 + style.Id))
                _sav.EventWork.SetEventFlag(292 + style.Id, style.Unlocked);
            if (style.CanLearn && style.Learned != _baseline.EventWork.GetEventFlag(3479 + style.Id))
                _sav.EventWork.SetEventFlag(3479 + style.Id, style.Learned);
        }
    }

    private static DateTime? Combine(DateTimeOffset? date, TimeSpan? time, int second) =>
        date is { } day && time is { } clock ? day.Date + new TimeSpan(clock.Hours, clock.Minutes, second) : null;
}

public partial class Gen7TrainerRecordViewModel(int id, string name, int value, int maximum) : ObservableObject
{
    public int Id { get; } = id;
    public string Name { get; } = name;
    public int Maximum { get; } = maximum;
    public int Original { get; } = value;
    public int DisplayMinimum => Math.Min(0, Original);
    public int DisplayMaximum => Math.Max(Maximum, Original);
    [ObservableProperty] private int _value = value;
}

public sealed record Gen7TimeOffsetChoice(string Name, ulong Value);

public partial class Gen7BattleStyleFlagViewModel(int id, string name, bool unlocked, bool learned) : ObservableObject
{
    public int Id { get; } = id;
    public bool CanUnlock => Id >= 2;
    public bool CanLearn => Id >= 1;
    [ObservableProperty] private string _name = name;
    [ObservableProperty] private bool _unlocked = unlocked;
    [ObservableProperty] private bool _learned = learned;
}
