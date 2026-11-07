using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// Standalone Pokémon workspace: edit individual Pokémon files against a selected target game
/// without a real save. An in-memory blank save supplies the format, legality and trainer context.
/// It has no file path and the save-writing commands stay disabled while it is current, so the
/// workspace never writes a save file. Pokémon leave the workspace through Save Pokémon As.
/// </summary>
public partial class MainWindowViewModel
{
    // The blank save that backs the current standalone workspace. Identity-compared against
    // CurrentSave, so loading any real save (File > Open, drop, backup restore, folder list)
    // leaves the workspace mode without further bookkeeping.
    private SaveFile? _blankWorkspaceSave;

    /// <summary>True while the current save is the in-memory context of a standalone Pokémon workspace.</summary>
    public bool IsBlankWorkspace => CurrentSave is not null && ReferenceEquals(CurrentSave, _blankWorkspaceSave);

    /// <summary>Save and Save As write the save file to disk; a blank workspace has no save to keep.</summary>
    private bool CanWriteSave => HasSave && !IsBlankWorkspace;

    /// <summary>
    /// Asks for a target game and starts an empty standalone Pokémon workspace for it.
    /// </summary>
    [RelayCommand]
    private async Task NewPokemonWorkspaceAsync()
    {
        if (!await CanLeaveCurrentSaveAsync())
            return;

        var target = await PickWorkspaceTargetAsync(suggested: null, context: null);
        if (target is not { } version)
            return;

        StartBlankWorkspace(version);
    }

    /// <summary>
    /// Opens a Pokémon file. With a save (real or blank) loaded, the file loads into the editor and
    /// converts to that save's format. Without one, the user picks a target game first and the
    /// file opens in a new standalone workspace.
    /// </summary>
    [RelayCommand]
    private async Task OpenPokemonFileAsync()
    {
        var path = await _dialogService.OpenFileAsync(T("File_OpenPokemonFileTitle"), FileDialogFilters.OpenEntityFile);
        if (string.IsNullOrEmpty(path))
            return;

        await OpenPokemonFilePathAsync(path);
    }

    /// <summary>Opens <paramref name="path"/> as described in <see cref="OpenPokemonFileAsync"/>.</summary>
    public async Task OpenPokemonFilePathAsync(string path)
    {
        if (CurrentSave is null)
        {
            var obj = TryGetSupportedFile(path);
            if (obj is SaveFile)
            {
                // A save file picked here is still a save: open it the normal way.
                await OpenSaveFilePathAsync(path);
                return;
            }

            var suggestion = GetSuggestedTarget(obj);
            if (suggestion is null)
            {
                await ShowPokemonFileError(path);
                return;
            }

            var target = await PickWorkspaceTargetAsync(suggestion,
                LocalizedStrings.Instance.Format("BlankWorkspace_OpenFileContext", Path.GetFileName(path)));
            if (target is not { } version)
                return;

            // Another flow may have loaded a save while the picker was open; never replace it silently.
            if (CurrentSave is not null)
                return;

            StartBlankWorkspace(version);
        }

        if (CurrentSave is not { } save || CurrentPokemonEditor is null)
            return;

        var result = new ImportEntityFileUseCase().Execute(save, path);
        switch (result.Kind)
        {
            case EntityFileDropKind.Entity:
                LoadEntity(result.Entity);
                return;
            case EntityFileDropKind.SaveFile:
                await OpenSaveFilePathAsync(path);
                return;
            default:
                await ShowPokemonFileError(path);
                return;
        }
    }

    /// <summary>
    /// Writes the Pokémon in the editor to a decrypted entity file (e.g. ".pk9"), for a real save
    /// and for a standalone workspace alike. The save itself is not touched.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task SavePokemonAsAsync()
    {
        if (CurrentPokemonEditor is not { } editor)
            return;

        var pk = editor.PreparePKM();
        var exported = new ExportEntityToFileUseCase().Execute(pk);
        if (exported is not { } file)
        {
            await _dialogService.ShowErrorAsync(T("Menu_File_SavePokemonAs"), T("File_LoadPokemonFirst"));
            return;
        }

        var path = await _dialogService.SaveFileAsync(
            T("File_SavePokemonAsTitle"), file.FileName, FileDialogFilters.EntityFile(pk.Extension));
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            await File.WriteAllBytesAsync(path, file.Data);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(T("Common_Error"),
                LocalizedStrings.Instance.Format("File_SavePokemonFailed", ex.Message));
        }
    }

    private void StartBlankWorkspace(GameVersion version)
    {
        var sav = new BlankWorkspaceUseCase().Execute(version);
        _blankWorkspaceSave = sav;
        // No path: the gateway has nowhere to write, and Save/Save As stay disabled.
        _saveFileService.OpenLoadedSave(sav);
    }

    private async Task<GameVersion?> PickWorkspaceTargetAsync(GameVersion? suggested, string? context)
    {
        var picker = new BlankWorkspaceTargetViewModel(suggested, context);
        await _windowService.ShowDialogAsync(picker, T("BlankWorkspace_Title"));
        return picker.Result;
    }

    private static GameVersion? GetSuggestedTarget(object? obj) => obj switch
    {
        PKM pk => BlankWorkspaceUseCase.SuggestTarget(pk),
        MysteryGift { IsEntity: true } g => SuggestFromContext(g.Context),
        IEncounterInfo { Species: > 0 } e => SuggestFromContext(e.Context),
        _ => null,
    };

    private static GameVersion SuggestFromContext(EntityContext context)
    {
        try
        {
            var version = context.GetSingleGameVersion();
            if (BlankWorkspaceUseCase.IsSupportedTarget(version))
                return version;
        }
        catch (ArgumentOutOfRangeException)
        {
            // Fall back to the newest target below.
        }
        return BlankWorkspaceUseCase.SupportedTargets[0];
    }

    private Task ShowPokemonFileError(string path) => _dialogService.ShowErrorAsync(
        T("File_ImportFailedTitle"),
        LocalizedStrings.Instance.Format("File_PokemonFileNotSupported", Path.GetFileName(path)));
}
