using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

/// <summary>
/// Encounter database browser for searching available encounters.
/// </summary>
public partial class EncountersEditorViewModel : ViewModelBase
{
    private readonly SaveFile _sav;

    public EncountersEditorViewModel(SaveFile sav)
    {
        _sav = sav;
        LoadSpecies();
    }

    public ObservableCollection<ComboItem> Species { get; } = [];

    [ObservableProperty] private int _selectedSpecies;
    [ObservableProperty] private string _searchResults = "Select a species to search encounters.";

    private void LoadSpecies()
    {
        Species.Clear();
        var speciesList = GameInfo.FilteredSources.Species;
        foreach (var s in speciesList)
        {
            Species.Add(s);
        }
    }

    [RelayCommand]
    private void SearchEncounters()
    {
        if (SelectedSpecies <= 0)
        {
            SearchResults = "Please select a species.";
            return;
        }

        // Simplified search - shows species was selected
        SearchResults = $"Searching encounters for species #{SelectedSpecies}...";
    }

    [RelayCommand]
    private void Reset()
    {
        SelectedSpecies = 0;
        SearchResults = "Select a species to search encounters.";
    }
}
