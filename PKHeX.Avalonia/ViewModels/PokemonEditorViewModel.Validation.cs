
using CommunityToolkit.Mvvm.ComponentModel;
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
}
