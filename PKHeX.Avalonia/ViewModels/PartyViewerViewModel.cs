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

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private ObservableCollection<PartySlotData> _slots = [];

    public event Action<int>? SlotActivated;

    public PartyViewerViewModel(SaveFile sav, ISpriteRenderer spriteRenderer)
    {
        _sav = sav;
        _spriteRenderer = spriteRenderer;
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
        
        for (int i = 0; i < 6; i++)
        {
            var pk = _sav.GetPartySlotAtIndex(i);
            Slots.Add(new PartySlotData
            {
                Slot = i,
                Species = pk.Species,
                Sprite = _spriteRenderer.GetSprite(pk),
                IsEmpty = pk.Species == 0,
                IsShiny = pk.IsShiny,
                Nickname = pk.Nickname,
                Level = pk.CurrentLevel,
                SpeciesName = GameInfo.Strings.Species[pk.Species],
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
}
