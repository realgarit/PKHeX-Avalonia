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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSave))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyCanExecuteChangedFor(nameof(SaveFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveFileAsCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseFileCommand))]
    private SaveFile? _currentSave;

    [ObservableProperty]
    private BoxViewerViewModel? _boxViewer;

    public bool HasSave => CurrentSave is not null;

    public string WindowTitle => CurrentSave is not null
        ? $"PKHeX Avalonia - {CurrentSave.Version}"
        : "PKHeX Avalonia";

    public MainWindowViewModel(
        ISaveFileService saveFileService,
        IDialogService dialogService,
        ISpriteRenderer spriteRenderer)
    {
        _saveFileService = saveFileService;
        _dialogService = dialogService;
        _spriteRenderer = spriteRenderer;

        _saveFileService.SaveFileChanged += OnSaveFileChanged;
    }

    private void OnSaveFileChanged(SaveFile? sav)
    {
        CurrentSave = sav;
        if (sav is not null)
        {
            _spriteRenderer.Initialize(sav);
            var boxViewer = new BoxViewerViewModel(sav, _spriteRenderer);
            boxViewer.SlotActivated += OnSlotActivated;
            BoxViewer = boxViewer;
        }
        else
        {
            if (BoxViewer is not null)
                BoxViewer.SlotActivated -= OnSlotActivated;
            BoxViewer = null;
        }
    }

    private async void OnSlotActivated(int box, int slot)
    {
        if (CurrentSave is null)
            return;

        var pk = CurrentSave.GetBoxSlotAtIndex(box, slot);
        if (pk.Species == 0)
            return; // Empty slot

        var editorVm = new PokemonEditorViewModel(pk, CurrentSave, _spriteRenderer, box, slot);
        var editorView = new PokemonEditor { DataContext = editorVm };

        var saved = await _dialogService.ShowDialogAsync(editorView, "Edit Pokémon");
        if (saved)
        {
            // Refresh the box viewer to show updated sprite
            BoxViewer?.RefreshCurrentBox();
        }
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
}
