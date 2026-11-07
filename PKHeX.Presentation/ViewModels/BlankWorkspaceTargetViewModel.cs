using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// Modal picker for the target game of a standalone Pokémon workspace. The selected game decides
/// the entity format, the legality context and the trainer defaults of the in-memory blank save.
/// </summary>
public partial class BlankWorkspaceTargetViewModel : ViewModelBase, ICloseableDialog
{
    public Action? CloseRequested { get; set; }

    /// <summary>Every game a blank workspace can target, newest first.</summary>
    public IReadOnlyList<BlankWorkspaceTargetOption> Targets { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private BlankWorkspaceTargetOption? _selectedTarget;

    /// <summary>Optional explanation shown above the list (e.g. the file that will be opened).</summary>
    public string? Context { get; }

    public bool HasContext => !string.IsNullOrEmpty(Context);

    /// <summary>The confirmed target, or <see langword="null"/> when the dialog was cancelled.</summary>
    public GameVersion? Result { get; private set; }

    public BlankWorkspaceTargetViewModel(GameVersion? suggested = null, string? context = null)
    {
        Targets = BlankWorkspaceUseCase.SupportedTargets
            .Select(static v => new BlankWorkspaceTargetOption(v))
            .ToArray();
        Context = context;
        _selectedTarget = Targets.FirstOrDefault(t => t.Version == suggested) ?? Targets.FirstOrDefault();
    }

    private bool CanConfirm => SelectedTarget is not null;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (SelectedTarget is null)
            return;
        Result = SelectedTarget.Version;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        Result = null;
        CloseRequested?.Invoke();
    }
}

/// <summary>One selectable target game: its localized name plus its generation and entity format.</summary>
public sealed class BlankWorkspaceTargetOption(GameVersion version)
{
    public GameVersion Version { get; } = version;

    public string Name { get; } = GameInfo.GetVersionName(version);

    /// <summary>Generation and stored entity format, e.g. "Gen 9 · PK9".</summary>
    public string Detail { get; } = LocalizedStrings.Instance.Format(
        "BlankWorkspace_TargetDetail",
        version.Generation,
        EntityFormatName(version));

    private static string EntityFormatName(GameVersion version)
    {
        // Side games store their own entity formats inside a mainline context.
        var type = version.SaveFileType;
        var blank = type switch
        {
            SaveFileType.XD => EntityBlank.GetBlank(nameof(XK3)),
            SaveFileType.Colosseum => EntityBlank.GetBlank(nameof(CK3)),
            SaveFileType.BattleRevolution => EntityBlank.GetBlank(nameof(BK4)),
            _ => EntityBlank.GetBlank(type.Context),
        };
        return blank.Extension.ToUpperInvariant();
    }

    public override string ToString() => Name;
}
