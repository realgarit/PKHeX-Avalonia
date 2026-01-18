using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
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
    [ObservableProperty] private IReadOnlyList<ComboItem> _speciesList;
    [ObservableProperty] private IReadOnlyList<ComboItem> _natureList;
    [ObservableProperty] private IReadOnlyList<ComboItem> _abilityList;
    [ObservableProperty] private IReadOnlyList<ComboItem> _itemList;

    public PKMDatabaseViewModel(SaveFile sav, ISpriteRenderer spriteRenderer, IDialogService dialogService)
    {
        _sav = sav;
        _spriteRenderer = spriteRenderer;
        _dialogService = dialogService;

        // Data sources with "Any" option prepended. Value must match SearchSettings wildcards:
        // Species=0 (Any), Nature=25 (Random), Ability=-1, Item=-1
        SpeciesList = new List<ComboItem> { new("Any", 0) }.Concat(GameInfo.Sources.SpeciesDataSource).ToList();
        NatureList = new List<ComboItem> { new("Any", 25) }.Concat(GameInfo.Sources.NatureDataSource).ToList(); // Nature.Random = 25
        AbilityList = new List<ComboItem> { new("Any", -1) }.Concat(GameInfo.Sources.AbilityDataSource).ToList();
        ItemList = new List<ComboItem> { new("Any", -1) }.Concat(GameInfo.Sources.GetItemDataSource(_sav.Version, _sav.Context, _sav.HeldItems)).ToList();
        
        // Default Selections (wildcards)
        Species = 0;
        Nature = 25; // Nature.Random = Any
        Ability = -1;
        Item = -1;
        
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (r, m) => RefreshLanguage());
    }

    [RelayCommand]
    private async Task SearchSaveAsync()
    {
        Results.Clear();
        IsSearching = true;
        StatusText = "Searching current save...";

        try
        {
            var settings = GetSearchSettings();
            var allPkms = _sav.BoxData.Concat(_sav.PartyData).ToList();
            
            // Check for valid Pokemon to scan
            int totalMons = allPkms.Count(p => p.Species != 0);
            if (totalMons == 0)
            {
                StatusText = "Save file contains no Pokémon (all slots empty).";
                return;
            }

            var matches = await Task.Run(() => settings.Search(allPkms).Where(p => p.Species != 0).ToList());
    
            foreach (var pk in matches)
            {
                Results.Add(new PKMDatabaseEntry(pk, _spriteRenderer));
            }
    
            StatusText = $"Found {Results.Count} matches in current save. (Scanned {totalMons} valid PKMs. Filters: S={Species} N={Nature})";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            await _dialogService.ShowErrorAsync("Search Error", ex.Message);
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task LoadFolderAsync()
    {
        var path = await _dialogService.OpenFolderAsync("Select Folder to Scan");
        if (string.IsNullOrEmpty(path)) return;

        Results.Clear();
        IsSearching = true;
        StatusText = "Scanning folder...";

        try 
        {
            var settings = GetSearchSettings();
            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            
            var matches = await Task.Run(() => 
            {
                var found = new List<PKM>();
                foreach (var file in files)
                {
                    var data = File.ReadAllBytes(file);
                    if (SaveUtil.IsSizeValid(data.Length))
                    {
                        var sav = SaveUtil.GetSaveFile(data);
                        if (sav != null)
                        {
                            var pkms = sav.BoxData.Concat(sav.PartyData);
                            found.AddRange(settings.Search(pkms).Where(p => p.Species != 0));
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
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private SearchSettings GetSearchSettings()
    {
        return new SearchSettings
        {
            Species = (ushort)Species,
            Nature = (Nature)Nature, // Nature.Random (25) is already 'Any'
            Ability = Ability, // -1 is already 'Any'
            Item = Item, // -1 is already 'Any'
            SearchShiny = IsShiny,
            SearchLegal = IsLegal,
            Format = _sav.Generation
        };
    }

    public event Action<PKM>? PokemonSelected;

    public void RefreshLanguage()
    {
        // Data sources with "Any" option prepended. Value must match SearchSettings wildcards:
        // Species=0 (Any), Nature=25 (Random), Ability=-1, Item=-1
        SpeciesList = new List<ComboItem> { new("Any", 0) }.Concat(GameInfo.Sources.SpeciesDataSource).ToList();
        NatureList = new List<ComboItem> { new("Any", 25) }.Concat(GameInfo.Sources.NatureDataSource).ToList();
        AbilityList = new List<ComboItem> { new("Any", -1) }.Concat(GameInfo.Sources.AbilityDataSource).ToList();
        ItemList = new List<ComboItem> { new("Any", -1) }.Concat(GameInfo.Sources.GetItemDataSource(_sav.Version, _sav.Context, _sav.HeldItems)).ToList();
        
        // Refresh current results
        var currentResults = Results.ToList();
        Results.Clear();
        foreach (var entry in currentResults)
        {
            entry.Refresh();
            Results.Add(entry);
        }
    }

    [RelayCommand]
    private void SelectPokemon(PKMDatabaseEntry entry)
    {
        PokemonSelected?.Invoke(entry.PKM);
    }
}

public partial class PKMDatabaseEntry : ObservableObject
{
    public PKM PKM { get; }
    public Bitmap? Sprite { get; }
    
    public string SpeciesName
    {
        get
        {
            if (PKM.Species == 0 || PKM.Species >= GameInfo.Strings.Species.Count)
                return "---";

            var name = GameInfo.Strings.Species[PKM.Species];
            if (PKM.Form > 0)
            {
                var formList = FormConverter.GetFormList(PKM.Species, GameInfo.Strings.types, GameInfo.Strings.forms, GameInfo.GenderSymbolASCII, PKM.Context);
                if (formList != null && PKM.Form < formList.Count() && !string.IsNullOrEmpty(formList[PKM.Form]))
                    name += $" ({formList[PKM.Form]})";
            }
            return name;
        }
    }

    public string Level => PKM.CurrentLevel.ToString();
    public string NatureName => GameInfo.Strings.Natures[(int)PKM.Nature];
    public string Gender => PKM.Gender switch { 0 => "♂", 1 => "♀", _ => "-" };

    public PKMDatabaseEntry(PKM pkm, ISpriteRenderer renderer)
    {
        PKM = pkm;
        Sprite = renderer.GetSprite(pkm);
    }

    public void Refresh()
    {
        OnPropertyChanged(string.Empty);
    }
}
