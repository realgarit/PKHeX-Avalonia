
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class PokemonEditorViewModel
{
    // Moves
    [ObservableProperty]
    private int _move1;

    [ObservableProperty]
    private int _move2;

    [ObservableProperty]
    private int _move3;

    [ObservableProperty]
    private int _move4;

    // Relearn Moves
    [ObservableProperty]
    private int _relearnMove1;

    [ObservableProperty]
    private int _relearnMove2;

    [ObservableProperty]
    private int _relearnMove3;

    [ObservableProperty]
    private int _relearnMove4;

    // PP
    [ObservableProperty]
    private int _pp1;

    [ObservableProperty]
    private int _pp2;

    [ObservableProperty]
    private int _pp3;

    [ObservableProperty]
    private int _pp4;

    [ObservableProperty]
    private int _ppUps1;

    [ObservableProperty]
    private int _ppUps2;

    [ObservableProperty]
    private int _ppUps3;

    [ObservableProperty]
    private int _ppUps4;

    // Tech Records (Gen 8+)
    public bool HasTechRecords => _pk is ITechRecord;

    [RelayCommand]
    private async Task ImportShowdown()
    {
        var text = await _dialogService.GetClipboardTextAsync();
        if (string.IsNullOrWhiteSpace(text)) return;

        if (ShowdownParsing.TryParseAnyLanguage(text, out var set))
        {
            _pk.ApplySetDetails(set);
            LoadFromPKM();
        }
        else
        {
            await _dialogService.ShowErrorAsync("Import Failed", "Could not parse Showdown text.");
        }
    }

    [RelayCommand]
    private async Task ExportShowdown()
    {
        var pk = PreparePKM();
        var set = new ShowdownSet(pk);
        await _dialogService.SetClipboardTextAsync(set.Text);
    }

    [RelayCommand]
    private void SuggestRelearnMoves()
    {
        var pk = PreparePKM();
        var la = new LegalityAnalysis(pk, _sav.Personal);
        Span<ushort> moves = stackalloc ushort[4];
        la.GetSuggestedRelearnMovesFromEncounter(moves);
        
        RelearnMove1 = moves[0];
        RelearnMove2 = moves[1];
        RelearnMove3 = moves[2];
        RelearnMove4 = moves[3];
        
        Validate();
    }

    [RelayCommand]
    private void SuggestCurrentMoves()
    {
        var pk = PreparePKM();
        var la = new LegalityAnalysis(pk, _sav.Personal);
        Span<ushort> moves = stackalloc ushort[4];
        la.GetSuggestedCurrentMoves(moves);
        
        Move1 = moves[0];
        Move2 = moves[1];
        Move3 = moves[2];
        Move4 = moves[3];
        
        Validate();
    }

    [RelayCommand]
    private void SetAllTechRecords()
    {
        if (_pk is not ITechRecord tr) return;
        
        var pk = PreparePKM();
        // Use the SetRecordFlags extension with LegalAll option
        tr.SetRecordFlags(pk, TechnicalRecordApplicatorOption.LegalAll);
        LoadFromPKM();
    }

    [RelayCommand]
    private void ClearTechRecords()
    {
        if (_pk is not ITechRecord tr) return;
        
        tr.ClearRecordFlags();
        LoadFromPKM();
    }

    partial void OnMove1Changed(int value) { if (!_isLoading) Validate(); }
    partial void OnMove2Changed(int value) { if (!_isLoading) Validate(); }
    partial void OnMove3Changed(int value) { if (!_isLoading) Validate(); }
    partial void OnMove4Changed(int value) { if (!_isLoading) Validate(); }
    partial void OnRelearnMove1Changed(int value) { if (!_isLoading) Validate(); }
    partial void OnRelearnMove2Changed(int value) { if (!_isLoading) Validate(); }
    partial void OnRelearnMove3Changed(int value) { if (!_isLoading) Validate(); }
    partial void OnRelearnMove4Changed(int value) { if (!_isLoading) Validate(); }
}
