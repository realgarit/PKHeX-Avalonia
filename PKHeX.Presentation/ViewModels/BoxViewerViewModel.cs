using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Presentation.Models;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace PKHeX.Presentation.ViewModels;

public partial class BoxViewerViewModel : ViewModelBase, IBoxNavigator
{
    private readonly SaveFile _sav;
    private readonly ISpriteRenderer _spriteRenderer;
    private readonly ISlotService? _slotService;
    private readonly Guid _sessionId;
    private readonly IWindowService? _windowService;
    private readonly IDialogService? _dialogService;
    private readonly bool _haXMode;

    private const int Columns = 6;

    [ObservableProperty]
    private int _currentBox;

    [ObservableProperty]
    private string _boxName = string.Empty;

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private ObservableCollection<SlotData> _slots = [];

    /// <summary>
    /// The detached "search and seek" tool. Operated from a modeless window so it can
    /// drive this box viewer (jump + highlight) without crowding the box grid.
    /// </summary>
    public EntitySeekViewModel Seek { get; }

    public int BoxCount => _sav.BoxCount;
    public int SlotsPerBox => _sav.BoxSlotCount;
    public bool CanMoveToParty => _sav.HasParty;
    /// <summary>
    /// The immutable save-session identity captured when this viewer was created. A viewer from a
    /// previous save must keep its original token so its detached drag payloads cannot become valid
    /// again after the shared slot service starts a new session.
    /// </summary>
    public Guid SessionId => _sessionId;

    /// <summary>
    /// The slot under the current keyboard/selection cursor, used to drive
    /// keyboard-only Ctrl+C/Ctrl+V/Delete slot operations (no mouse required).
    /// </summary>
    public SlotData? SelectedSlot => SelectedIndex >= 0 && SelectedIndex < Slots.Count ? Slots[SelectedIndex] : null;

    // IBoxNavigator
    int IBoxNavigator.CurrentSlot => SelectedIndex;
    void IBoxNavigator.NavigateTo(int box, int slot)
    {
        if (!IsSessionCurrent())
            return;

        if (box != CurrentBox)
            LoadBox(box);
        SelectedIndex = slot;
    }

    public BoxViewerViewModel(SaveFile sav, ISpriteRenderer spriteRenderer, ISlotService? slotService = null, IWindowService? windowService = null, IDialogService? dialogService = null, bool haXMode = false)
    {
        _sav = sav;
        _spriteRenderer = spriteRenderer;
        _slotService = slotService;
        var sessionId = slotService?.SessionId ?? Guid.Empty;
        _sessionId = sessionId == Guid.Empty ? Guid.NewGuid() : sessionId;
        _windowService = windowService;
        _dialogService = dialogService;
        _haXMode = haXMode;
        Seek = new EntitySeekViewModel(sav, this);

        LoadBox(0);
    }

    private bool IsSessionCurrent() => _slotService is null
        || (_sessionId != Guid.Empty && _slotService.IsCurrentSession(_sessionId));

    /// <summary>Opens (or focuses) the modeless seek tool window.</summary>
    [RelayCommand]
    private void OpenSeekTool()
    {
        if (IsSessionCurrent())
            _windowService?.ShowTool(Seek, LocalizedStrings.Instance["BoxViewer_SearchSeekTitle"]);
    }

    /// <summary>Opens (or focuses) this Box viewer as a modeless workspace.</summary>
    [RelayCommand]
    private void OpenDetachedTool()
    {
        if (IsSessionCurrent())
            _windowService?.ShowTool(this, LocalizedStrings.Instance["Tab_Box"]);
    }

    partial void OnSelectedIndexChanged(int value)
    {
        for (int i = 0; i < Slots.Count; i++)
            Slots[i].IsSelected = i == value;
        OnPropertyChanged(nameof(SelectedSlot));
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
            var isEmpty = pk.Species == 0;

            // Use StringResourceLookup for all string-table accesses to safely
            // handle Gen 1/2 where Ability is -1 and some properties are placeholders.
            var slotData = new SlotData
            {
                Slot = slot,
                Box = box,
                Species = pk.Species,
                Sprite = _spriteRenderer.GetSprite(pk),
                IsEmpty = isEmpty,
                IsShiny = pk.IsShiny,
                Nickname = isEmpty ? string.Empty : pk.Nickname,
                SpeciesName = isEmpty ? string.Empty : StringResourceLookup.Species(pk.Species),
                Level = pk.CurrentLevel,
                Gender = (byte)pk.Gender,
                HeldItem = (ushort)pk.HeldItem,
                HeldItemName = pk.HeldItem > 0 ? StringResourceLookup.Item(pk.HeldItem) : string.Empty,
                IsEgg = pk.IsEgg,
                Form = pk.Form,
                Ability = (ushort)pk.Ability,
                AbilityName = StringResourceLookup.Ability(pk.Ability),
                Nature = (byte)pk.Nature,
                NatureName = StringResourceLookup.Nature((int)pk.Nature),
                ShowdownSummary = isEmpty ? string.Empty : new ShowdownSet(pk).Text,
                IsLegal = _haXMode || isEmpty || new LegalityAnalysis(pk).Valid,
                IsSelected = false,
            };

            Slots.Add(slotData);
        }

        // Restore selection position (clamped to valid range)
        SelectedIndex = Math.Clamp(previousIndex, 0, Math.Max(0, Slots.Count - 1));
        OnPropertyChanged(nameof(SelectedSlot));
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
        if (!IsSessionCurrent() || SelectedIndex < 0 || SelectedIndex >= Slots.Count)
            return;

        var slot = Slots[SelectedIndex];
        SlotActivated?.Invoke(CurrentBox, slot.Slot);
    }

    public void RefreshCurrentBox()
    {
        LoadBox(CurrentBox);
    }

    public event Action<int, int>? SlotActivated;
    public event Action<int, int>? ViewSlotRequested;
    public event Action<int, int>? SetSlotRequested;
    public event Action<int, int>? DeleteSlotRequested;
    
    [RelayCommand]
    private void ViewSlot(SlotData? slot)
    {
        if (slot is null || slot.IsEmpty || !IsSessionCurrent())
            return;
        
        if (_slotService is not null)
            _slotService.RequestView(_sessionId, SlotLocation.FromBox(CurrentBox, slot.Slot));
        else
            ViewSlotRequested?.Invoke(CurrentBox, slot.Slot);
    }
    
    [RelayCommand]
    private void SetSlot(SlotData? slot)
    {
        if (slot is null || !IsSessionCurrent())
            return;
        
        if (_slotService is not null)
            _slotService.RequestSet(_sessionId, SlotLocation.FromBox(CurrentBox, slot.Slot));
        else
            SetSlotRequested?.Invoke(CurrentBox, slot.Slot);
    }
    
    [RelayCommand]
    private void DeleteSlot(SlotData? slot)
    {
        if (slot is null || slot.IsEmpty || !IsSessionCurrent())
            return;
        
        if (_slotService is not null)
            _slotService.RequestDelete(_sessionId, SlotLocation.FromBox(CurrentBox, slot.Slot));
        else
            DeleteSlotRequested?.Invoke(CurrentBox, slot.Slot);
    }

    /// <summary>
    /// Moves a box Pokémon into the first empty Party slot. This provides a discoverable
    /// cross-tab operation because the Box and Party grids cannot be dragged simultaneously.
    /// </summary>
    [RelayCommand]
    private async Task MoveToParty(SlotData? slot)
    {
        if (slot is null || slot.IsEmpty || _slotService is null || !_sav.HasParty || !IsSessionCurrent())
            return;

        var destination = -1;
        for (var i = 0; i < 6; i++)
        {
            PKM partySlot;
            try
            {
                partySlot = _sav.GetPartySlotAtIndex(i);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            if (partySlot.Species == 0)
            {
                destination = i;
                break;
            }
        }

        if (destination < 0)
        {
            if (_dialogService is not null)
                await _dialogService.ShowErrorAsync(
                    LocalizedStrings.Instance["Slot_NoEmptyPartyTitle"],
                    LocalizedStrings.Instance["Slot_NoEmptyPartyMessage"]);
            return;
        }

        if (IsSessionCurrent())
            _slotService.RequestMove(_sessionId, SlotLocation.FromBox(CurrentBox, slot.Slot), SlotLocation.FromParty(destination), clone: false);
    }

    public PKM GetSlotPKM(int slot) => _sav.GetBoxSlotAtIndex(CurrentBox, slot);

    /// <summary>Creates a session-bound payload for a slot in the current box.</summary>
    public SlotDragData CreateDragData(int slot) => new(SlotLocation.FromBox(CurrentBox, slot), SessionId);

    public void SetSlotPKM(int slot, PKM pk)
    {
        if (!IsSessionCurrent())
            return;

        _sav.SetBoxSlotAtIndex(pk, CurrentBox, slot);
        RefreshCurrentBox();
    }

    public void ClearSlot(int slot)
    {
        if (!IsSessionCurrent())
            return;

        _sav.SetBoxSlotAtIndex(_sav.BlankPKM, CurrentBox, slot);
        RefreshCurrentBox();
    }

    [RelayCommand]
    private void RequestMove((SlotDragData data, SlotData dest, bool clone) param)
    {
        if (_slotService is null || !IsSessionCurrent() || param.data.SessionId != SessionId)
            return;

        _slotService.RequestMove(param.data.SessionId, param.data.Source, param.dest.Location, param.clone);
    }

    /// <summary>Raised when a dropped OS file turns out to be a save file, so the host can open it.</summary>
    public event Action<string>? SaveFileDropRequested;

    /// <summary>
    /// Handles one or more OS files dropped onto a box slot. A single file replaces the target
    /// slot (subject to format compatibility); multiple files are placed into the box's next
    /// empty slots in order. Reuses the same detection/conversion pipeline as folder import.
    /// </summary>
    public async Task HandleFileDropAsync(IReadOnlyList<string> paths, int targetSlot)
    {
        if (paths.Count == 0 || !IsSessionCurrent())
            return;

        if (paths.Count == 1)
        {
            var result = new ImportEntityFileUseCase().Execute(_sav, paths[0]);
            switch (result.Kind)
            {
                case EntityFileDropKind.SaveFile:
                    if (IsSessionCurrent())
                        SaveFileDropRequested?.Invoke(paths[0]);
                    return;
                case EntityFileDropKind.Entity:
                    if (_slotService is not null)
                        await _slotService.RequestReplaceAsync(_sessionId, SlotLocation.FromBox(CurrentBox, targetSlot), result.Entity!);
                    else
                        SetSlotPKM(targetSlot, result.Entity!);
                    return;
                default:
                    if (_dialogService is not null)
                        await _dialogService.ShowErrorAsync(LocalizedStrings.Instance["BoxViewer_ImportFailedTitle"], result.Message ?? LocalizedStrings.Instance["BoxViewer_ImportFailedDefault"]);
                    return;
            }
        }

        var batch = new BatchImportEntityFilesUseCase().Execute(_sav, CurrentBox, paths);
        RefreshCurrentBox();

        if (_dialogService is not null)
        {
            var message = LocalizedStrings.Instance.Format("BoxViewer_PlacedMessage", batch.Placed, paths.Count, CurrentBox + 1);
            if (batch.Skipped > 0)
                message += LocalizedStrings.Instance.Format("BoxViewer_SkippedMessage", batch.Skipped);
            await _dialogService.ShowInformationAsync(LocalizedStrings.Instance["BoxViewer_ImportFilesTitle"], message);
        }
    }
}
