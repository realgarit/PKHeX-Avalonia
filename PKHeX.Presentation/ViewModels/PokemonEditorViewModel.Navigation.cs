using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PKHeX.Presentation.ViewModels;

public partial class PokemonEditorViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMainEditorSection), nameof(IsStatsEditorSection), nameof(IsMetEditorSection),
        nameof(IsMovesEditorSection), nameof(IsOtEditorSection), nameof(IsAdvancedEditorSection))]
    private int _selectedEditorSection;

    public bool IsMainEditorSection => SelectedEditorSection == 0;
    public bool IsStatsEditorSection => SelectedEditorSection == 1;
    public bool IsMetEditorSection => SelectedEditorSection == 2;
    public bool IsMovesEditorSection => SelectedEditorSection == 3;
    public bool IsOtEditorSection => SelectedEditorSection == 4;
    public bool IsAdvancedEditorSection => SelectedEditorSection >= 5;

    [RelayCommand]
    private void SelectEditorSection(string? section)
    {
        if (int.TryParse(section, out var index) && index is >= 0 and <= 7)
            SelectedEditorSection = index;
    }
}
