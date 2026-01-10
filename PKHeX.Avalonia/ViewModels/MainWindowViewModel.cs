using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ISaveFileService _saveFileService;
    private readonly IDialogService _dialogService;
    private readonly ISpriteRenderer _spriteRenderer;
    private readonly ISlotService _slotService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSave))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyCanExecuteChangedFor(nameof(SaveFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveFileAsCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseFileCommand))]
    private SaveFile? _currentSave;

    [ObservableProperty]
    private BoxViewerViewModel? _boxViewer;

    [ObservableProperty]
    private PartyViewerViewModel? _partyViewer;

    public bool HasSave => CurrentSave is not null;

    public string WindowTitle => CurrentSave is not null
        ? $"PKHeX Avalonia - {CurrentSave.Version}"
        : "PKHeX Avalonia";

    public MainWindowViewModel(
        ISaveFileService saveFileService,
        IDialogService dialogService,
        ISpriteRenderer spriteRenderer,
        ISlotService slotService)
    {
        _saveFileService = saveFileService;
        _dialogService = dialogService;
        _spriteRenderer = spriteRenderer;
        _slotService = slotService;

        _saveFileService.SaveFileChanged += OnSaveFileChanged;
        _slotService.ViewRequested += OnViewRequested;
        _slotService.SetRequested += OnSetRequested;
        _slotService.DeleteRequested += OnDeleteRequested;
        _slotService.MoveRequested += OnMoveRequested;
    }

    [ObservableProperty]
    private PokemonEditorViewModel? _currentPokemonEditor;

    private void OnSaveFileChanged(SaveFile? sav)
    {
        CurrentSave = sav;
        if (sav is not null)
        {
            _spriteRenderer.Initialize(sav);
            
            // Initialize PKHeX Core data filters for the current save
            GameInfo.FilteredSources = new FilteredGameDataSource(sav, GameInfo.Sources);
            
            // Initialize Editor with a blank PKM (or first slot?)
            var blank = sav.BlankPKM;
            CurrentPokemonEditor = new PokemonEditorViewModel(blank, sav, _spriteRenderer, _dialogService);
            
            var boxViewer = new BoxViewerViewModel(sav, _spriteRenderer, _slotService);
            boxViewer.SlotActivated += OnBoxSlotActivated;
            boxViewer.ViewSlotRequested += OnBoxViewSlot;
            boxViewer.SetSlotRequested += OnBoxSetSlot;
            boxViewer.DeleteSlotRequested += OnBoxDeleteSlot;
            BoxViewer = boxViewer;
            
            var partyViewer = new PartyViewerViewModel(sav, _spriteRenderer, _slotService);
            partyViewer.SlotActivated += OnPartySlotActivated;
            partyViewer.ViewSlotRequested += OnPartyViewSlot;
            partyViewer.SetSlotRequested += OnPartySetSlot;
            PartyViewer = partyViewer;
        }
        else
        {
            CurrentPokemonEditor = null;
            
            if (BoxViewer is not null)
            {
                BoxViewer.SlotActivated -= OnBoxSlotActivated;
                BoxViewer.ViewSlotRequested -= OnBoxViewSlot;
                BoxViewer.SetSlotRequested -= OnBoxSetSlot;
                BoxViewer.DeleteSlotRequested -= OnBoxDeleteSlot;
            }
            BoxViewer = null;
            
            if (PartyViewer is not null)
            {
                PartyViewer.SlotActivated -= OnPartySlotActivated;
                PartyViewer.ViewSlotRequested -= OnPartyViewSlot;
                PartyViewer.SetSlotRequested -= OnPartySetSlot;
            }
            PartyViewer = null;
        }
    }

    private void OnBoxSlotActivated(int box, int slot)
    {
        // Double click = View
        OnBoxViewSlot(box, slot);
    }

    private void OnPartySlotActivated(int slot)
    {
        // Double click = View
        OnPartyViewSlot(slot);
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var path = await _dialogService.OpenFileAsync(
            "Open Save File",
            ["*.sav", "*.bin", "main", "*"]);

        if (string.IsNullOrEmpty(path))
            return;

        // Debug: Show path in title temporarily
        var fileExists = System.IO.File.Exists(path);
        var fileSize = fileExists ? new System.IO.FileInfo(path).Length : 0;

        var success = await _saveFileService.LoadSaveFileAsync(path);
        if (!success)
        {
            await _dialogService.ShowErrorAsync("Error",
                $"Failed to load save file.\n\nPath: {path}\nExists: {fileExists}\nSize: {fileSize} bytes");
        }
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task SaveFileAsync()
    {
        var success = await _saveFileService.SaveFileAsync();
        if (!success)
        {
            await _dialogService.ShowErrorAsync("Error", "Failed to save file.");
        }
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task SaveFileAsAsync()
    {
        var path = await _dialogService.SaveFileAsync(
            "Save As",
            CurrentSave?.Metadata.FileName);

        if (string.IsNullOrEmpty(path))
            return;

        var success = await _saveFileService.SaveFileAsync(path);
        if (!success)
        {
            await _dialogService.ShowErrorAsync("Error", "Failed to save file.");
        }
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private void CloseFile()
    {
        _saveFileService.CloseSave();
    }
    
    // Slot Service event handlers
    private void OnViewRequested(SlotLocation location)
    {
        if (location.IsParty)
            OnPartyViewSlot(location.Slot);
        else
            OnBoxViewSlot(location.Box, location.Slot);
    }
    
    private void OnSetRequested(SlotLocation location)
    {
        if (location.IsParty)
            OnPartySetSlot(location.Slot);
        else
            OnBoxSetSlot(location.Box, location.Slot);
    }
    
    private void OnDeleteRequested(SlotLocation location)
    {
        if (location.IsParty)
            OnPartyDeleteSlot(location.Slot);
        else
            OnBoxDeleteSlot(location.Box, location.Slot);
    }
    
    private void OnMoveRequested(SlotLocation source, SlotLocation destination, bool clone)
    {
        if (CurrentSave is null) return;

        var pkSource = source.IsParty
            ? CurrentSave.GetPartySlotAtIndex(source.Slot)
            : CurrentSave.GetBoxSlotAtIndex(source.Box, source.Slot);

        if (pkSource.Species == 0) return;

        var pkDest = destination.IsParty
            ? CurrentSave.GetPartySlotAtIndex(destination.Slot)
            : CurrentSave.GetBoxSlotAtIndex(destination.Box, destination.Slot);

        // Perform move/swap/clone
        if (clone)
        {
            if (destination.IsParty)
                CurrentSave.SetPartySlotAtIndex(pkSource.Clone(), destination.Slot);
            else
                CurrentSave.SetBoxSlotAtIndex(pkSource.Clone(), destination.Box, destination.Slot);
        }
        else
        {
            // Swap
            if (source.IsParty)
                CurrentSave.SetPartySlotAtIndex(pkDest, source.Slot);
            else
                CurrentSave.SetBoxSlotAtIndex(pkDest, source.Box, source.Slot);

            if (destination.IsParty)
                CurrentSave.SetPartySlotAtIndex(pkSource, destination.Slot);
            else
                CurrentSave.SetBoxSlotAtIndex(pkSource, destination.Box, destination.Slot);
        }

        // Refresh viewers
        BoxViewer?.RefreshCurrentBox();
        PartyViewer?.RefreshParty();
    }

    // Box slot action handlers
    private void OnBoxViewSlot(int box, int slot)
    {
        if (CurrentSave is null || BoxViewer is null || CurrentPokemonEditor is null)
            return;
        
        var pk = CurrentSave.GetBoxSlotAtIndex(box, slot);
        Console.WriteLine($"GetBoxSlotAtIndex({box}, {slot}): Species={pk.Species}, IV_HP={pk.IV_HP}, Level={pk.CurrentLevel}, OT={pk.OriginalTrainerName}");
        if (pk.Species == 0)
            return;

        // Load into Side Panel Editor
        CurrentPokemonEditor.LoadPKM(pk);
    }

    private void OnBoxSetSlot(int box, int slot)
    {
        if (CurrentSave is null || BoxViewer is null || CurrentPokemonEditor is null)
            return;
        
        // Get modified PKM from editor
        var pkm = CurrentPokemonEditor.PreparePKM();
        
        // Set to slot
        CurrentSave.SetBoxSlotAtIndex(pkm, box, slot);
        BoxViewer.RefreshCurrentBox();
    }
    
    private void OnBoxDeleteSlot(int box, int slot)
    {
        if (CurrentSave is null || BoxViewer is null)
            return;
        
        var pk = CurrentSave.GetBoxSlotAtIndex(box, slot);
        if (pk.Species == 0)
            return; // Already empty
        
        // Clear the slot
        CurrentSave.SetBoxSlotAtIndex(CurrentSave.BlankPKM, box, slot);
        BoxViewer.RefreshCurrentBox();
    }
    
    // Party slot action handlers
    private void OnPartyViewSlot(int slot)
    {
        if (CurrentSave is null || CurrentPokemonEditor is null)
            return;
        
        var pk = CurrentSave.GetPartySlotAtIndex(slot);
        if (pk.Species == 0)
            return;
        
        // Load into Side Panel Editor
        CurrentPokemonEditor.LoadPKM(pk);
    }
    
    private void OnPartySetSlot(int slot)
    {
        if (CurrentSave is null || PartyViewer is null || CurrentPokemonEditor is null)
            return;
        
        var pkm = CurrentPokemonEditor.PreparePKM();
        
        CurrentSave.SetPartySlotAtIndex(pkm, slot);
        PartyViewer.RefreshParty();
    }
    
    private void OnPartyDeleteSlot(int slot)
    {
        if (CurrentSave is null || PartyViewer is null)
            return;
        
        // Party slots can't be deleted in the middle - only if it's the last slot
        // For now, just show an error
        _ = _dialogService.ShowErrorAsync("Delete", "Cannot delete party Pokémon. Move to a box first.");
    }
}
