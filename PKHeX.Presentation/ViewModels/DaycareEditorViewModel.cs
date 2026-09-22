using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Application.Abstractions;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class DaycareEditorViewModel : ViewModelBase, ICloseableDialog
{
    private readonly SaveFile _source;
    private readonly SaveFile? _working;
    private readonly SaveFile _readSource;
    private readonly bool _hasWorkingCopy;
    private readonly ISpriteRenderer _spriteRenderer;
    private readonly IReadOnlyList<DaycareLocation> _locations;
    private readonly IDaycareEggState? _eggState;
    private readonly IDaycareExperience? _experience;
    private readonly IReadOnlyList<ReadOnlyDaycareSlot> _readOnlySlots;

    public Action? CloseRequested { get; set; }

    public DaycareEditorViewModel(SaveFile sav, ISpriteRenderer spriteRenderer)
    {
        _source = sav;
        try
        {
            _working = sav.Clone();
            _readSource = _working;
            _hasWorkingCopy = true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // Invalid synthetic saves remain inspectable, but never become writable.
            _working = null;
            _readSource = sav;
            _hasWorkingCopy = false;
        }
        _spriteRenderer = spriteRenderer;
        
        _locations = GetDaycareLocations(_readSource);
        _eggState = _readSource as IDaycareEggState;
        _experience = _readSource as IDaycareExperience;
        _readOnlySlots = GetReadOnlySlots(_readSource);

        LoadDaycareData();
    }

    public bool HasDaycare => _locations.Count > 0 || _readOnlySlots.Count > 0;
    public bool IsReadOnly => !_hasWorkingCopy || _locations.Count == 0 && _readOnlySlots.Count > 0;
    public bool HasEggState => _eggState is not null;
    public bool HasExperience => _experience is not null;
    public bool CanSave => _hasWorkingCopy && !IsReadOnly;
    public bool CanEditOccupancy => CanSave && _working is not SAV3 && _working is not SAV8BS;
    public int SlotCount => _locations.Count > 0 ? _locations.Sum(x => x.Storage.DaycareSlotCount) : _readOnlySlots.Count;

    [ObservableProperty]
    private ObservableCollection<DaycareSlotViewModel> _slots = [];

    [ObservableProperty]
    private bool _isEggAvailable;

    partial void OnIsEggAvailableChanged(bool value)
    {
        if (_eggState is not null && CanSave)
            _eggState.IsEggAvailable = value;
    }

    private void LoadDaycareData()
    {
        Slots.Clear();

        if (_locations.Count == 0)
        {
            foreach (var slot in _readOnlySlots)
            {
                var pk = TryRead(slot.Data, _readSource.Context);
                Slots.Add(new DaycareSlotViewModel(slot.Location, pk, slot.Occupied, 0, _spriteRenderer, isReadOnly: true, canEditOccupancy: false));
            }
            return;
        }

        var globalIndex = 0;
        for (var locationIndex = 0; locationIndex < _locations.Count; locationIndex++)
        {
            var storage = _locations[locationIndex].Storage;
            for (var localIndex = 0; localIndex < storage.DaycareSlotCount; localIndex++, globalIndex++)
            {
                var mem = storage.GetDaycareSlot(localIndex);
                var pk = TryRead(mem, _readSource.Context);
                var occupied = storage.IsDaycareOccupied(localIndex);
                var exp = ReadExperience(globalIndex);
                var locationText = _locations.Count == 1
                    ? LocalizedStrings.Instance.Format("DaycareEditorView_Slot", localIndex + 1)
                    : LocalizedStrings.Instance.Format("DaycareEditorView_NurserySlot", locationIndex + 1, localIndex + 1);

                var vm = new DaycareSlotViewModel(locationText, pk, occupied, exp, _spriteRenderer,
                    isReadOnly: IsReadOnly, canEditOccupancy: CanEditOccupancy, storage, localIndex, globalIndex);
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(DaycareSlotViewModel.IsOccupied) && CanEditOccupancy)
                        vm.Storage?.SetDaycareOccupied(vm.StorageIndex, vm.IsOccupied);
                    else if (e.PropertyName == nameof(DaycareSlotViewModel.Experience) && _experience is not null && CanSave)
                        _experience.SetDaycareEXP(vm.ExperienceIndex, vm.Experience);
                };
                Slots.Add(vm);
            }
        }

        if (_eggState is not null)
            IsEggAvailable = _eggState.IsEggAvailable;
    }

    [RelayCommand]
    private void RefreshDaycare()
    {
        LoadDaycareData();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (_working is not null)
            _source.CopyChangesFrom(_working);
        _source.State.Edited = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    private uint ReadExperience(int index)
    {
        try { return _experience?.GetDaycareEXP(index) ?? 0; }
        catch (ArgumentOutOfRangeException) { return 0; }
    }

    private static IReadOnlyList<DaycareLocation> GetDaycareLocations(SaveFile sav)
    {
        if (sav is IDaycareMulti multi)
        {
            var locations = new List<DaycareLocation>(multi.DaycareCount);
            for (var i = 0; i < multi.DaycareCount; i++)
                locations.Add(new DaycareLocation($"Nursery {i + 1}", multi[i]));
            return locations;
        }

        return sav is IDaycareStorage storage ? [new DaycareLocation("Daycare", storage)] : [];
    }

    private static IReadOnlyList<ReadOnlyDaycareSlot> GetReadOnlySlots(SaveFile sav)
    {
        if (sav is SAV7b lgpe)
        {
            var data = lgpe.Daycare.Stored;
            var pk = TryRead(data, sav.Context);
            return [new ReadOnlyDaycareSlot(data, pk?.Species > 0, LocalizedStrings.Instance["DaycareEditorView_StoredPokemon"] )];
        }

        if (sav is SAV8SWSH swsh)
        {
            var slots = new List<ReadOnlyDaycareSlot>(4);
            for (int daycare = 0; daycare < 2; daycare++)
            {
                for (int slot = 0; slot < 2; slot++)
                {
                    var index = (daycare * 2) + slot;
                    var data = swsh.Daycare[index].Slice(0, swsh.SIZE_STORED);
                    var pk = TryRead(data, sav.Context);
                    var location = LocalizedStrings.Instance.Format("DaycareEditorView_NurserySlot", daycare + 1, slot + 1);
                    slots.Add(new ReadOnlyDaycareSlot(data, pk?.Species > 0, location));
                }
            }
            return slots;
        }

        return [];
    }

    private static PKM? TryRead(Memory<byte> data, EntityContext context) => TryRead(data, context, out var pk) ? pk : null;

    private static bool TryRead(Memory<byte> data, EntityContext context, out PKM? pk)
    {
        try
        {
            pk = EntityFormat.GetFromBytes(data.ToArray(), context);
            return true;
        }
        catch (ArgumentException)
        {
            pk = null;
            return false;
        }
    }

    private sealed record DaycareLocation(string Name, IDaycareStorage Storage);
    private sealed record ReadOnlyDaycareSlot(Memory<byte> Data, bool Occupied, string Location);
}

public partial class DaycareSlotViewModel : ViewModelBase
{
    private readonly ISpriteRenderer _spriteRenderer;

    public DaycareSlotViewModel(string location, PKM? pk, bool occupied, uint exp, ISpriteRenderer spriteRenderer, bool isReadOnly, bool canEditOccupancy, IDaycareStorage? storage = null, int storageIndex = -1, int experienceIndex = -1)
    {
        Location = location;
        _pk = pk;
        _isOccupied = occupied;
        _experience = exp;
        _spriteRenderer = spriteRenderer;
        IsReadOnly = isReadOnly;
        CanEditOccupancy = canEditOccupancy;
        Storage = storage;
        StorageIndex = storageIndex;
        ExperienceIndex = experienceIndex;
    }

    public string Location { get; }
    public bool IsReadOnly { get; }
    public bool CanEditOccupancy { get; }
    public IDaycareStorage? Storage { get; }
    public int StorageIndex { get; }
    public int ExperienceIndex { get; }
    
    [ObservableProperty]
    private PKM? _pk;

    [ObservableProperty]
    private bool _isOccupied;

    [ObservableProperty]
    private uint _experience;

    public string Species => Pk?.Species > 0 ? StringResourceLookup.Species(Pk.Species) : LocalizedStrings.Instance["DaycareEditorView_Empty"];
    public string Level => Pk is not null && Pk.Species > 0 ? LocalizedStrings.Instance.Format("DaycareEditorView_Level", Pk.CurrentLevel) : "";
    public byte[]? Sprite => Pk is not null && Pk.Species > 0 ? _spriteRenderer.GetSprite(Pk) : null;
}
