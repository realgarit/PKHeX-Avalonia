using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Avalonia.Models;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using System;
using System.Collections.ObjectModel;

namespace PKHeX.Avalonia.ViewModels;

public partial class PartyViewerViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly ISpriteRenderer _spriteRenderer;
    private readonly ISlotService? _slotService;

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private ObservableCollection<PartySlotData> _slots = [];

    public event Action<int>? SlotActivated;
    public event Action<int>? ViewSlotRequested;
    public event Action<int>? SetSlotRequested;

    public PartyViewerViewModel(SaveFile sav, ISpriteRenderer spriteRenderer, ISlotService? slotService = null)
    {
        _sav = sav;
        _spriteRenderer = spriteRenderer;
        _slotService = slotService;
        LoadParty();
    }

    partial void OnSelectedIndexChanged(int value)
    {
        // Update IsSelected on all slots
        for (int i = 0; i < Slots.Count; i++)
            Slots[i].IsSelected = i == value;
    }

    private void LoadParty()
    {
        var previousIndex = SelectedIndex;
        Slots.Clear();
        
        var strings = GameInfo.Strings;
        var partyCount = _sav.PartyCount;
        
        for (int i = 0; i < 6; i++)
        {
            var pk = _sav.GetPartySlotAtIndex(i);
            var isEmpty = pk.Species == 0 || i >= partyCount;
            
            Slots.Add(new PartySlotData
            {
                Slot = i,
                Species = pk.Species,
                Sprite = _spriteRenderer.GetSprite(pk),
                IsEmpty = isEmpty,
                IsShiny = pk.IsShiny,
                Nickname = isEmpty ? string.Empty : pk.Nickname,
                SpeciesName = isEmpty ? string.Empty : strings.Species[pk.Species],
                Level = pk.CurrentLevel,
                Gender = (byte)pk.Gender,
                HeldItem = (ushort)pk.HeldItem,
                HeldItemName = pk.HeldItem > 0 ? strings.Item[pk.HeldItem] : string.Empty,
                IsEgg = pk.IsEgg,
                CurrentHp = (ushort)(pk is PKM pkm ? pkm.Stat_HPCurrent : 0),
                MaxHp = (ushort)(pk is PKM pkm2 ? pkm2.Stat_HPMax : 0),
                IsSelected = false
            });
        }
        
        // Restore selection position (clamped to valid range)
        SelectedIndex = Math.Clamp(previousIndex, 0, Slots.Count - 1);
    }

    [RelayCommand]
    private void SelectSlotByClick(PartySlotData? slot)
    {
        if (slot is null)
            return;

        SelectedIndex = slot.Slot;
    }

    [RelayCommand]
    private void ActivateSlot()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Slots.Count)
            return;

        var slot = Slots[SelectedIndex];
        SlotActivated?.Invoke(slot.Slot);
    }

    [RelayCommand]
    private void MoveSelection(string direction)
    {
        if (Slots.Count == 0) return;

        int newIndex = direction switch
        {
            "Up" => SelectedIndex > 0 ? SelectedIndex - 1 : SelectedIndex,
            "Down" => SelectedIndex < Slots.Count - 1 ? SelectedIndex + 1 : SelectedIndex,
            _ => SelectedIndex
        };

        SelectedIndex = newIndex;
    }

    public void RefreshParty() => LoadParty();
    
    [RelayCommand]
    private void ViewSlot(PartySlotData? slot)
    {
        if (slot is null || slot.IsEmpty)
            return;
        
        if (_slotService is not null)
            _slotService.RequestView(SlotLocation.FromParty(slot.Slot));
        else
            ViewSlotRequested?.Invoke(slot.Slot);
    }
    
    [RelayCommand]
    private void SetSlot(PartySlotData? slot)
    {
        if (slot is null)
            return;
        
        if (_slotService is not null)
            _slotService.RequestSet(SlotLocation.FromParty(slot.Slot));
        else
            SetSlotRequested?.Invoke(slot.Slot);
    }
    
    /// <summary>
    /// Gets the PKM at the specified slot.
    /// </summary>
    public PKM GetSlotPKM(int slot) => _sav.GetPartySlotAtIndex(slot);
}
