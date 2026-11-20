using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class EntralinkEditorViewModel : ViewModelBase, ICloseableDialog
{
    private readonly SAV5 _source;
    private readonly SAV5 _sav;
    private readonly SAV5B2W2? _b2w2;
    private readonly SAV5BW? _bw;
    private readonly Entralink5 _entralink;
    private readonly EntreeForest _forest;
    private readonly FestaBlock5? _festa;
    private readonly bool[] _originalEventFlags;
    private readonly ISpriteRenderer? _spriteRenderer;
    private readonly IDialogService? _dialogService;
    private readonly Random _random;
    private byte[]? _beforeRandomize;

    public Action? CloseRequested { get; set; }

    public EntralinkEditorViewModel(SaveFile sav, ISpriteRenderer? spriteRenderer = null,
        IDialogService? dialogService = null, Random? random = null)
    {
        _spriteRenderer = spriteRenderer;
        _dialogService = dialogService;
        _random = random ?? Random.Shared;
        _source = (SAV5)sav;
        var editedBeforeClone = _source.State.Edited;
        _sav = (SAV5)_source.Clone();
        _source.State.Edited = editedBeforeClone;
        _b2w2 = _sav as SAV5B2W2;
        _bw = _sav as SAV5BW;
        _entralink = _sav.Entralink;
        _forest = _sav.EntreeForest;
        _festa = _b2w2?.Festa;
        _originalEventFlags = _b2w2 is null ? [] : Enumerable.Range(0, _sav.EventWork.EventFlagCount)
            .Select(_sav.EventWork.GetEventFlag).ToArray();

        IsB2W2 = _b2w2 is not null;
        IsBW = _bw is not null;

        // Initialize Lists
        var ppValues = Enum.GetValues<PassPower5>();
        var ppNames = Enum.GetNames<PassPower5>(); // Fallback to enum names if no translation, or implement translation similarly to WinForms if needed.
        PassPowers = IsB2W2 ? ppValues.Cast<PassPower5>().Select((v, i) => new ComboItem(ppNames[i], (int)v)).ToList() : [];

        // Species
        var species = GameInfo.Strings.Species;
        SpeciesList = Enumerable.Range(0, species.Count).Select(i => new ComboItem(species[i], i)).ToList();
        
        // Moves
        var moves = GameInfo.Strings.Move;
        MoveList = Enumerable.Range(0, moves.Count).Select(i => new ComboItem(moves[i], i)).ToList();
        
        LoadData();
    }

    public bool IsB2W2 { get; }
    public bool IsBW { get; }

    public IReadOnlyList<ComboItem> PassPowers { get; }
    public IReadOnlyList<ComboItem> SpeciesList { get; }
    public IReadOnlyList<ComboItem> MoveList { get; }
    public IReadOnlyList<ComboItem> GenderList { get; } = [
        new ComboItem("Male", 0),
        new ComboItem("Female", 1),
        new ComboItem("Genderless", 2)
    ];
    
    // Entralink Levels
    [ObservableProperty] private int _whiteLevel;
    [ObservableProperty] private int _blackLevel;
    
    partial void OnWhiteLevelChanged(int value) => _entralink.WhiteForestLevel = (ushort)value;
    partial void OnBlackLevelChanged(int value) => _entralink.BlackCityLevel = (ushort)value;

    // Pass Powers (B2W2)
    [ObservableProperty] private int _passPower1;
    [ObservableProperty] private int _passPower2;
    [ObservableProperty] private int _passPower3;

    partial void OnPassPower1Changed(int value)
    {
        if (_b2w2 != null) ((Entralink5B2W2)_entralink).PassPower1 = (byte)value;
    }
    partial void OnPassPower2Changed(int value)
    {
        if (_b2w2 != null) ((Entralink5B2W2)_entralink).PassPower2 = (byte)value;
    }
    partial void OnPassPower3Changed(int value)
    {
        if (_b2w2 != null) ((Entralink5B2W2)_entralink).PassPower3 = (byte)value;
    }

    // Entralink missions completed (Black/White only; B2W2 has Pass Powers and the Funfest block instead)
    [ObservableProperty] private int _missionsComplete;
    partial void OnMissionsCompleteChanged(int value)
    {
        if (_entralink is Entralink5BW bw) bw.MissionsComplete = (byte)Math.Clamp(value, 0, byte.MaxValue);
    }

    // Festa Missions (B2W2)
    [ObservableProperty] private int _festaHosted;
    [ObservableProperty] private int _festaParticipated;
    [ObservableProperty] private int _festaCompleted;
    [ObservableProperty] private int _festaScore;
    [ObservableProperty] private int _festaMostParticipants;

    partial void OnFestaHostedChanged(int value) { if (_festa != null) _festa.Hosted = (ushort)value; }
    partial void OnFestaParticipatedChanged(int value) { if (_festa != null) _festa.Participated = (ushort)value; }
    partial void OnFestaCompletedChanged(int value) { if (_festa != null) _festa.Completed = (ushort)value; }
    partial void OnFestaScoreChanged(int value) { if (_festa != null) _festa.TopScores = (ushort)value; }
    partial void OnFestaMostParticipantsChanged(int value) { if (_festa != null) _festa.Participants = (byte)Math.Clamp(value, 0, byte.MaxValue); }

    public ObservableCollection<FunfestMissionRow> Missions { get; } = [];
    public ObservableCollection<FunfestMissionRow> FilteredMissions { get; } = [];
    [ObservableProperty] private FunfestMissionRow? _selectedMission;
    [ObservableProperty] private string _missionFilter = string.Empty;

    partial void OnMissionFilterChanged(string value) => RefreshMissionFilter();
    partial void OnSelectedMissionChanged(FunfestMissionRow? value) => UnlockSelectedMissionCommand.NotifyCanExecuteChanged();

    private bool CanUnlockSelectedMission => SelectedMission is { IsUnlocked: false };

    private void RefreshMissionFilter()
    {
        FilteredMissions.Clear();
        foreach (var mission in Missions)
            if (string.IsNullOrWhiteSpace(MissionFilter)
                || mission.Name.Contains(MissionFilter, StringComparison.CurrentCultureIgnoreCase))
                FilteredMissions.Add(mission);
        if (SelectedMission is null || !FilteredMissions.Contains(SelectedMission))
            SelectedMission = FilteredMissions.FirstOrDefault();
    }

    private void LoadMissions()
    {
        if (_festa is null) return;
        Missions.Clear();
        for (var index = 0; index <= FestaBlock5.MaxMissionIndex; index++)
        {
            var id = index;
            var mission = (Funfest5Mission)index;
            var name = LocalizedStrings.Instance[$"Funfest5Mission_{mission}"];
            Missions.Add(new FunfestMissionRow(id, name, _festa.GetMissionRecord(index),
                _festa.IsFunfestMissionUnlocked(index), score => _festa.SetMissionRecord(id, score)));
        }
        RefreshMissionFilter();
    }

    [RelayCommand(CanExecute = nameof(CanUnlockSelectedMission))]
    private void UnlockSelectedMission()
    {
        if (_festa is null || SelectedMission is null) return;
        _festa.UnlockFunfestMission(SelectedMission.Index);
        RefreshMissionLocks();
    }

    private void RefreshMissionLocks()
    {
        if (_festa is null) return;
        foreach (var mission in Missions)
            mission.IsUnlocked = _festa.IsFunfestMissionUnlocked(mission.Index);
        UnlockSelectedMissionCommand.NotifyCanExecuteChanged();
    }


    // Forest
    [ObservableProperty] private ObservableCollection<EntreeAreaViewModel> _areas = [];
    [ObservableProperty] private EntreeAreaViewModel? _selectedArea;
    [ObservableProperty] private EntreeSlotViewModel? _selectedEntreeSlot;

    partial void OnSelectedAreaChanged(EntreeAreaViewModel? value) =>
        SelectedEntreeSlot = value?.Slots.FirstOrDefault();
    
    [ObservableProperty] private int _unlockedAreas;
    partial void OnUnlockedAreasChanged(int value) => _forest.Unlock38Areas = value; // 0-6 maps to Areas 3-8

    [ObservableProperty] private bool _unlock9thArea;
    partial void OnUnlock9thAreaChanged(bool value) => _forest.Unlock9thArea = value;

    public void LoadData()
    {
        WhiteLevel = _entralink.WhiteForestLevel;
        BlackLevel = _entralink.BlackCityLevel;
        if (_entralink is Entralink5BW bw)
            MissionsComplete = bw.MissionsComplete;

        if (_b2w2 != null)
        {
            var el = (Entralink5B2W2)_entralink;
            PassPower1 = el.PassPower1;
            PassPower2 = el.PassPower2;
            PassPower3 = el.PassPower3;

            if (_festa != null)
            {
                FestaHosted = _festa.Hosted;
                FestaParticipated = _festa.Participated;
                FestaCompleted = _festa.Completed;
                FestaScore = _festa.TopScores;
                FestaMostParticipants = _festa.Participants;
                LoadMissions();
            }
        }

        LoadForest();
        UnlockedAreas = _forest.Unlock38Areas;
        Unlock9thArea = _forest.Unlock9thArea;
    }

    private void LoadForest()
    {
        _forest.StartAccess();
        var slots = _forest.Slots;
        
        Areas.Clear();
        var areaGroups = slots.GroupBy(s => s.Area & ~(EntreeForestArea.Center | EntreeForestArea.Left | EntreeForestArea.Right));
        
        foreach (var group in areaGroups)
        {
            var name = GetAreaName(group.Key);
            var vm = new EntreeAreaViewModel(name, group.Select(s => new EntreeSlotViewModel(s, _spriteRenderer)).ToList());
            Areas.Add(vm);
        }

        if (Areas.Count > 0)
            SelectedArea = Areas[0];
    }
    
    private string GetAreaName(EntreeForestArea area)
    {
        if (area.HasFlag(EntreeForestArea.Deepest)) return "Deepest Clearing";
        if (area.HasFlag(EntreeForestArea.Ninth)) return "Area 9 (Sky)";
        if (area.HasFlag(EntreeForestArea.First)) return "Area 1";
        if (area.HasFlag(EntreeForestArea.Second)) return "Area 2";
        if (area.HasFlag(EntreeForestArea.Third)) return "Area 3";
        if (area.HasFlag(EntreeForestArea.Fourth)) return "Area 4";
        if (area.HasFlag(EntreeForestArea.Fifth)) return "Area 5";
        if (area.HasFlag(EntreeForestArea.Sixth)) return "Area 6";
        if (area.HasFlag(EntreeForestArea.Seventh)) return "Area 7";
        if (area.HasFlag(EntreeForestArea.Eighth)) return "Area 8";
        return "Unknown Area";
    }

    [RelayCommand]
    private void UnlockAllMissions()
    {
        if (_festa is null) return;
        // Core's bulk helper excludes the last mission; call the public per-mission API for
        // the full inclusive range without changing the mirrored Core source.
        for (var index = 0; index <= FestaBlock5.MaxMissionIndex; index++)
            _festa.UnlockFunfestMission(index);
        RefreshMissionLocks();
    }

    [RelayCommand]
    private void Save()
    {
        // Slots are decrypted for editing. Re-encrypt the staged forest before committing any
        // bytes so merely opening and saving this dialog cannot corrupt the forest block.
        _forest.EndAccess();
        var changed = CopyChangedBlock(_entralink.Data, _source.Entralink.Data);
        changed |= CopyChangedBlock(_forest.Data, _source.EntreeForest.Data);
        if (_festa is not null && _source is SAV5B2W2 b2w2)
        {
            changed |= CopyChangedBlock(_festa.Data, b2w2.Festa.Data);
            // Funfest unlocks also change EventWork flags. Apply only staged bit changes,
            // preserving unrelated flags and work values that changed in the live save.
            for (var flag = 0; flag < _originalEventFlags.Length; flag++)
            {
                var staged = _sav.EventWork.GetEventFlag(flag);
                if (staged == _originalEventFlags[flag] || staged == _source.EventWork.GetEventFlag(flag))
                    continue;
                _source.EventWork.SetEventFlag(flag, staged);
                changed = true;
            }
        }
        if (changed)
            _source.State.Edited = true;
        CloseRequested?.Invoke();
    }

    private static bool CopyChangedBlock(ReadOnlySpan<byte> staged, Span<byte> target)
    {
        if (staged.SequenceEqual(target)) return false;
        staged.CopyTo(target);
        return true;
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();
    
    [RelayCommand]
    private void UnlockAllAreasCmd()
    {
        _forest.UnlockAllAreas();
        UnlockedAreas = _forest.Unlock38Areas;
        Unlock9thArea = _forest.Unlock9thArea;
    }

    /// <summary>
    /// Empties every Entrée Forest slot. Like every edit here it lands on the staged clone, so
    /// Cancel discards it.
    /// </summary>
    [RelayCommand]
    private void DeleteAllSlots()
    {
        _forest.DeleteAll();
        LoadForest();
    }

    private bool CanRandomizeForest => _dialogService is not null;
    private bool CanUndoRandomizeForest => _beforeRandomize is not null;

    [RelayCommand(CanExecute = nameof(CanRandomizeForest))]
    private async Task RandomizeForestAsync()
    {
        if (_dialogService is null) return;
        var confirmed = await _dialogService.ShowConfirmationAsync(
            LocalizedStrings.Instance["EntralinkEditor_RandomizeTitle"],
            LocalizedStrings.Instance["EntralinkEditor_RandomizeConfirm"],
            LocalizedStrings.Instance["Common_OK"],
            LocalizedStrings.Instance["Common_Cancel"]);
        if (!confirmed) return;

        var source = (_sav is SAV5BW ? Encounters5BW.DreamWorld_BW : Encounters5B2W2.DreamWorld_B2W2)
            .Concat(Encounters5DR.DreamWorld_Common).ToArray();
        if (source.Length == 0) return;

        _beforeRandomize = _forest.Data.ToArray();
        var remaining = source.ToList();
        foreach (var slot in _forest.Slots)
        {
            if (remaining.Count == 0)
                remaining.AddRange(source);
            var index = _random.Next(remaining.Count);
            var encounter = remaining[index];
            remaining.RemoveAt(index);
            slot.Species = encounter.Species;
            slot.Form = encounter.Form;
            slot.Gender = !((IFixedGender)encounter).IsFixedGender
                ? PersonalTable.B2W2[encounter.Species].RandomGender()
                : encounter.Gender;
            ReadOnlySpan<ushort> candidateMoves = encounter.Moves;
            var moves = candidateMoves.ToArray().Where(move => move != 0).ToArray();
            slot.Move = moves.Length == 0 ? (ushort)0 : moves[_random.Next(moves.Length)];
        }
        _forest.UnlockAllAreas();
        UnlockedAreas = _forest.Unlock38Areas;
        Unlock9thArea = _forest.Unlock9thArea;
        LoadForest();
        UndoRandomizeForestCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanUndoRandomizeForest))]
    private void UndoRandomizeForest()
    {
        if (_beforeRandomize is null) return;
        _beforeRandomize.CopyTo(_forest.Data);
        _beforeRandomize = null;
        UnlockedAreas = _forest.Unlock38Areas;
        Unlock9thArea = _forest.Unlock9thArea;
        LoadForest();
        UndoRandomizeForestCommand.NotifyCanExecuteChanged();
    }
}

public partial class EntreeAreaViewModel : ObservableObject
{
    public string Name { get; }
    public ObservableCollection<EntreeSlotViewModel> Slots { get; }

    public EntreeAreaViewModel(string name, IEnumerable<EntreeSlotViewModel> slots)
    {
        Name = name;
        Slots = new ObservableCollection<EntreeSlotViewModel>(slots);
    }
}

public partial class EntreeSlotViewModel : ViewModelBase
{
    private readonly EntreeSlot _slot;
    private readonly ISpriteRenderer? _spriteRenderer;

    public EntreeSlotViewModel(EntreeSlot slot, ISpriteRenderer? spriteRenderer = null)
    {
        _slot = slot;
        _spriteRenderer = spriteRenderer;
        // Loading a row must not invoke Core setters, which can normalize reserved bits.
        _species = _slot.Species;
        _move = _slot.Move;
        _gender = _slot.Gender;
        _form = _slot.Form;
        _animation = (int)_slot.Animation;
    }

    public string SlotPosition => GetPositionName(_slot.Area);

    private string GetPositionName(EntreeForestArea area)
    {
        if (area.HasFlag(EntreeForestArea.Left)) return "Left";
        if (area.HasFlag(EntreeForestArea.Right)) return "Right";
        if (area.HasFlag(EntreeForestArea.Center)) return "Center";
        return "Center";
    }

    // ComboItem.Value is an int; matching it keeps Avalonia's SelectedValue lookup exact.
    [ObservableProperty] private int _species;
    partial void OnSpeciesChanged(int value)
    {
        _slot.Species = (ushort)value;
        OnPropertyChanged(nameof(SpeciesName));
        OnPropertyChanged(nameof(Sprite));
    }
    
    public string SpeciesName => GameInfo.Strings.Species[Species];
    public string MoveName => Move < GameInfo.Strings.Move.Count ? GameInfo.Strings.Move[Move] : Move.ToString();
    public string GenderSymbol => Gender switch { 0 => "♂", 1 => "♀", _ => "–" };
    public string AnimationName => LocalizedStrings.Instance[$"EntralinkEditor_Animation{Animation}"];
    public byte[]? Sprite => Species == 0 ? null :
        _spriteRenderer?.GetSprite((ushort)Species, (byte)Math.Clamp(Form, 0, byte.MaxValue),
            (byte)Math.Clamp(Gender, 0, byte.MaxValue), 0, false, EntityContext.Gen5);

    [ObservableProperty] private int _move;
    partial void OnMoveChanged(int value)
    {
        _slot.Move = (ushort)value;
        OnPropertyChanged(nameof(MoveName));
    }

    [ObservableProperty] private int _gender;
    partial void OnGenderChanged(int value)
    {
        _slot.Gender = (byte)value;
        OnPropertyChanged(nameof(GenderSymbol));
        OnPropertyChanged(nameof(Sprite));
    }

    [ObservableProperty] private int _form;
    partial void OnFormChanged(int value)
    {
        _slot.Form = (byte)value;
        OnPropertyChanged(nameof(Sprite));
    }
    
    [ObservableProperty] private int _animation;
    partial void OnAnimationChanged(int value)
    {
        _slot.Animation = (EntreeForestAnimation)value;
        OnPropertyChanged(nameof(AnimationName));
    }
}

public partial class FunfestMissionRow : ObservableObject
{
    private readonly Action<Funfest5Score> _onChanged;
    private Funfest5Score _record;

    public int Index { get; }
    public string Name { get; }
    public string StatusText => LocalizedStrings.Instance[IsUnlocked ? "EntralinkEditor_MissionUnlocked" : "EntralinkEditor_MissionLocked"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isUnlocked;
    [ObservableProperty] private int _bestTotal;
    [ObservableProperty] private int _bestScore;
    [ObservableProperty] private int _level;
    [ObservableProperty] private bool _isNew;

    public FunfestMissionRow(int index, string name, Funfest5Score record, bool unlocked, Action<Funfest5Score> onChanged)
    {
        Index = index;
        Name = name;
        _record = record;
        _onChanged = onChanged;
        _isUnlocked = unlocked;
        _bestTotal = record.Total;
        _bestScore = record.Score;
        _level = record.Level;
        _isNew = record.IsNew;
    }

    partial void OnBestTotalChanged(int value)
    {
        _record.Total = Math.Clamp(value, 0, 0x3FFF);
        _onChanged(_record);
    }

    partial void OnBestScoreChanged(int value)
    {
        _record.Score = Math.Clamp(value, 0, 0x3FFF);
        _onChanged(_record);
    }

    partial void OnLevelChanged(int value)
    {
        _record.Level = Math.Clamp(value, 0, 7);
        _onChanged(_record);
    }

    partial void OnIsNewChanged(bool value)
    {
        _record.IsNew = value;
        _onChanged(_record);
    }
}
