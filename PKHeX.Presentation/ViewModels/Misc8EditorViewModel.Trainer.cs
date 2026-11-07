using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using System.Collections.ObjectModel;
using System.Globalization;

namespace PKHeX.Presentation.ViewModels;

public partial class Misc8EditorViewModel
{
    [ObservableProperty] private string _trainerName = string.Empty;
    [ObservableProperty] private uint _money = 0;
    [ObservableProperty] private int _badgeCount;
    [ObservableProperty] private int _gender = 0;
    [ObservableProperty] private int _language = 0;
    [ObservableProperty] private uint _displayTid = 0;
    [ObservableProperty] private uint _displaySid = 0;
    [ObservableProperty] private int _playedHours = 0;
    [ObservableProperty] private int _playedMinutes = 0;
    [ObservableProperty] private int _playedSeconds = 0;
    [ObservableProperty] private string _leagueCardName = string.Empty;
    [ObservableProperty] private string _uniformNumber = string.Empty;
    [ObservableProperty] private int _leagueTrainerId = 0;
    [ObservableProperty] private int _rotoRallyScore = 0;
    [ObservableProperty] private ulong _mapId = 0;
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _z;
    [ObservableProperty] private double _sX;
    [ObservableProperty] private double _sY;
    [ObservableProperty] private double _sZ;
    [ObservableProperty] private double _rotation;
    [ObservableProperty] private int _skinColor;
    [ObservableProperty] private DateTimeOffset? _startedDate;
    [ObservableProperty] private DateTimeOffset? _lastSavedDate;
    [ObservableProperty] private TimeSpan? _lastSavedTime;
    public IReadOnlyList<ComboItem> Genders => WithUnknown([new(LocalizedStrings.Instance["Pokedex5Editor_Male"], 0), new(LocalizedStrings.Instance["Pokedex5Editor_Female"], 1)], Gender);
    public IReadOnlyList<ComboItem> Languages => WithUnknown(GameInfo.Sources.LanguageDataSource(8, EntityContext.Gen8), Language);
    public IReadOnlyList<ComboItem> SkinColors => WithUnknown(Enumerable.Range(0, 8).Select(i => new ComboItem(LocalizedStrings.Instance["Trainer7_Skin_" + i], i)), SkinColor);
    public ObservableCollection<SwshTrainerRecordViewModel> TrainerRecords { get; } = [];
    [ObservableProperty] private SwshTrainerRecordViewModel? _selectedRecord;
    private readonly Dictionary<string, string> _geometryText = new();
    private readonly HashSet<string> _invalidGeometry = new();
    public string XText { get => GeometryText("X", X); set => SetGeometry("X", value); }
    public string YText { get => GeometryText("Y", Y); set => SetGeometry("Y", value); }
    public string ZText { get => GeometryText("Z", Z); set => SetGeometry("Z", value); }
    public string SXText { get => GeometryText("SX", SX); set => SetGeometry("SX", value); }
    public string SYText { get => GeometryText("SY", SY); set => SetGeometry("SY", value); }
    public string SZText { get => GeometryText("SZ", SZ); set => SetGeometry("SZ", value); }
    private string GeometryText(string key, double value) => _geometryText.GetValueOrDefault(key) ?? value.ToString("R", CultureInfo.CurrentCulture);
    private void SetGeometry(string key, string text)
    {
        _geometryText[key] = text;
        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var value))
        { _invalidGeometry.Remove(key); switch (key) { case "X": X = value; break; case "Y": Y = value; break; case "Z": Z = value; break; case "SX": SX = value; break; case "SY": SY = value; break; default: SZ = value; break; } }
        else _invalidGeometry.Add(key);
        OnPropertyChanged(key + "Text"); OnPropertyChanged(nameof(CanSave));
    }
    private static IReadOnlyList<ComboItem> WithUnknown(IEnumerable<ComboItem> choices, int selected)
    { var values = choices.ToList(); if (values.All(item => item.Value != selected)) values.Add(new(LocalizedStrings.Instance.Format("RaidSession_UnknownType", selected), selected)); return values; }

    private void LoadTrainerFields()
    {
        _geometryText.Clear(); _invalidGeometry.Clear();
        TrainerName = _sav.OT;
        Money = _sav.Money;
        BadgeCount = _sav.Badges;
        Gender = _sav.Gender;
        Language = _sav.Language;
        DisplayTid = _sav.DisplayTID;
        DisplaySid = _sav.DisplaySID;
        PlayedHours = _sav.PlayedHours;
        PlayedMinutes = _sav.PlayedMinutes;
        PlayedSeconds = _sav.PlayedSeconds;
        LeagueCardName = _sav.TrainerCard.OT;
        UniformNumber = _sav.TrainerCard.Number;
        LeagueTrainerId = _sav.TrainerCard.TrainerID;
        RotoRallyScore = _sav.TrainerCard.RotoRallyScore;
        MapId = _sav.Coordinates.M;
        X = _sav.Coordinates.X;
        Y = _sav.Coordinates.Y;
        Z = _sav.Coordinates.Z;
        SX = _sav.Coordinates.SX;
        SY = _sav.Coordinates.SY;
        SZ = _sav.Coordinates.SZ;
        Rotation = Math.Atan2(_sav.Coordinates.RZ, _sav.Coordinates.RW) * 360 / Math.PI;
        SkinColor = (int)PlayerSkinColor8Extensions.GetSkinColorFromSkin(_sav.MyStatus.Skin);
        StartedDate = null;
        try { StartedDate = new DateTimeOffset(_sav.TrainerCard.StartedYear, _sav.TrainerCard.StartedMonth, _sav.TrainerCard.StartedDay, 0, 0, 0, TimeSpan.Zero); } catch (ArgumentOutOfRangeException) { }
        var last = ReadLastSaved(_sav); LastSavedDate = last is { } date ? new DateTimeOffset(date.Date, TimeSpan.Zero) : null; LastSavedTime = last?.TimeOfDay;
        TrainerRecords.Clear(); foreach (var record in RecordLists.RecordList_8.Where(record => record.Key >= 0 && record.Key < _sav.RecordCount))
            TrainerRecords.Add(new(record.Key, LocalizedStrings.Instance["Trainer8_Record_" + record.Key], _sav.GetRecord(record.Key), _sav.GetRecordMax(record.Key)));
        foreach (var (id, key) in new[] { (50, SaveBlockAccessor8SWSH.KRecordCramorantRobo), (51, SaveBlockAccessor8SWSH.KRecordSparringTypesCleared) })
            if (_sav.SaveRevision >= 1 && _sav.Blocks.TryGetBlock(key, out var block) && block.Type == SCTypeCode.UInt32)
                TrainerRecords.Add(new(id, LocalizedStrings.Instance["Trainer8_Record_" + id], unchecked((int)_sav.GetValue<uint>(key)), int.MaxValue));
        SelectedRecord = TrainerRecords.FirstOrDefault();
        foreach (var key in new[] { "X", "Y", "Z", "SX", "SY", "SZ" }) OnPropertyChanged(key + "Text");
    }

    private static DateTime? ReadLastSaved(SAV8SWSH save)
    { try { return save.Played.LastSavedDate; } catch (ArgumentOutOfRangeException) { return null; } }

    private void SaveTrainerFields()
    {
        if (TrainerName != _baseline.OT) _sav.OT = TrainerName;
        if (Money != _baseline.Money) _sav.Money = Money;
        if (BadgeCount != _baseline.Badges) _sav.Badges = BadgeCount;
        if (Gender != _baseline.Gender) _sav.Gender = (byte)Gender;
        if (Language != _baseline.Language) _sav.Language = Language;
        if (PlayedHours != _baseline.PlayedHours) _sav.PlayedHours = PlayedHours;
        if (PlayedMinutes != _baseline.PlayedMinutes) _sav.PlayedMinutes = PlayedMinutes;
        if (PlayedSeconds != _baseline.PlayedSeconds) _sav.PlayedSeconds = PlayedSeconds;
        if (LeagueCardName != _baseline.TrainerCard.OT) _sav.TrainerCard.OT = LeagueCardName;
        if (LeagueTrainerId != _baseline.TrainerCard.TrainerID) _sav.TrainerCard.TrainerID = LeagueTrainerId;
        if (RotoRallyScore != _baseline.TrainerCard.RotoRallyScore) _sav.TrainerCard.RotoRallyScore = RotoRallyScore;
        if (MapId != _baseline.Coordinates.M) _sav.Coordinates.M = MapId;
        if (DisplayTid != _baseline.DisplayTID || DisplaySid != _baseline.DisplaySID) _sav.SetDisplayID(DisplayTid, DisplaySid);
        if (UniformNumber != _baseline.TrainerCard.Number) { _sav.TrainerCard.Number = UniformNumber; _sav.MyStatus.Number = UniformNumber; }
        if (!X.Equals((double)_baseline.Coordinates.X)) _sav.Coordinates.X = (float)X;
        if (!Y.Equals((double)_baseline.Coordinates.Y)) _sav.Coordinates.Y = (float)Y;
        if (!Z.Equals((double)_baseline.Coordinates.Z)) _sav.Coordinates.Z = (float)Z;
        if (!SX.Equals((double)_baseline.Coordinates.SX)) _sav.Coordinates.SX = (float)SX;
        if (!SY.Equals((double)_baseline.Coordinates.SY)) _sav.Coordinates.SY = (float)SY;
        if (!SZ.Equals((double)_baseline.Coordinates.SZ)) _sav.Coordinates.SZ = (float)SZ;
        if (Math.Abs(Rotation - Math.Atan2(_baseline.Coordinates.RZ, _baseline.Coordinates.RW) * 360 / Math.PI) > 1e-8)
        { double angle = Rotation * Math.PI / 360; _sav.Coordinates.RX = 0; _sav.Coordinates.RY = 0; _sav.Coordinates.RZ = (float)Math.Sin(angle); _sav.Coordinates.RW = (float)Math.Cos(angle); }
        if (SkinColor != (int)PlayerSkinColor8Extensions.GetSkinColorFromSkin(_baseline.MyStatus.Skin)) _sav.MyStatus.SetSkinColor((PlayerSkinColor8)SkinColor);
        if (StartedDate is { } date && (date.Year != _baseline.TrainerCard.StartedYear || date.Month != _baseline.TrainerCard.StartedMonth || date.Day != _baseline.TrainerCard.StartedDay))
        { _sav.TrainerCard.StartedYear = (ushort)date.Year; _sav.TrainerCard.StartedMonth = (byte)date.Month; _sav.TrainerCard.StartedDay = (byte)date.Day; }
        if (LastSavedDate is { } saved && LastSavedTime is { } time && saved.Date + new TimeSpan(time.Hours, time.Minutes, 0) != ReadLastSaved(_baseline))
            _sav.Played.LastSavedDate = saved.Date + new TimeSpan(time.Hours, time.Minutes, 0);
        foreach (var record in TrainerRecords)
            if (record.Value != record.Original)
            {
                if (record.Id < _sav.RecordCount) _sav.SetRecord(record.Id, record.Value);
                else _sav.SetValue(record.Id == 50 ? SaveBlockAccessor8SWSH.KRecordCramorantRobo : SaveBlockAccessor8SWSH.KRecordSparringTypesCleared, (uint)record.Value);
            }
    }

    public bool CanSave => !_closed && _invalidGeometry.Count == 0 && TrainerName.Length <= 12 && LeagueCardName.Length <= 12 &&
        (UniformNumber == _baseline.TrainerCard.Number || UniformNumber.Length <= 3 && UniformNumber.All(c => c is >= '0' and <= '9' or '\0')) &&
        DisplayTid <= 999999 && (ulong)DisplaySid * 1000000 + DisplayTid <= uint.MaxValue &&
        (Money == _baseline.Money || Money <= _sav.MaxMoney) &&
        (BadgeCount == _baseline.Badges || BadgeCount is >= 0 and <= 8) &&
        (Gender == _baseline.Gender || Gender is 0 or 1) && (Language == _baseline.Language || Language is >= 1 and <= 10 && Language != 6) &&
        PlayedHours is >= 0 and <= 65535 && (PlayedMinutes == _baseline.PlayedMinutes || PlayedMinutes is >= 0 and <= 59) &&
        (PlayedSeconds == _baseline.PlayedSeconds || PlayedSeconds is >= 0 and <= 59) &&
        (LeagueTrainerId == _baseline.TrainerCard.TrainerID || LeagueTrainerId is >= 0 and <= 999999) &&
        ValidFloat(X, _baseline.Coordinates.X) && ValidFloat(Y, _baseline.Coordinates.Y) && ValidFloat(Z, _baseline.Coordinates.Z) &&
        ValidFloat(SX, _baseline.Coordinates.SX) && ValidFloat(SY, _baseline.Coordinates.SY) && ValidFloat(SZ, _baseline.Coordinates.SZ) &&
        (Rotation.Equals(Math.Atan2(_baseline.Coordinates.RZ, _baseline.Coordinates.RW) * 360 / Math.PI) || double.IsFinite(Rotation) && Math.Abs(Rotation) <= 360) &&
        (StartedDate is not null || !DateUtil.IsValidDate(_baseline.TrainerCard.StartedYear, _baseline.TrainerCard.StartedMonth, _baseline.TrainerCard.StartedDay)) &&
        (StartedDate is null || StartedDate.Value.Year <= ushort.MaxValue) && (LastSavedDate is not null || ReadLastSaved(_baseline) is null) &&
        (LastSavedDate.HasValue == LastSavedTime.HasValue) && (LastSavedDate is null || LastSavedDate.Value.Year is >= 1900 and <= 5995) &&
        (LastSavedTime is null || LastSavedTime >= TimeSpan.Zero && LastSavedTime < TimeSpan.FromDays(1)) &&
        (Watts == _baseline.MyStatus.Watt || Watts <= MyStatus8.MaxWatt) && (Bp == _baseline.Misc.BP || Bp is >= 0 and <= 9999) &&
        (RotoRallyScore == _baseline.TrainerCard.RotoRallyScore || RotoRallyScore is >= 0 and <= TrainerCard8.RotoRallyScoreMax) &&
        (SkinColor == (int)PlayerSkinColor8Extensions.GetSkinColorFromSkin(_baseline.MyStatus.Skin) || SkinColor is >= 0 and <= 7) &&
        TrainerRecords.All(record => record.Value == record.Original || record.Value >= 0 && record.Value <= record.Maximum);
    private static bool ValidFloat(double value, float original) => value.Equals((double)original) || double.IsFinite(value) && Math.Abs(value) <= float.MaxValue;

    private async Task RunActionAsync(string kind, Action<SAV8SWSH> action, Func<bool>? scopeStillValid = null)
    {
        if (_closed || _dialogs is null) return; int epoch = _epoch;
        if (!await _dialogs.ShowConfirmationAsync(LocalizedStrings.Instance["Trainer8_Action" + kind], LocalizedStrings.Instance["Trainer8_Confirm" + kind], LocalizedStrings.Instance["TrainerEditor_ApplyChanges"], LocalizedStrings.Instance["Common_Cancel"])) return;
        if (_closed || epoch != _epoch || scopeStillValid?.Invoke() is false) return;
        _session.ApplyAction(action); _sav = _session.Staged; _epoch++; OnPropertyChanged(nameof(CanUndo)); NotifyBattleTeams();
    }
    [RelayCommand] private Task ResetAppearance()
    {
        if (SkinColor is < 0 or > 7 || Gender is not (0 or 1)) return Task.CompletedTask;
        int skin = SkinColor, gender = Gender;
        return RunActionAsync("Appearance", save =>
        {
            byte original = save.Gender;
            try { save.Gender = (byte)gender; save.MyStatus.ResetAppearance((PlayerSkinColor8)skin); }
            finally { save.Gender = original; }
        }, () => Gender == gender && SkinColor == skin);
    }
    [RelayCommand] private void Undo() { if (_closed) return; _session.Undo(); _sav = _session.Staged; _epoch++; OnPropertyChanged(nameof(CanUndo)); NotifyBattleTeams(); }
    [RelayCommand] private void Reset() { if (_closed) return; _session.Reset(); _sav = _session.Staged; _baseline = (SAV8SWSH)_sav.Clone(); _epoch++; LoadTrainerFields(); LoadMisc(); LoadBattleTower(); Error = string.Empty; OnPropertyChanged(nameof(CanUndo)); NotifyBattleTeams(); }
    public void Dispose() { _closed = true; _epoch++; CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.UnregisterAll(this); UnlockBattleTeamsCommand.NotifyCanExecuteChanged(); }
    public void RefreshLanguage()
    {
        int gender = Gender, language = Language, skin = SkinColor;
        OnPropertyChanged(nameof(Genders)); OnPropertyChanged(nameof(Languages)); OnPropertyChanged(nameof(SkinColors));
        Gender = gender; Language = language; SkinColor = skin;
        foreach (var record in TrainerRecords) record.Name = LocalizedStrings.Instance["Trainer8_Record_" + record.Id];
    }
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (args.PropertyName is not (nameof(CanSave) or nameof(HasError))) base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(CanSave)));
    }
    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));
}

public partial class SwshTrainerRecordViewModel(int id, string name, int value, int maximum) : ObservableObject
{
    public int Id { get; } = id;
    public int Maximum { get; } = maximum;
    public int Original { get; } = value;
    public int DisplayMinimum => Math.Min(0, Original);
    public int DisplayMaximum => Math.Max(Maximum, Original);
    [ObservableProperty] private string _name = name;
    [ObservableProperty] private int _value = value;
}
