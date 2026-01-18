using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media.Imaging;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class EncounterDatabaseViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly ISpriteRenderer _spriteRenderer;
    private readonly IDialogService _dialogService;
    private readonly Action<PKM> _onSelect;

    public EncounterDatabaseViewModel(SaveFile sav, ISpriteRenderer spriteRenderer, IDialogService dialogService, Action<PKM> onSelect)
    {
        _sav = sav;
        _spriteRenderer = spriteRenderer;
        _dialogService = dialogService;
        _onSelect = onSelect;

        LoadSpeciesList();
    }

    [ObservableProperty]
    private ObservableCollection<ComboItem> _speciesList = [];

    [ObservableProperty]
    private ushort _selectedSpecies;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private ObservableCollection<EncounterResultViewModel> _results = [];

    [ObservableProperty]
    private EncounterResultViewModel? _selectedResult;

    private void LoadSpeciesList()
    {
        var names = GameInfo.Strings.Species;
        for (ushort i = 1; i <= _sav.MaxSpeciesID; i++)
        {
            var name = i < names.Count ? names[i] : $"Species #{i}";
            SpeciesList.Add(new ComboItem(name, i));
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (SelectedSpecies == 0)
        {
            await _dialogService.ShowErrorAsync("Search Error", "Please select a species first.");
            return;
        }

        IsSearching = true;
        Results.Clear();

        try
        {
            var foundEncounters = await Task.Run(() =>
            {
                var blank = _sav.BlankPKM;
                blank.Species = SelectedSpecies;
                blank.Form = 0;
                blank.Gender = 0;

                var versions = GameUtil.GetVersionsWithinRange(blank, _sav.Generation).ToArray();
                return EncounterMovesetGenerator.GenerateEncounters(blank, ReadOnlyMemory<ushort>.Empty, versions)
                    .Take(100)
                    .ToList();
            });

            foreach (var enc in foundEncounters)
            {
                var pk = enc.ConvertToPKM(_sav);
                Results.Add(new EncounterResultViewModel(enc, pk, _spriteRenderer));
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync("Search Error", ex.Message);
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private void SelectEncounter(EncounterResultViewModel? result)
    {
        if (result?.Encounter == null) return;

        try
        {
            var pk = result.Encounter.ConvertToPKM(_sav);
            pk.ResetPartyStats();
            _onSelect(pk);
        }
        catch (Exception)
        {
            // Could not convert, ignore
        }
    }
}

public class EncounterResultViewModel // Removed 'partial' as it's not needed unless using ObservableProperty
{
    private readonly IEncounterable _encounter;
    
    public EncounterResultViewModel(IEncounterable encounter, PKM pkm, ISpriteRenderer renderer)
    {
        _encounter = encounter;
        Encounter = encounter;
        Sprite = renderer.GetSprite(pkm);
    }
    
    public IEncounterable Encounter { get; }
    public Bitmap? Sprite { get; }
    public string Species => GameInfo.Strings.Species.Count > _encounter.Species ? GameInfo.Strings.Species[_encounter.Species] : $"#{_encounter.Species}";
    public string Level => $"Lv. {_encounter.LevelMin}" + (_encounter.LevelMin != _encounter.LevelMax ? $"-{_encounter.LevelMax}" : "");
    public string Version => _encounter.Version.ToString();
    public string Type => _encounter.GetType().Name.Replace("Encounter", "");
    public string Location
    {
        get
        {
            var version = _encounter.Version;
            var context = _encounter.Context;
            var locationId = _encounter.Location;
            var names = GameInfo.GetLocationList(version, context, false);
            return names.FirstOrDefault(x => x.Value == locationId)?.Text ?? $"#{locationId}";
        }
    }
}
