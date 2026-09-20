using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class DaycareEditorViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly ISpriteRenderer _spriteRenderer;
    private readonly IDaycareStorage? _storage;
    private readonly IDaycareEggState? _eggState;
    private readonly IDaycareExperience? _experience;
    private readonly IReadOnlyList<ReadOnlyDaycareSlot> _readOnlySlots;

    public DaycareEditorViewModel(SaveFile sav, ISpriteRenderer spriteRenderer)
    {
        _sav = sav;
        _spriteRenderer = spriteRenderer;
        
        _storage = sav as IDaycareStorage;
        _eggState = sav as IDaycareEggState;
        _experience = sav as IDaycareExperience;
        _readOnlySlots = GetReadOnlySlots(sav);

        LoadDaycareData();
    }

    public bool HasDaycare => _storage is not null || _readOnlySlots.Count > 0;
    public bool IsReadOnly => _storage is null && _readOnlySlots.Count > 0;
    public bool HasEggState => _eggState is not null;
    public bool HasExperience => _experience is not null;
    public int SlotCount => _storage?.DaycareSlotCount ?? _readOnlySlots.Count;

    [ObservableProperty]
    private ObservableCollection<DaycareSlotViewModel> _slots = [];

    [ObservableProperty]
    private bool _isEggAvailable;

    partial void OnIsEggAvailableChanged(bool value)
    {
        if (_eggState is not null)
            _eggState.IsEggAvailable = value;
    }

    private void LoadDaycareData()
    {
        Slots.Clear();

        if (_storage is null)
        {
            foreach (var slot in _readOnlySlots)
            {
                var pk = TryRead(slot.Data, _sav.Context);
                Slots.Add(new DaycareSlotViewModel(slot.Location, pk, slot.Occupied, 0, _spriteRenderer, isReadOnly: true));
            }
            return;
        }

        for (int i = 0; i < _storage.DaycareSlotCount; i++)
        {
            var mem = _storage.GetDaycareSlot(i);
            var pk = EntityFormat.GetFromBytes(mem.ToArray(), _sav.Context);
            var occupied = _storage.IsDaycareOccupied(i);
            var exp = _experience?.GetDaycareEXP(i) ?? 0;
            
            var vm = new DaycareSlotViewModel(
                LocalizedStrings.Instance.Format("DaycareEditorView_Slot", i + 1),
                pk, occupied, exp, _spriteRenderer, isReadOnly: false, index: i);
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DaycareSlotViewModel.IsOccupied))
                    _storage.SetDaycareOccupied(vm.Index, vm.IsOccupied);
                else if (e.PropertyName == nameof(DaycareSlotViewModel.Experience) && _experience is not null)
                    _experience.SetDaycareEXP(vm.Index, vm.Experience);
            };
            Slots.Add(vm);
        }

        if (_eggState is not null)
            IsEggAvailable = _eggState.IsEggAvailable;
    }

    [RelayCommand]
    private void RefreshDaycare()
    {
        LoadDaycareData();
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

    private sealed record ReadOnlyDaycareSlot(Memory<byte> Data, bool Occupied, string Location);
}

public partial class DaycareSlotViewModel : ViewModelBase
{
    private readonly ISpriteRenderer _spriteRenderer;

    public DaycareSlotViewModel(string location, PKM? pk, bool occupied, uint exp, ISpriteRenderer spriteRenderer, bool isReadOnly, int index = -1)
    {
        Index = index;
        Location = location;
        _pk = pk;
        _isOccupied = occupied;
        _experience = exp;
        _spriteRenderer = spriteRenderer;
        IsReadOnly = isReadOnly;
    }

    public int Index { get; }
    public string Location { get; }
    public bool IsReadOnly { get; }
    
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
