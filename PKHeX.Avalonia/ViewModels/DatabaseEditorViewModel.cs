using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

/// <summary>
/// PKM Database browser for viewing stored Pokémon files.
/// </summary>
public partial class DatabaseEditorViewModel : ViewModelBase
{
    public DatabaseEditorViewModel()
    {
        // Note: Database functionality requires file system access
        // This is a placeholder implementation
    }

    [ObservableProperty] private string _databasePath = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _resultCount;

    public ObservableCollection<PKMSummary> Results { get; } = [];

    [RelayCommand]
    private void Search()
    {
        // Database search would be implemented here
        ResultCount = Results.Count;
    }

    [RelayCommand]
    private void Clear()
    {
        SearchText = string.Empty;
        Results.Clear();
        ResultCount = 0;
    }
}

public class PKMSummary
{
    public PKMSummary(PKM pk)
    {
        Species = pk.Species;
        SpeciesName = GameInfo.Strings.Species[pk.Species];
        Level = pk.CurrentLevel;
        IsShiny = pk.IsShiny;
        OT = pk.OriginalTrainerName;
    }

    public ushort Species { get; }
    public string SpeciesName { get; }
    public int Level { get; }
    public bool IsShiny { get; }
    public string OT { get; }

    public string Display => $"{SpeciesName} Lv.{Level}{(IsShiny ? " ★" : "")}";
}
