using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// Hall of Fame editor for Gen 6 (X/Y and Omega Ruby/Alpha Sapphire). Edits a clone of the save:
/// every entry's clear number and date, and each team member's species, form, gender, level, held
/// item, moves, nickname, shiny flag, OT and IDs. Save copies the clone back; Cancel discards it.
/// </summary>
public partial class HallOfFameEditorViewModel : ViewModelBase, ICloseableDialog
{
    private readonly SaveFile _source;
    private readonly SAV6? _clone;
    private readonly HallOfFame6? _fame;
    private readonly ISpriteRenderer _spriteRenderer;

    public Action? CloseRequested { get; set; }

    public HallOfFameEditorViewModel(SaveFile sav, ISpriteRenderer spriteRenderer)
    {
        _source = sav;
        _spriteRenderer = spriteRenderer;

        // Determine support based on save type (the ORAS demo has no Hall of Fame block).
        IsSupported = sav is SAV6XY or SAV6AO;
        if (!IsSupported)
            return;

        _clone = (SAV6)sav.Clone();
        _fame = ((ISaveBlock6Main)_clone).HallOfFame;

        var sources = GameInfo.FilteredSources;
        SpeciesOptions = WithNone(sources.Species);
        ItemOptions = WithNone(sources.Items);
        MoveOptions = WithNone(sources.Moves);
        LoadEntries();
    }

    public bool IsSupported { get; }

    public IReadOnlyList<ComboItem> SpeciesOptions { get; } = [];
    public IReadOnlyList<ComboItem> ItemOptions { get; } = [];
    public IReadOnlyList<ComboItem> MoveOptions { get; } = [];

    public IReadOnlyList<ComboItem> GenderOptions { get; } =
    [
        new(LocalizedStrings.Instance["HallOfFameEditor_Male"], 0),
        new(LocalizedStrings.Instance["HallOfFameEditor_Female"], 1),
        new(LocalizedStrings.Instance["HallOfFameEditor_Genderless"], 2),
    ];

    public IReadOnlyList<ComboItem> OriginalTrainerGenderOptions { get; } =
    [
        new(LocalizedStrings.Instance["HallOfFameEditor_Male"], 0),
        new(LocalizedStrings.Instance["HallOfFameEditor_Female"], 1),
    ];

    [ObservableProperty]
    private ObservableCollection<HallOfFameEntryViewModel> _entries = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteEntryCommand))]
    private HallOfFameEntryViewModel? _selectedEntry;

    partial void OnSelectedEntryChanged(HallOfFameEntryViewModel? value)
        => value?.SelectFirstMember();

    private void LoadEntries()
    {
        if (_fame is null || _clone is null)
            return;

        var selected = SelectedEntry?.Index;
        Entries.Clear();
        for (int i = 0; i < HallOfFame6.Entries; i++)
        {
            if (!HallOfFameEntryViewModel.HasData(_fame, i))
                continue;
            Entries.Add(new HallOfFameEntryViewModel(i, _fame, _clone.Language, this));
        }

        SelectedEntry = Entries.FirstOrDefault(e => e.Index == selected) ?? Entries.FirstOrDefault();
    }

    internal byte[]? RenderSprite(ushort species, byte form, uint gender, bool isShiny)
    {
        if (_clone is null)
            return null;
        var pk = _clone.BlankPKM;
        pk.Species = species;
        pk.Form = form;
        pk.Gender = (byte)gender;
        if (isShiny)
            pk.SetShiny();
        return _spriteRenderer.GetSprite(pk);
    }

    [RelayCommand]
    private void Refresh() => LoadEntries();

    private bool CanDeleteEntry() => SelectedEntry is not null;

    /// <summary>
    /// Removes the selected entry the way the game does: later entries shift down one slot, so the
    /// list is rebuilt from the block afterwards.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteEntry))]
    private void DeleteEntry()
    {
        if (_fame is null || SelectedEntry is not { } entry)
            return;

        _fame.ClearEntry(entry.Index);
        SelectedEntry = null;
        LoadEntries();
    }

    [RelayCommand]
    private void Save()
    {
        if (_clone is null)
            return;

        _source.CopyChangesFrom(_clone);
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    /// <summary>The filtered sources may omit value 0, which is a legitimate empty member/item/move.</summary>
    private static IReadOnlyList<ComboItem> WithNone(IReadOnlyList<ComboItem> items)
    {
        if (items.Any(i => i.Value == 0))
            return items;
        return [new ComboItem(LocalizedStrings.Instance["HallOfFameEditor_None"], 0), .. items];
    }
}

/// <summary>One Hall of Fame entry. Its clear number and date are packed into the entry's trailing index word.</summary>
public partial class HallOfFameEntryViewModel : ViewModelBase
{
    private readonly HallOfFame6 _fame;
    private readonly bool _isLoading;

    internal HallOfFameEntryViewModel(int index, HallOfFame6 fame, int language, HallOfFameEditorViewModel owner)
    {
        Index = index;
        _fame = fame;
        Owner = owner;

        _isLoading = true;
        var info = ReadIndex(fame, index);
        ClearNumber = (int)info.ClearIndex;
        Date = ToDate(info.Year, info.Month, info.Day);
        _isLoading = false;

        for (int member = 0; member < HallOfFame6.PokeCount; member++)
        {
            if (new HallFame6Entity(fame.GetEntity(index, member), language).Species == 0)
                continue;
            Members.Add(new HallOfFameMemberViewModel(index, member, fame, language, owner));
        }
    }

    public int Index { get; }
    public HallOfFameEditorViewModel Owner { get; }
    public int MaxClearNumber => HallOfFame6.MaxComplete;

    public string Title => ClearNumber == 0
        ? LocalizedStrings.Instance["HallOfFameEditor_FirstClear"]
        : LocalizedStrings.Instance.Format("HallOfFameEditor_ClearNumber", ClearNumber);
    public string DateString => Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private int _clearNumber;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateString))]
    private DateTimeOffset? _date;

    [ObservableProperty]
    private ObservableCollection<HallOfFameMemberViewModel> _members = [];

    [ObservableProperty]
    private HallOfFameMemberViewModel? _selectedMember;

    internal void SelectFirstMember() => SelectedMember ??= Members.FirstOrDefault();

    partial void OnClearNumberChanged(int value)
    {
        if (_isLoading)
            return;
        var info = ReadIndex(_fame, Index);
        info.ClearIndex = (uint)Math.Clamp(value, 0, HallOfFame6.MaxComplete);
    }

    partial void OnDateChanged(DateTimeOffset? value)
    {
        if (_isLoading || value is not { } date)
            return;
        // The year is stored as an 8-bit offset from 2000.
        var year = Math.Clamp(date.Year, 2000, 2255);
        var info = ReadIndex(_fame, Index);
        info.Year = (uint)(year - 2000);
        info.Month = (uint)date.Month;
        info.Day = (uint)date.Day;
    }

    internal static bool HasData(HallOfFame6 fame, int index) => ReadIndex(fame, index).HasData;

    private static HallFame6Index ReadIndex(HallOfFame6 fame, int index)
        => new(fame.GetEntry(index)[^HallFame6Index.SIZE..]);

    private static DateTimeOffset? ToDate(uint year, uint month, uint day)
    {
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth((int)(2000 + year), (int)month))
            return null;
        return new DateTimeOffset(new DateTime((int)(2000 + year), (int)month, (int)day));
    }
}

/// <summary>
/// One team member. <see cref="HallFame6Entity"/> is a ref struct over the save's bytes, so it is
/// re-wrapped for every read and write instead of being kept.
/// </summary>
public partial class HallOfFameMemberViewModel : ViewModelBase
{
    private delegate void EntityEdit(HallFame6Entity entity);

    private readonly int _team;
    private readonly int _member;
    private readonly HallOfFame6 _fame;
    private readonly int _language;
    private readonly HallOfFameEditorViewModel _owner;
    private readonly bool _isLoading;

    internal HallOfFameMemberViewModel(int team, int member, HallOfFame6 fame, int language, HallOfFameEditorViewModel owner)
    {
        _team = team;
        _member = member;
        _fame = fame;
        _language = language;
        _owner = owner;

        _isLoading = true;
        var e = Entity;
        Species = e.Species;
        Form = e.Form;
        Gender = (int)e.Gender;
        Level = (int)e.Level;
        HeldItem = e.HeldItem;
        Move1 = e.Move1;
        Move2 = e.Move2;
        Move3 = e.Move3;
        Move4 = e.Move4;
        Nickname = e.Nickname;
        IsNicknamed = e.IsNicknamed;
        IsShiny = e.IsShiny;
        OriginalTrainerName = e.OriginalTrainerName;
        OriginalTrainerGender = (int)e.OriginalTrainerGender;
        TID16 = e.TID16;
        SID16 = e.SID16;
        EncryptionConstant = e.EncryptionConstant.ToString("X8", CultureInfo.InvariantCulture);
        _isLoading = false;
    }

    private HallFame6Entity Entity => new(_fame.GetEntity(_team, _member), _language);

    private void Edit(EntityEdit edit)
    {
        if (_isLoading)
            return;
        edit(Entity);
    }

    public HallOfFameEditorViewModel Owner => _owner;

    public string SpeciesName
    {
        get
        {
            var names = GameInfo.Strings.Species;
            return (uint)Species < (uint)names.Count ? names[Species] : Species.ToString(CultureInfo.InvariantCulture);
        }
    }

    public string DisplayName => string.IsNullOrEmpty(Nickname) || Nickname == SpeciesName
        ? SpeciesName
        : $"{Nickname} ({SpeciesName})";

    public byte[]? Sprite => _owner.RenderSprite((ushort)Species, (byte)Form, (uint)Gender, IsShiny);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeciesName), nameof(DisplayName), nameof(Sprite))]
    private int _species;
    partial void OnSpeciesChanged(int value) => Edit(e => e.Species = (ushort)Math.Clamp(value, 0, ushort.MaxValue));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Sprite))]
    private int _form;
    partial void OnFormChanged(int value) => Edit(e => e.Form = (byte)Math.Clamp(value, 0, MaxForm));

    /// <summary>The form is a 5-bit field in the packed member word.</summary>
    public int MaxForm => 31;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Sprite))]
    private int _gender;
    partial void OnGenderChanged(int value) => Edit(e => e.Gender = (uint)Math.Clamp(value, 0, 2));

    [ObservableProperty]
    private int _level;
    partial void OnLevelChanged(int value) => Edit(e => e.Level = (uint)Math.Clamp(value, 1, 100));

    [ObservableProperty]
    private int _heldItem;
    partial void OnHeldItemChanged(int value) => Edit(e => e.HeldItem = (ushort)value);

    [ObservableProperty] private int _move1;
    [ObservableProperty] private int _move2;
    [ObservableProperty] private int _move3;
    [ObservableProperty] private int _move4;
    partial void OnMove1Changed(int value) => Edit(e => e.Move1 = (ushort)value);
    partial void OnMove2Changed(int value) => Edit(e => e.Move2 = (ushort)value);
    partial void OnMove3Changed(int value) => Edit(e => e.Move3 = (ushort)value);
    partial void OnMove4Changed(int value) => Edit(e => e.Move4 = (ushort)value);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _nickname = string.Empty;
    partial void OnNicknameChanged(string value) => Edit(e => e.Nickname = value ?? string.Empty);

    [ObservableProperty]
    private bool _isNicknamed;
    partial void OnIsNicknamedChanged(bool value) => Edit(e => e.IsNicknamed = value);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Sprite))]
    private bool _isShiny;
    partial void OnIsShinyChanged(bool value) => Edit(e => e.IsShiny = value);

    [ObservableProperty]
    private string _originalTrainerName = string.Empty;
    partial void OnOriginalTrainerNameChanged(string value) => Edit(e => e.OriginalTrainerName = value ?? string.Empty);

    [ObservableProperty]
    private int _originalTrainerGender;
    partial void OnOriginalTrainerGenderChanged(int value) => Edit(e => e.OriginalTrainerGender = (uint)Math.Clamp(value, 0, 1));

    [ObservableProperty]
    private int _tID16;
    partial void OnTID16Changed(int value) => Edit(e => e.TID16 = (ushort)Math.Clamp(value, 0, ushort.MaxValue));

    [ObservableProperty]
    private int _sID16;
    partial void OnSID16Changed(int value) => Edit(e => e.SID16 = (ushort)Math.Clamp(value, 0, ushort.MaxValue));

    /// <summary>Hex text; an unparsable value leaves the stored constant unchanged.</summary>
    [ObservableProperty]
    private string _encryptionConstant = "00000000";
    partial void OnEncryptionConstantChanged(string value)
    {
        if (uint.TryParse(value?.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var ec))
            Edit(e => e.EncryptionConstant = ec);
    }
}
