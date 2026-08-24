using CommunityToolkit.Mvvm.Input;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class MainWindowViewModel
{
    /// <summary>Opens (or focuses) the existing Box viewer instance as a modeless workspace.</summary>
    [RelayCommand(CanExecute = nameof(HasSave))]
    private void OpenBoxWorkspace()
    {
        if (BoxViewer is not null)
            _windowService.ShowTool(BoxViewer, LocalizedStrings.Instance["Tab_Box"]);
    }

    /// <summary>Opens (or focuses) the existing Party viewer instance as a modeless workspace.</summary>
    [RelayCommand(CanExecute = nameof(HasSave))]
    private void OpenPartyWorkspace()
    {
        if (PartyViewer is not null)
            _windowService.ShowTool(PartyViewer, LocalizedStrings.Instance["Tab_Party"]);
    }
}
