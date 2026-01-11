
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Avalonia.Views;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class PokemonEditorViewModel
{
    [ObservableProperty]
    private bool _isLegal;

    [ObservableProperty]
    private string _legalityReport = string.Empty;

    [ObservableProperty]
    private bool _valid; // Legality fast-check

    private void Validate()
    {
        var pk = PreparePKM();
        var la = new LegalityAnalysis(pk, _sav.Personal);
        IsLegal = la.Valid;
        LegalityReport = la.Report();
        Valid = la.Valid;
    }

    [RelayCommand]
    private async Task ShowLegalityAsync()
    {
        Validate(); // Ensure report is fresh
        var view = new LegalityView(LegalityReport);
        await _dialogService.ShowDialogAsync(view, "Legality Analysis");
    }
}
