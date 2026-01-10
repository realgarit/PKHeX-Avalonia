using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Avalonia.Models;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using System.Collections.ObjectModel;

namespace PKHeX.Avalonia.ViewModels;

public partial class BoxViewerViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly ISpriteRenderer _spriteRenderer;

    private const int Columns = 6;

    [ObservableProperty]
    private int _currentBox;

    [ObservableProperty]
    private string _boxName = string.Empty;

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private ObservableCollection<SlotData> _slots = [];

    public int BoxCount => _sav.BoxCount;
    public int SlotsPerBox => _sav.BoxSlotCount;

    public BoxViewerViewModel(SaveFile sav, ISpriteRenderer spriteRenderer)
    {
        _sav = sav;
        _spriteRenderer = spriteRenderer;

        LoadBox(0);
    }

    partial void OnSelectedIndexChanged(int value)
    {
        for (int i = 0; i < Slots.Count; i++)
            Slots[i].IsSelected = i == value;
    }

    private void LoadBox(int box)
    {
        if (box < 0 || box >= BoxCount)
            return;

        var previousIndex = SelectedIndex;
        CurrentBox = box;
        BoxName = _sav is IBoxDetailNameRead r
            ? r.GetBoxName(box)
            : BoxDetailNameExtensions.GetDefaultBoxName(box);

        Slots.Clear();

        var boxData = _sav.GetBoxData(box);
        for (int slot = 0; slot < boxData.Length; slot++)
        {
            var pk = boxData[slot];
            Slots.Add(new SlotData
            {
                Slot = slot,
                Box = box,
                Species = pk.Species,
                Sprite = _spriteRenderer.GetSprite(pk),
                IsEmpty = pk.Species == 0,
                IsShiny = pk.IsShiny,
                Nickname = pk.Nickname,
                IsSelected = false
            });
        }

        // Restore selection position (clamped to valid range)
        SelectedIndex = Math.Clamp(previousIndex, 0, Math.Max(0, Slots.Count - 1));
    }

    [RelayCommand]
    private void PreviousBox()
    {
        var newBox = CurrentBox - 1;
        if (newBox < 0)
            newBox = BoxCount - 1;
        LoadBox(newBox);
    }

    [RelayCommand]
    private void NextBox()
    {
        var newBox = CurrentBox + 1;
        if (newBox >= BoxCount)
            newBox = 0;
        LoadBox(newBox);
    }

    [RelayCommand]
    private void SelectSlotByClick(SlotData? slot)
    {
        if (slot is null)
            return;

        SelectedIndex = slot.Slot;
    }

    [RelayCommand]
    private void MoveSelection(string direction)
    {
        if (Slots.Count == 0) return;

        int newIndex = direction switch
        {
            "Left" => SelectedIndex > 0 ? SelectedIndex - 1 : SelectedIndex,
            "Right" => SelectedIndex < Slots.Count - 1 ? SelectedIndex + 1 : SelectedIndex,
            "Up" => SelectedIndex >= Columns ? SelectedIndex - Columns : SelectedIndex,
            "Down" => SelectedIndex + Columns < Slots.Count ? SelectedIndex + Columns : SelectedIndex,
            _ => SelectedIndex
        };

        SelectedIndex = newIndex;
    }

    [RelayCommand]
    private void SelectFirstSlot()
    {
        if (Slots.Count > 0)
            SelectedIndex = 0;
    }

    [RelayCommand]
    private void SelectLastSlot()
    {
        if (Slots.Count > 0)
            SelectedIndex = Slots.Count - 1;
    }

    [RelayCommand]
    private void ActivateSlot()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Slots.Count)
            return;

        var slot = Slots[SelectedIndex];
        SlotActivated?.Invoke(CurrentBox, slot.Slot);
    }

    public void RefreshCurrentBox()
    {
        LoadBox(CurrentBox);
    }

    public event Action<int, int>? SlotActivated;
}
