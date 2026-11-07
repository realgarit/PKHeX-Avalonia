using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// Detached (modeless) workspace windows for Box, Party and the Pokémon editor (#247). Box and
/// Party keep their live, shared ViewModels; the Pokémon editor moves its single ViewModel into a
/// window instead of copying it, so there is never a second, competing unsaved editor.
/// </summary>
public partial class MainWindowViewModel
{
    private DetachedPokemonEditorViewModel? _detachedEditorHost;

    /// <summary>True while the Pokémon editor is shown in its own window instead of the main window.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmbeddedPokemonEditor))]
    private bool _isPokemonEditorDetached;

    /// <summary>
    /// The editor the main window embeds: <see langword="null"/> while it is detached, so exactly
    /// one view at a time edits the shared <see cref="CurrentPokemonEditor"/>.
    /// </summary>
    public PokemonEditorViewModel? EmbeddedPokemonEditor => IsPokemonEditorDetached ? null : CurrentPokemonEditor;

    partial void OnCurrentPokemonEditorChanged(PokemonEditorViewModel? value)
    {
        // A new save builds a new editor; a window that still hosts the old one must not survive it.
        if (_detachedEditorHost is { } host && !ReferenceEquals(host.Editor, value))
            _windowService.CloseTool(host);
        OnPropertyChanged(nameof(EmbeddedPokemonEditor));
    }

    /// <summary>Opens (or focuses) the Pokémon editor in its own window.</summary>
    [RelayCommand(CanExecute = nameof(HasSave))]
    private void OpenPokemonEditorWorkspace()
    {
        if (CurrentPokemonEditor is not { } editor)
            return;

        if (_detachedEditorHost is not { } host || !ReferenceEquals(host.Editor, editor))
        {
            host = new DetachedPokemonEditorViewModel(editor, DockPokemonEditorFrom);
            _detachedEditorHost = host;
        }

        IsPokemonEditorDetached = true;
        _windowService.ShowTool(host, LocalizedStrings.Instance["Workspace_PokemonEditorWindowTitle"], () =>
        {
            if (!ReferenceEquals(_detachedEditorHost, host))
                return;
            _detachedEditorHost = null;
            IsPokemonEditorDetached = false;
        });
    }

    /// <summary>Returns the detached Pokémon editor to the main window and shows the Pokémon workspace.</summary>
    [RelayCommand]
    private void DockPokemonEditor()
    {
        if (_detachedEditorHost is { } host)
            _windowService.CloseTool(host);

        // CloseTool normally clears the state through the window's closed callback; clear it here
        // as well so a host without a live window can never leave the editor hidden.
        _detachedEditorHost = null;
        IsPokemonEditorDetached = false;
        if (HasSave)
            ActiveWorkspace = MainWorkspace.Pokemon;
    }

    // A window from an earlier save session must not dock the editor of the current one.
    private void DockPokemonEditorFrom(DetachedPokemonEditorViewModel host)
    {
        if (ReferenceEquals(_detachedEditorHost, host))
            DockPokemonEditor();
    }

    private void OnBoxReturnToMainWindowRequested() => SelectWorkspaceTab(BoxWorkspaceTabIndex);

    private void OnPartyReturnToMainWindowRequested() => SelectWorkspaceTab(PartyWorkspaceTabIndex);
}

/// <summary>
/// Window content for the detached Pokémon editor: the one shared editor ViewModel plus a
/// dock-back action. It never copies the editor, so edits stay in a single place.
/// </summary>
public sealed partial class DetachedPokemonEditorViewModel : ViewModelBase
{
    private readonly Action<DetachedPokemonEditorViewModel> _returnToMainWindow;

    public DetachedPokemonEditorViewModel(PokemonEditorViewModel editor, Action<DetachedPokemonEditorViewModel> returnToMainWindow)
    {
        Editor = editor;
        _returnToMainWindow = returnToMainWindow;
    }

    public PokemonEditorViewModel Editor { get; }

    [RelayCommand]
    private void ReturnToMainWindow() => _returnToMainWindow(this);
}
