using System.IO;
using System.Linq;
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
    private readonly IClipboardService _clipboardService;
    private readonly AppSettings _settings;
    private readonly UndoRedoService _undoRedo;
    private readonly LanguageService _languageService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSave))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyCanExecuteChangedFor(nameof(SaveFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveFileAsCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImportShowdownCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportShowdownCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenPKMDatabaseCommand))]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    [NotifyCanExecuteChangedFor(nameof(RedoCommand))]
    private SaveFile? _currentSave;

    [ObservableProperty]
    private BoxViewerViewModel? _boxViewer;

    [ObservableProperty]
    private PartyViewerViewModel? _partyViewer;

    [ObservableProperty]
    private TrainerEditorViewModel? _trainerEditor;

    [ObservableProperty]
    private InventoryEditorViewModel? _inventoryEditor;


    [ObservableProperty]
    private EventFlagsEditorViewModel? _eventFlagsEditor;

    [ObservableProperty]
    private MysteryGiftEditorViewModel? _mysteryGiftEditor;

    [ObservableProperty]
    private BatchEditorViewModel? _batchEditor;

    public bool HasSave => CurrentSave is not null;
    public bool CanUndo => _undoRedo.CanUndo;
    public bool CanRedo => _undoRedo.CanRedo;

    public string WindowTitle => CurrentSave is not null
        ? $"PKHeX Avalonia - {CurrentSave.Version}"
        : "PKHeX Avalonia";

    public LanguageService LanguageService => _languageService;

    public MainWindowViewModel(
        ISaveFileService saveFileService,
        IDialogService dialogService,
        ISpriteRenderer spriteRenderer,
        ISlotService slotService,
        IClipboardService clipboardService,
        AppSettings settings,
        UndoRedoService undoRedo,
        LanguageService languageService)
    {
        _saveFileService = saveFileService;
        _dialogService = dialogService;
        _spriteRenderer = spriteRenderer;
        _slotService = slotService;
        _clipboardService = clipboardService;
        _settings = settings;
        _undoRedo = undoRedo;
        _languageService = languageService;

        _saveFileService.SaveFileChanged += OnSaveFileChanged;
        _slotService.ViewRequested += OnViewRequested;
        _slotService.SetRequested += OnSetRequested;
        _slotService.DeleteRequested += OnDeleteRequested;
        _slotService.MoveRequested += OnMoveRequested;
        
        _undoRedo.PropertyChanged += (_, _) =>
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        };
        _undoRedo.UndoPerformed += OnUndoRedoPerformed;
        _undoRedo.RedoPerformed += OnUndoRedoPerformed;
    }

    [ObservableProperty]
    private PokemonEditorViewModel? _currentPokemonEditor;

    private void OnSaveFileChanged(SaveFile? sav)
    {
        CurrentSave = sav;
        if (sav is not null)
        {
            _spriteRenderer.Initialize(sav);
            _undoRedo.Initialize(sav);
            
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

            TrainerEditor = new TrainerEditorViewModel(sav);
            InventoryEditor = new InventoryEditorViewModel(sav);
            EventFlagsEditor = new EventFlagsEditorViewModel(sav);
            MysteryGiftEditor = new MysteryGiftEditorViewModel(sav, _dialogService);
            BatchEditor = new BatchEditorViewModel(sav, _dialogService);
            BatchEditor.BatchEditCompleted += OnBatchEditCompleted;
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

            TrainerEditor = null;
            InventoryEditor = null;
            EventFlagsEditor = null;
            MysteryGiftEditor = null;
            BatchEditor = null;
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

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task ImportShowdownAsync()
    {
        if (CurrentSave is null || CurrentPokemonEditor is null) return;

        var text = await _clipboardService.GetTextAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            await _dialogService.ShowErrorAsync("Import Failed", "Clipboard is empty.");
            return;
        }

        var set = new ShowdownSet(text);
        if (set.Species <= 0)
        {
            await _dialogService.ShowErrorAsync("Import Failed", "Invalid Showdown set text.");
            return;
        }

        // Logic adapted from WinForms ShowdownSet implementation
        // Create blank PKM of current context
        var pk = CurrentSave.BlankPKM;
        pk.ApplySetDetails(set);
        
        // Ensure Nature matches StatNature for Gen 8+ (identity nature should match import unless otherwise specified)
        if (pk.Format >= 8)
            pk.Nature = pk.StatNature;

        // Auto-legalize somewhat
        pk.SetPIDGender(pk.Gender);
        
        // Load into editor
        CurrentPokemonEditor.LoadPKM(pk);
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task ExportShowdownAsync()
    {
        if (CurrentPokemonEditor is null) return;
        
        // Prepare current state
        var pk = CurrentPokemonEditor.PreparePKM();
        if (pk.Species == 0) return;

        // Convert to Showdown format
        var set = new ShowdownSet(pk);
        var text = set.Text;
        
        await _clipboardService.SetTextAsync(text);
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenPKMDatabaseAsync()
    {
        if (CurrentSave is null) return;
        
        var vm = new PKMDatabaseViewModel(CurrentSave, _spriteRenderer, _dialogService);
        vm.PokemonSelected += (pk) =>
        {
            CurrentPokemonEditor?.LoadPKM(pk);
        };
        
        var view = new PKMDatabaseView { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "PKM Database");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenMysteryGiftDatabaseAsync()
    {
        if (CurrentSave is null) return;

        var vm = new MysteryGiftDatabaseViewModel(CurrentSave, _spriteRenderer, _dialogService);
        vm.GiftSelected += (mg) =>
        {
            var pk = mg.ConvertToPKM(CurrentSave);
            CurrentPokemonEditor?.LoadPKM(pk);
        };

        var view = new MysteryGiftDatabaseView { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Mystery Gift Database");
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
        _ = _dialogService.ShowErrorAsync("Delete", "Cannot delete party Pokémon. Move to a box first.");
    }

    private void OnBatchEditCompleted()
    {
        BoxViewer?.RefreshCurrentBox();
        PartyViewer?.RefreshParty();
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenBatchEditorAsync()
    {
        if (CurrentSave is null) return;
        
        var vm = new BatchEditorViewModel(CurrentSave, _dialogService);
        vm.BatchEditCompleted += OnBatchEditCompleted;
        
        var view = new Views.BatchEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Batch Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenBlockEditorAsync()
    {
        if (CurrentSave is null) return;
        
        var vm = new BlockEditorViewModel(CurrentSave, _dialogService);
        var view = new Views.BlockEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Block Editor");
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var vm = new SettingsViewModel(_settings);
        var view = new SettingsView { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Settings");
    }

    [RelayCommand]
    private async Task OpenAboutAsync()
    {
        var view = new Views.AboutView();
        await _dialogService.ShowDialogAsync(view, "About PKHeX");
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        _undoRedo.Undo();
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        _undoRedo.Redo();
    }

    private void OnUndoRedoPerformed(ISlotInfo info)
    {
        // Refresh the appropriate viewer based on slot type
        if (info is SlotInfoBox)
            BoxViewer?.RefreshCurrentBox();
        else if (info is SlotInfoParty)
            PartyViewer?.RefreshParty();
    }

    [RelayCommand]
    private void ChangeLanguage(string languageCode)
    {
        _languageService.SetLanguage(languageCode);
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenBoxManipAsync()
    {
        if (CurrentSave is null) return;
        
        var vm = new BoxManipViewModel(CurrentSave, _dialogService, () =>
        {
            BoxViewer?.RefreshCurrentBox();
        });
        var view = new Views.BoxManipView { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Box Manipulation");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenEncounterDatabaseAsync()
    {
        if (CurrentSave is null) return;
        
        var vm = new EncounterDatabaseViewModel(CurrentSave, _dialogService, pk =>
        {
            CurrentPokemonEditor?.LoadPKM(pk);
        });
        var view = new Views.EncounterDatabaseView { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Encounter Database");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenDaycareAsync()
    {
        if (CurrentSave is null) return;
        
        var vm = new DaycareEditorViewModel(CurrentSave, _spriteRenderer);
        var view = new Views.DaycareEditorView { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Daycare");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenRecordsAsync()
    {
        if (CurrentSave is null) return;

        var vm = new RecordsEditorViewModel(CurrentSave);
        var view = new Views.RecordsEditorView { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Game Records");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenHallOfFameAsync()
    {
        if (CurrentSave is null) return;

        var vm = new HallOfFameEditorViewModel(CurrentSave, _spriteRenderer);
        var view = new Views.HallOfFameEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Hall of Fame");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenSecretBaseAsync()
    {
        if (CurrentSave is null) return;

        var vm = new SecretBaseEditorViewModel(CurrentSave, _spriteRenderer);
        var view = new Views.SecretBaseEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Secret Base Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenPokebeanAsync()
    {
        if (CurrentSave is null) return;

        var vm = new PokebeanEditorViewModel(CurrentSave);
        var view = new Views.PokebeanEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Poké Bean Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenFestivalPlazaAsync()
    {
        if (CurrentSave is null) return;

        var vm = new FestivalPlazaEditorViewModel(CurrentSave);
        var view = new Views.FestivalPlazaEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Festival Plaza Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenRaidEditorAsync()
    {
        if (CurrentSave is null) return;

        var vm = new RaidEditorViewModel(CurrentSave);
        var view = new Views.RaidEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Raid Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenSuperTrainingAsync()
    {
        if (CurrentSave is null) return;

        var vm = new SuperTrainingEditorViewModel(CurrentSave);
        var view = new Views.SuperTrainingEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Super Training Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenApricornAsync()
    {
        if (CurrentSave is null) return;

        var vm = new ApricornEditorViewModel(CurrentSave);
        var view = new Views.ApricornEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Apricorn Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenPokeGear4Async()
    {
        if (CurrentSave is null) return;

        var vm = new PokeGear4EditorViewModel(CurrentSave);
        var view = new Views.PokeGear4Editor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "PokéGear Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenGeonet4Async()
    {
        if (CurrentSave is null) return;

        var vm = new Geonet4EditorViewModel(CurrentSave);
        var view = new Views.Geonet4Editor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Geonet Globe Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenBoxLayoutAsync()
    {
        if (CurrentSave is null) return;

        var vm = new BoxLayoutEditorViewModel(CurrentSave);
        var view = new Views.BoxLayoutEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Box Layout Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenHoneyTreeAsync()
    {
        if (CurrentSave is null) return;

        var vm = new HoneyTreeEditorViewModel(CurrentSave);
        var view = new Views.HoneyTreeEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Honey Tree Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenUndergroundAsync()
    {
        if (CurrentSave is null) return;

        var vm = new UndergroundEditorViewModel(CurrentSave);
        var view = new Views.UndergroundEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Underground Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenRoamerAsync()
    {
        if (CurrentSave is null) return;

        var vm = new RoamerEditorViewModel(CurrentSave);
        var view = new Views.RoamerEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Roamer Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenOPowerAsync()
    {
        if (CurrentSave is null) return;

        var vm = new OPowerEditorViewModel(CurrentSave);
        var view = new Views.OPowerEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "O-Power Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenZygardeCellAsync()
    {
        if (CurrentSave is null) return;

        var vm = new ZygardeCellEditorViewModel(CurrentSave);
        var view = new Views.ZygardeCellEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Zygarde Cell Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenRaid9Async()
    {
        if (CurrentSave is null) return;

        var vm = new Raid9EditorViewModel(CurrentSave);
        var view = new Views.Raid9Editor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Tera Raid Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenRaidSevenStar9Async()
    {
        if (CurrentSave is null) return;

        var vm = new RaidSevenStar9EditorViewModel(CurrentSave);
        var view = new Views.RaidSevenStar9Editor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "7-Star Tera Raid Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenFashion9Async()
    {
        if (CurrentSave is not SAV9SV && CurrentSave is not SAV9ZA) return;

        var vm = new Fashion9EditorViewModel(CurrentSave);
        var view = new Views.Fashion9Editor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Fashion Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenDonutAsync()
    {
        if (CurrentSave is not SAV9ZA) return;

        var vm = new DonutEditorViewModel(CurrentSave);
        var view = new Views.DonutEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Donut Editor (PLZA)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenDLC5Async()
    {
        if (CurrentSave is not SAV5 sav) return;
        var vm = new DLC5EditorViewModel(sav, _dialogService);
        var view = new Views.DLC5Editor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "DLC Editor (Gen 5)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenPokedexAsync()
    {
        if (CurrentSave is null) return;

        if (CurrentSave is SAV9SV sav9)
        {
             var vm9 = new PokedexGen9EditorViewModel(sav9);
             var view9 = new Views.PokedexGen9Editor { DataContext = vm9 };
             await _dialogService.ShowDialogAsync(view9, "Pokédex Editor (Gen 9 SV)");
             return;
        }

        if (CurrentSave is SAV8LA savLA)
        {
             var vmLA = new PokedexLAEditorViewModel(savLA);
             var viewLA = new Views.PokedexLAEditor { DataContext = vmLA };
             await _dialogService.ShowDialogAsync(viewLA, "Pokédex Editor (PLA)");
             return;
        }

        // Default to Simple editor for earlier gens (or unsupported modern gens for now)
        var vm = new PokedexSimpleEditorViewModel(CurrentSave);
        var view = new Views.PokedexSimpleEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Pokédex Editor (Simple)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenBattlePassAsync()
    {
        if (CurrentSave is not SAV4BR) return;

        var vm = new BattlePassEditorViewModel(CurrentSave);
        var view = new Views.BattlePassEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Battle Pass Editor (PBR)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenUnderground8bAsync()
    {
        if (CurrentSave is not SAV8BS) return;

        var vm = new Underground8bEditorViewModel(CurrentSave);
        var view = new Views.Underground8bEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Underground Editor (BDSP)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenSealStickers8bAsync()
    {
        if (CurrentSave is not SAV8BS) return;

        var vm = new SealStickers8bEditorViewModel(CurrentSave);
        var view = new Views.SealStickers8bEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Seal Stickers Editor (BDSP)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenPoffin8bAsync()
    {
        if (CurrentSave is not SAV8BS) return;

        var vm = new Poffin8bEditorViewModel(CurrentSave);
        var view = new Views.Poffin8bEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Poffin Editor (BDSP)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenLink6Async()
    {
        if (CurrentSave is not SAV6) return;

        var vm = new Link6EditorViewModel(CurrentSave, _dialogService);
        var view = new Views.Link6Editor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Pokémon Link Editor (Gen 6)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenPokepuffAsync()
    {
        if (CurrentSave is null) return;

        var vm = new PokepuffEditorViewModel(CurrentSave);
        var view = new Views.PokepuffEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Poké Puff Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenPokeBlockAsync()
    {
        if (CurrentSave is null) return;

        var vm = new PokeBlockEditorViewModel(CurrentSave);
        var view = new Views.PokeBlockEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Pokéblock Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenBerryFieldAsync()
    {
        if (CurrentSave is null) return;

        var vm = new BerryFieldEditorViewModel(CurrentSave);
        var view = new Views.BerryFieldEditorView { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Berry Field Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenRoamer6Async()
    {
        if (CurrentSave is null) return;

        var vm = new Roamer6EditorViewModel(CurrentSave);
        var view = new Views.Roamer6Editor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Roamer Editor (XY)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenChatterAsync()
    {
        if (CurrentSave is null) return;

        var vm = new ChatterEditorViewModel(CurrentSave);
        var view = new Views.ChatterEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Chatter Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenRTCAsync()
    {
        if (CurrentSave is null) return;

        var vm = new RTCEditorViewModel(CurrentSave);
        var view = new Views.RTCEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "RTC Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenMedalAsync()
    {
        if (CurrentSave is null) return;

        var vm = new MedalEditorViewModel(CurrentSave);
        var view = new Views.MedalEditorView { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Medal Rally Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenUnityTower5Async()
    {
        if (CurrentSave is null) return;

        var vm = new UnityTower5EditorViewModel(CurrentSave);
        var view = new Views.UnityTower5Editor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Unity Tower Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenPoffinCaseAsync()
    {
        if (CurrentSave is null) return;

        var vm = new PoffinCaseEditorViewModel(CurrentSave);
        var view = new Views.PoffinCaseEditorView { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Poffin Case Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenPoketchAsync()
    {
        if (CurrentSave is null) return;

        var vm = new PoketchEditorViewModel(CurrentSave);
        var view = new Views.PoketchEditorView { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Pokétch Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenPokeBlock3CaseAsync()
    {
        if (CurrentSave is null) return;

        var vm = new PokeBlock3CaseEditorViewModel(CurrentSave);
        var view = new Views.PokeBlock3CaseEditorView { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Pokéblock Case Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenHallOfFame3Async()
    {
        if (CurrentSave is null) return;

        var vm = new HallOfFame3EditorViewModel(CurrentSave);
        var view = new Views.HallOfFame3EditorView { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Hall of Fame (Gen 3)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenFashionAsync()
    {
        if (CurrentSave is null) return;

        var vm = new FashionEditorViewModel(CurrentSave);
        var view = new Views.FashionEditorView { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Fashion Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenTrainerCard8Async()
    {
        if (CurrentSave is null) return;

        var vm = new TrainerCard8EditorViewModel(CurrentSave);
        var view = new Views.TrainerCard8EditorView { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Trainer Card Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenMailBoxAsync()
    {
        if (CurrentSave is null) return;

        var vm = new MailBoxEditorViewModel(CurrentSave);
        var view = new Views.MailBoxEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Mail Box Editor");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenEventFlags2Async()
    {
        if (CurrentSave is not SAV2 sav2) return;

        var vm = new EventFlags2EditorViewModel(sav2);
        var view = new Views.EventFlags2Editor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Event Flags (Gen 2)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenMisc2Async()
    {
        if (CurrentSave is not SAV2 sav2) return;

        var vm = new Misc2EditorViewModel(sav2);
        var view = new Views.Misc2Editor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Misc Editor (Gen 2)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenMisc8bAsync()
    {
        if (CurrentSave is not SAV8BS sav8b) return;

        var vm = new Misc8bEditorViewModel(sav8b);
        var view = new Views.Misc8bEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Misc Editor (BDSP)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenEventReset1Async()
    {
        if (CurrentSave is not SAV1 sav1) return;

        var vm = new EventReset1EditorViewModel(sav1);
        var view = new Views.EventReset1Editor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Event Reset (Gen 1)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenHallOfFame1Async()
    {
        if (CurrentSave is not SAV1 sav1) return;

        var vm = new HallOfFame1EditorViewModel(sav1);
        var view = new Views.HallOfFame1Editor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Hall of Fame (Gen 1)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenRoamer3Async()
    {
        if (CurrentSave is not SAV3 sav3) return;

        var vm = new Roamer3EditorViewModel(sav3);
        var view = new Views.Roamer3Editor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Roamer Editor (Gen 3)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenRTC3Async()
    {
        if (CurrentSave is not IGen3Hoenn) return;

        var vm = new RTC3EditorViewModel(CurrentSave);
        var view = new Views.RTC3Editor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "RTC Editor (Gen 3)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenCapture7GGAsync()
    {
        if (CurrentSave is not SAV7b sav7b) return;

        var vm = new Capture7GGEditorViewModel(sav7b);
        var view = new Views.Capture7GGEditor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Capture Records (Let's Go)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task OpenHallOfFame7Async()
    {
        if (CurrentSave is null) return;

        var vm = new HallOfFame7EditorViewModel(CurrentSave);
        var view = new Views.HallOfFame7Editor { DataContext = vm };
        await _dialogService.ShowDialogAsync(view, "Hall of Fame (SM/USUM)");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task DumpBoxesAsync()
    {
        if (CurrentSave is null) return;

        var path = await _dialogService.OpenFolderAsync("Select Folder to Dump Boxes");
        if (string.IsNullOrEmpty(path)) return;

        int count = 0;
        for (int b = 0; b < CurrentSave.BoxCount; b++)
        {
            var boxData = CurrentSave.GetBoxData(b);
            for (int s = 0; s < boxData.Length; s++)
            {
                var pk = boxData[s];
                if (pk.Species == 0) continue;

                var fileName = $"{b+1:00}_{s+1:00} - {pk.Nickname} - {pk.PID:X8}.{pk.Extension}";
                // Replace invalid characters
                foreach (var c in Path.GetInvalidFileNameChars())
                    fileName = fileName.Replace(c, '_');

                var filePath = Path.Combine(path, fileName);
                File.WriteAllBytes(filePath, pk.Data);
                count++;
            }
        }

        await _dialogService.ShowInformationAsync("Dump Boxes", $"Successfully dumped {count} Pokémon to {path}");
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task LoadBoxesAsync()
    {
        if (CurrentSave is null) return;

        var path = await _dialogService.OpenFolderAsync("Select Folder to Load Boxes");
        if (string.IsNullOrEmpty(path)) return;

        var extensions = EntityFileExtension.GetExtensions().Select(e => "." + e).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f)))
            .ToList();

        if (files.Count == 0)
        {
            await _dialogService.ShowInformationAsync("Load Boxes", "No supported Pokémon files found in the selected folder.");
            return;
        }

        int loaded = 0;
        int skipped = 0;
        int currentBox = 0;
        int currentSlot = 0;

        foreach (var file in files)
        {
            try
            {
                var data = File.ReadAllBytes(file);
                var pk = EntityFormat.GetFromBytes(data, CurrentSave.Context);
                if (pk == null)
                {
                    skipped++;
                    continue;
                }

                // Find next empty slot
                bool found = false;
                while (currentBox < CurrentSave.BoxCount)
                {
                    while (currentSlot < CurrentSave.BoxSlotCount)
                    {
                        var existing = CurrentSave.GetBoxSlotAtIndex(currentBox, currentSlot);
                        if (existing.Species == 0)
                        {
                            CurrentSave.SetBoxSlotAtIndex(pk, currentBox, currentSlot);
                            found = true;
                            loaded++;
                            break;
                        }
                        currentSlot++;
                    }
                    if (found) break;
                    currentSlot = 0;
                    currentBox++;
                }

                if (!found) break; // No more space
            }
            catch
            {
                skipped++;
            }
        }

        BoxViewer?.RefreshCurrentBox();
        var message = $"Successfully loaded {loaded} Pokémon.";
        if (skipped > 0) message += $"\nSkipped {skipped} files.";
        if (loaded < files.Count && currentBox >= CurrentSave.BoxCount) message += "\nStopped because boxes are full.";
        
        await _dialogService.ShowInformationAsync("Load Boxes", message);
    }
}
