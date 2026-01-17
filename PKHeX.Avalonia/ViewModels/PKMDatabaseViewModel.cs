using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using PKHeX.Core.Searching;

namespace PKHeX.Avalonia.ViewModels;

public partial class PKMDatabaseViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly ISpriteRenderer _spriteRenderer;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<PKMDatabaseEntry> _results = [];

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private int _searchProgress;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private PKMDatabaseEntry? _selectedResult;

    // Filtering properties (mapped to SearchSettings)
    [ObservableProperty] private int _species;
    [ObservableProperty] private int _nature;
    [ObservableProperty] private int _ability;
    [ObservableProperty] private int _item;
    [ObservableProperty] private bool? _isShiny;
    [ObservableProperty] private bool? _isLegal;

    // Data Sources for View
    public IReadOnlyList<ComboItem> SpeciesList { get; }
    public IReadOnlyList<ComboItem> NatureList { get; }
    public IReadOnlyList<ComboItem> AbilityList { get; }
    public IReadOnlyList<ComboItem> ItemList { get; }

    public PKMDatabaseViewModel(SaveFile sav, ISpriteRenderer spriteRenderer, IDialogService dialogService)
    {
        _sav = sav;
        _spriteRenderer = spriteRenderer;
        _dialogService = dialogService;

        SpeciesList = GameInfo.Sources.SpeciesDataSource;
        NatureList = GameInfo.Sources.NatureDataSource;
        AbilityList = GameInfo.Sources.AbilityDataSource;
        ItemList = GameInfo.Sources.GetItemDataSource(_sav.Version, _sav.Context, _sav.HeldItems);
    }

    [RelayCommand]
    private async Task SearchSaveAsync()
    {
        Results.Clear();
        IsSearching = true;
        StatusText = "Searching current save...";

        var settings = GetSearchSettings();
        var allPkms = _sav.BoxData.Concat(_sav.PartyData);
        
        var matches = await Task.Run(() => settings.Search(allPkms).ToList());

        foreach (var pk in matches)
        {
            Results.Add(new PKMDatabaseEntry(pk, _spriteRenderer));
        }

        StatusText = $"Found {Results.Count} matches in current save.";
        IsSearching = false;
    }

    [RelayCommand]
    private async Task LoadFolderAsync()
    {
        var path = await _dialogService.OpenFolderAsync("Select Folder to Scan");
        if (string.IsNullOrEmpty(path)) return;

        Results.Clear();
        IsSearching = true;
        StatusText = "Scanning folder...";

        var settings = GetSearchSettings();
        var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
        
        var matches = await Task.Run(() => 
        {
            var found = new List<PKM>();
            int count = 0;
            foreach (var file in files)
            {
                count++;
                // Progress could be reported here if needed
                
                var data = File.ReadAllBytes(file);
                if (SaveUtil.IsSizeValid(data.Length))
                {
                    var sav = SaveUtil.GetSaveFile(data);
                    if (sav != null)
                    {
                        var pkms = sav.BoxData.Concat(sav.PartyData);
                        found.AddRange(settings.Search(pkms));
                    }
                }
                else
                {
                    var pk = EntityFormat.GetFromBytes(data, _sav.Context);
                    if (pk != null && settings.Search(new[] { pk }).Any())
                        found.Add(pk);
                }
            }
            return found;
        });

        foreach (var pk in matches)
        {
            Results.Add(new PKMDatabaseEntry(pk, _spriteRenderer));
        }

        StatusText = $"Found {Results.Count} matches in folder.";
        IsSearching = false;
    }

    private SearchSettings GetSearchSettings()
    {
        return new SearchSettings
        {
            Species = (ushort)Species,
            Nature = (Nature)Nature,
            Ability = Ability,
            Item = Item,
            SearchShiny = IsShiny,
            SearchLegal = IsLegal,
            Format = _sav.Generation
        };
    }

    public event Action<PKM>? PokemonSelected;

    [RelayCommand]
    private void SelectPokemon(PKMDatabaseEntry entry)
    {
        PokemonSelected?.Invoke(entry.PKM);
    }
}

public class PKMDatabaseEntry
{
    public PKM PKM { get; }
    public Bitmap? Sprite { get; }
    public string SpeciesName => GameInfo.Strings.Species[PKM.Species];
    public string Level => PKM.CurrentLevel.ToString();
    public string NatureName => GameInfo.Strings.Natures[(int)PKM.Nature];
    public string Gender => PKM.Gender switch { 0 => "♂", 1 => "♀", _ => "-" };

    public PKMDatabaseEntry(PKM pkm, ISpriteRenderer renderer)
    {
        PKM = pkm;
        Sprite = renderer.GetSprite(pkm);
    }
}
