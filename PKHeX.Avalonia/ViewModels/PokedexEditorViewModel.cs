using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class PokedexEditorViewModel : ViewModelBase
{
    private readonly SaveFile _sav;

    public PokedexEditorViewModel(SaveFile sav)
    {
        _sav = sav;
        MaxSpecies = sav.MaxSpeciesID;

        LoadEntries();
    }

    public int MaxSpecies { get; }

    [ObservableProperty]
    private ObservableCollection<PokedexEntryViewModel> _entries = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PokedexEntryViewModel> _filteredEntries = [];

    partial void OnSearchTextChanged(string value)
    {
        FilterEntries();
    }

    private void LoadEntries()
    {
        var speciesNames = GameInfo.Strings.Species;
        Entries.Clear();

        for (ushort i = 1; i <= MaxSpecies; i++)
        {
            var name = i < speciesNames.Count ? speciesNames[i] : $"Species #{i}";
            var seen = _sav.GetSeen(i);
            var caught = _sav.GetCaught(i);
            Entries.Add(new PokedexEntryViewModel(i, name, seen, caught));
        }

        FilterEntries();
    }

    private void FilterEntries()
    {
        FilteredEntries.Clear();
        var search = SearchText.ToLowerInvariant();

        foreach (var entry in Entries)
        {
            if (string.IsNullOrEmpty(search) ||
                entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                entry.Species.ToString().Contains(search))
            {
                FilteredEntries.Add(entry);
            }
        }
    }

    [RelayCommand]
    private void Save()
    {
        foreach (var entry in Entries)
        {
            _sav.SetSeen(entry.Species, entry.IsSeen);
            _sav.SetCaught(entry.Species, entry.IsCaught);
        }
    }

    [RelayCommand]
    private void Reset()
    {
        LoadEntries();
    }

    [RelayCommand]
    private void SetAllSeen()
    {
        foreach (var entry in Entries)
        {
            entry.IsSeen = true;
        }
    }

    [RelayCommand]
    private void SetAllCaught()
    {
        foreach (var entry in Entries)
        {
            entry.IsSeen = true;
            entry.IsCaught = true;
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        foreach (var entry in Entries)
        {
            entry.IsSeen = false;
            entry.IsCaught = false;
        }
    }
}

public partial class PokedexEntryViewModel : ViewModelBase
{
    public PokedexEntryViewModel(ushort species, string name, bool seen, bool caught)
    {
        Species = species;
        Name = name;
        _isSeen = seen;
        _isCaught = caught;
    }

    public ushort Species { get; }
    public string Name { get; }

    [ObservableProperty]
    private bool _isSeen;

    [ObservableProperty]
    private bool _isCaught;

    partial void OnIsCaughtChanged(bool value)
    {
        // If caught, must be seen
        if (value && !IsSeen)
            IsSeen = true;
    }
}
