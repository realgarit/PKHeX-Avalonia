using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// Encounter search with species/move/version/type/level/location/shiny/egg filters. Selecting a result only
/// inspects it; <see cref="SelectEncounterCommand"/> ("Load into editor") is the explicit replace action.
/// </summary>
public partial class EncounterDatabaseViewModel : ViewModelBase, IDisposable
{
    /// <summary>Results beyond this are counted as truncated so an Any-species move search stays responsive.</summary>
    public const int MaxResults = 500;

    private static readonly EncounterTypeGroup[] TypeGroups =
        [EncounterTypeGroup.Egg, EncounterTypeGroup.Mystery, EncounterTypeGroup.Static, EncounterTypeGroup.Trade, EncounterTypeGroup.Slot];

    private readonly SaveFile _sav;
    private readonly ISpriteRenderer _spriteRenderer;
    private readonly IDialogService _dialogService;
    private readonly Action<PKM> _onSelect;
    private CancellationTokenSource? _search;

    public EncounterDatabaseViewModel(SaveFile sav, ISpriteRenderer spriteRenderer, IDialogService dialogService, Action<PKM> onSelect)
    {
        _sav = sav;
        _spriteRenderer = spriteRenderer;
        _dialogService = dialogService;
        _onSelect = onSelect;

        LoadSpeciesList();
        MoveList = BuildMoveList();
        VersionList = BuildVersionList();
        TypeFilters = TypeGroups.Select(g => new EncounterTypeFilterViewModel(g, OnTypeFilterChanged)).ToArray();
    }

    [ObservableProperty]
    private ObservableCollection<ComboItem> _speciesList = [];

    public IReadOnlyList<ComboItem> MoveList { get; }
    public IReadOnlyList<ComboItem> VersionList { get; }
    public IReadOnlyList<EncounterTypeFilterViewModel> TypeFilters { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private ushort _selectedSpecies;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SearchCommand))] private ushort _move1;
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SearchCommand))] private ushort _move2;
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SearchCommand))] private ushort _move3;
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SearchCommand))] private ushort _move4;
    [ObservableProperty] private int _selectedVersion;
    [ObservableProperty] private int _levelMin = 1;
    [ObservableProperty] private int _levelMax = 100;
    [ObservableProperty] private string _locationText = string.Empty;
    /// <summary>Null means "any"; true/false restrict to shiny-locked/not or egg/not.</summary>
    [ObservableProperty] private bool? _shinyFilter;
    [ObservableProperty] private bool? _eggFilter;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand), nameof(CancelSearchCommand))]
    private bool _isSearching;

    [ObservableProperty]
    private ObservableCollection<EncounterResultViewModel> _results = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedResult))]
    [NotifyCanExecuteChangedFor(nameof(LoadSelectedCommand))]
    private EncounterResultViewModel? _selectedResult;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _statusText = string.Empty;

    public bool HasSelectedResult => SelectedResult is not null;
    public bool HasStatus => !string.IsNullOrEmpty(StatusText);
    public bool IsTruncated { get; private set; }

    private ushort[] Moves => new[] { Move1, Move2, Move3, Move4 }.Where(m => m != 0).Distinct().ToArray();
    private bool AnyTypeSelected => TypeFilters.Any(t => t.IsChecked);

    private void OnTypeFilterChanged() => SearchCommand.NotifyCanExecuteChanged();

    private void LoadSpeciesList()
    {
        var names = GameInfo.Strings.Species;
        SpeciesList.Add(new ComboItem(LocalizedStrings.Instance["EncounterDatabase_Any"], 0));
        for (ushort i = 1; i <= _sav.MaxSpeciesID; i++)
        {
            var name = i < names.Count ? names[i] : $"#{i}";
            SpeciesList.Add(new ComboItem(name, i));
        }
    }

    private IReadOnlyList<ComboItem> BuildMoveList()
    {
        var names = GameInfo.Strings.Move;
        var list = new List<ComboItem> { new(LocalizedStrings.Instance["EncounterDatabase_Any"], 0) };
        for (int i = 1; i <= _sav.MaxMoveID && i < names.Count; i++)
            if (!string.IsNullOrWhiteSpace(names[i])) list.Add(new ComboItem(names[i], i));
        return list;
    }

    private IReadOnlyList<ComboItem> BuildVersionList()
    {
        var names = GameInfo.Sources.VersionDataSource.ToDictionary(v => v.Value, v => v.Text);
        var blank = _sav.BlankPKM;
        var list = new List<ComboItem> { new(LocalizedStrings.Instance["EncounterDatabase_AnyGame"], 0) };
        foreach (var version in GetSearchableVersions(blank, _sav.Context).Distinct().OrderByDescending(v => v))
            list.Add(new ComboItem(names.TryGetValue((int)version, out var text) ? text : version.ToString(), (int)version));
        return list;
    }

    /// <summary>
    /// Versions the encounter generator can search. The HOME range also contains reserved IDs
    /// (e.g. Winds/Waves) that have no entity context yet; Core's generator cannot build an
    /// evolution chain for them, so they are excluded the same way upstream's generator skips them.
    /// </summary>
    private static IEnumerable<GameVersion> GetSearchableVersions(IGameValueLimit limit, EntityContext context) =>
        GameUtil.GetVersionsWithinRange(limit, context).Where(static v => v.Context is not EntityContext.None);

    private bool CanSearch() => !IsSearching && AnyTypeSelected && (SelectedSpecies != 0 || Moves.Length != 0);

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync()
    {
        if (SelectedSpecies == 0 && Moves.Length == 0)
        {
            await _dialogService.ShowErrorAsync(LocalizedStrings.Instance["EncounterDatabase_SearchErrorTitle"], LocalizedStrings.Instance["EncounterDatabase_SelectSpeciesFirst"]);
            return;
        }

        _search?.Cancel();
        var cts = _search = new CancellationTokenSource();
        IsSearching = true;
        SelectedResult = null;
        Results.Clear();
        StatusText = LocalizedStrings.Instance["EncounterDatabase_Searching"];

        try
        {
            var query = CaptureQuery();
            var (found, truncated) = await Task.Run(() => Find(query, cts.Token), cts.Token);
            if (cts.IsCancellationRequested) { StatusText = LocalizedStrings.Instance["EncounterDatabase_Cancelled"]; return; }

            foreach (var enc in found)
                Results.Add(new EncounterResultViewModel(enc, enc.ConvertToPKM(_sav), _spriteRenderer));
            IsTruncated = truncated;
            OnPropertyChanged(nameof(IsTruncated));
            StatusText = Results.Count == 0
                ? LocalizedStrings.Instance["EncounterDatabase_NoResults"]
                : LocalizedStrings.Instance.Format(truncated ? "EncounterDatabase_ResultCountTruncated" : "EncounterDatabase_ResultCount", Results.Count);
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizedStrings.Instance["EncounterDatabase_Cancelled"];
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            await _dialogService.ShowErrorAsync(LocalizedStrings.Instance["EncounterDatabase_SearchErrorTitle"], ex.Message);
        }
        finally
        {
            if (ReferenceEquals(_search, cts)) IsSearching = false;
        }
    }

    private bool CanCancelSearch() => IsSearching;

    [RelayCommand(CanExecute = nameof(CanCancelSearch))]
    private void CancelSearch() => _search?.Cancel();

    [RelayCommand]
    private void ResetFilters()
    {
        SelectedSpecies = 0;
        Move1 = Move2 = Move3 = Move4 = 0;
        SelectedVersion = 0;
        LevelMin = 1;
        LevelMax = 100;
        LocationText = string.Empty;
        ShinyFilter = null;
        EggFilter = null;
        foreach (var filter in TypeFilters) filter.IsChecked = true;
    }

    private sealed record Query(ushort Species, ushort[] Moves, GameVersion[] Versions, HashSet<EncounterTypeGroup> Types,
        int LevelMin, int LevelMax, string Location, bool? Shiny, bool? Egg);

    private Query CaptureQuery()
    {
        var versions = SelectedVersion != 0 ? [(GameVersion)SelectedVersion]
            : GetSearchableVersions(_sav.BlankPKM, _sav.Context).ToArray();
        return new Query(SelectedSpecies, Moves, versions, TypeFilters.Where(t => t.IsChecked).Select(t => t.Group).ToHashSet(),
            Math.Min(LevelMin, LevelMax), Math.Max(LevelMin, LevelMax), LocationText.Trim(), ShinyFilter, EggFilter);
    }

    private (List<IEncounterable> Results, bool Truncated) Find(Query q, CancellationToken token)
    {
        var pk = _sav.BlankPKM;
        var species = q.Species != 0 ? [q.Species] : Enumerable.Range(1, _sav.MaxSpeciesID).Select(i => (ushort)i);
        var seen = new HashSet<IEncounterable>(ReferenceEqualityComparer.Instance);
        var results = new List<IEncounterable>();
        foreach (var s in species)
        {
            var forms = Math.Max((byte)1, _sav.Personal.GetFormEntry(s, 0).FormCount);
            for (byte form = 0; form < forms; form++)
            {
                token.ThrowIfCancellationRequested();
                if (FormInfo.IsBattleOnlyForm(s, form, pk.Format)) continue;
                pk.Species = s;
                pk.Form = form;
                pk.SetGender(pk.GetSaneGender());
                EncounterMovesetGenerator.OptimizeCriteria(pk, _sav);
                foreach (var enc in EncounterMovesetGenerator.GenerateEncounters(pk, q.Moves, q.Versions))
                {
                    token.ThrowIfCancellationRequested();
                    if (!seen.Add(enc) || !Matches(enc, q)) continue;
                    if (results.Count == MaxResults) return (results, true);
                    results.Add(enc);
                }
            }
        }
        return (results, false);
    }

    private static bool Matches(IEncounterable enc, Query q)
    {
        if (!q.Types.Contains(GetGroup(enc))) return false;
        if (enc.LevelMax < q.LevelMin || enc.LevelMin > q.LevelMax) return false;
        if (q.Shiny is { } shiny && enc.IsShiny != shiny) return false;
        if (q.Egg is { } egg && enc.IsEgg != egg) return false;
        if (q.Location.Length != 0 && !EncounterResultViewModel.GetLocationName(enc).Contains(q.Location, StringComparison.CurrentCultureIgnoreCase))
            return false;
        return true;
    }

    /// <summary>Classifies an encounter the same way Core's generator groups its search passes.</summary>
    public static EncounterTypeGroup GetGroup(IEncounterable enc)
    {
        if (enc is IEncounterEgg) return EncounterTypeGroup.Egg;
        if (enc is MysteryGift) return EncounterTypeGroup.Mystery;
        var name = enc.GetType().Name;
        if (name.StartsWith("EncounterSlot", StringComparison.Ordinal)) return EncounterTypeGroup.Slot;
        if (name.StartsWith("EncounterTrade", StringComparison.Ordinal)) return EncounterTypeGroup.Trade;
        return EncounterTypeGroup.Static;
    }

    private bool CanLoadSelected() => SelectedResult is not null;

    [RelayCommand(CanExecute = nameof(CanLoadSelected))]
    private Task LoadSelectedAsync() => SelectEncounterAsync(SelectedResult);

    /// <summary>Explicit "Load into editor": converts the encounter for this save and replaces the editor entity.</summary>
    [RelayCommand]
    private async Task SelectEncounterAsync(EncounterResultViewModel? result)
    {
        if (result?.Encounter == null) return;

        try
        {
            var pk = result.Encounter.ConvertToPKM(_sav);
            pk.ResetPartyStats();
            _onSelect(pk);
            StatusText = LocalizedStrings.Instance.Format("EncounterDatabase_Loaded", result.Species);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(LocalizedStrings.Instance["EncounterDatabase_ConversionErrorTitle"], LocalizedStrings.Instance.Format("EncounterDatabase_ConversionErrorMessage", ex.Message));
        }
    }

    public void Dispose() => _search?.Cancel();
}

public sealed partial class EncounterTypeFilterViewModel(EncounterTypeGroup group, Action changed) : ObservableObject
{
    public EncounterTypeGroup Group => group;
    public string Name => LocalizedStrings.Instance[$"EncounterDatabase_Type_{group}"];
    [ObservableProperty] private bool _isChecked = true;
    partial void OnIsCheckedChanged(bool value) => changed();
}

public class EncounterResultViewModel
{
    private readonly IEncounterable _encounter;

    public EncounterResultViewModel(IEncounterable encounter, PKM pkm, ISpriteRenderer renderer)
    {
        _encounter = encounter;
        Encounter = encounter;
        Sprite = renderer.GetSprite(pkm);
    }

    public IEncounterable Encounter { get; }
    public byte[]? Sprite { get; }
    public string Species
    {
        get
        {
            var name = GameInfo.Strings.Species.Count > _encounter.Species ? GameInfo.Strings.Species[_encounter.Species] : $"#{_encounter.Species}";
            return _encounter.Form == 0 ? name : $"{name}-{_encounter.Form}";
        }
    }
    public string Level => LocalizedStrings.Instance.Format(_encounter.LevelMin != _encounter.LevelMax ? "EncounterDatabase_LevelRange" : "EncounterDatabase_Level",
        _encounter.LevelMin, _encounter.LevelMax);
    public string Version => GameInfo.Sources.VersionDataSource.FirstOrDefault(v => v.Value == (int)_encounter.Version)?.Text ?? _encounter.Version.ToString();
    public string Type => LocalizedStrings.Instance[$"EncounterDatabase_Type_{EncounterDatabaseViewModel.GetGroup(_encounter)}"];
    public string Location => GetLocationName(_encounter);
    public string Flags => string.Join(", ", new[]
    {
        _encounter.IsShiny ? LocalizedStrings.Instance["EncounterDatabase_FlagShiny"] : null,
        _encounter.IsEgg ? LocalizedStrings.Instance["EncounterDatabase_FlagEgg"] : null,
    }.Where(f => f is not null));
    public string Moves => _encounter is IMoveset { Moves: { HasMoves: true } moves }
        ? moves.GetMovesetLine(GameInfo.Strings.Move, ", ")
        : string.Empty;
    public bool HasMoves => Moves.Length != 0;
    public string LongName => _encounter.LongName;

    public static string GetLocationName(IEncounterable enc)
    {
        var names = GameInfo.GetLocationList(enc.Version, enc.Context, false);
        return names.FirstOrDefault(x => x.Value == enc.Location)?.Text ?? $"#{enc.Location}";
    }

    /// <summary>Screen-reader-friendly summary for this result card, e.g. "Pikachu, Lv. 5, Route 1, Yellow".</summary>
    public string AccessibleName => $"{Species}, {Level}, {Location}, {Version}, {Type}";
}
