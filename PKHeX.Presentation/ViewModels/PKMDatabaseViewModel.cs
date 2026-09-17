using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Core.Searching;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class PKMDatabaseViewModel : ViewModelBase
{
    private const int MaxFolderResults = 10_000;

    private readonly SaveFile _sav;
    private readonly ISpriteRenderer _spriteRenderer;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private ObservableCollection<PKMDatabaseEntry> _results = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchSaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelSearchCommand))]
    private bool _isSearching;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchProgressText))]
    private int _searchProgress;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private PKMDatabaseEntry? _selectedResult;

    /// <summary>Shared entity filter inputs + combo data sources (bound by the view).</summary>
    public EntityFilterViewModel Filter { get; }

    public string SearchProgressText =>
        LocalizedStrings.Instance.Format("PKMDatabase_ScannedFiles", SearchProgress);

    public PKMDatabaseViewModel(SaveFile sav, ISpriteRenderer spriteRenderer, IDialogService dialogService)
    {
        _sav = sav;
        _spriteRenderer = spriteRenderer;
        _dialogService = dialogService;

        Filter = new EntityFilterViewModel(sav);

        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (r, m) => RefreshLanguage());
    }

    private bool CanStartSearch => !IsSearching;

    [RelayCommand(CanExecute = nameof(CanStartSearch))]
    private async Task SearchSaveAsync()
    {
        Results.Clear();
        using var search = BeginSearch();
        StatusText = LocalizedStrings.Instance["PKMDatabase_SearchingCurrentSave"];

        try
        {
            var settings = Filter.GetSearchSettings();
            var allPkms = _sav.BoxData.Concat(_sav.PartyData).ToList();

            int totalMons = allPkms.Count(p => p.Species != 0);
            if (totalMons == 0)
            {
                StatusText = LocalizedStrings.Instance["PKMDatabase_SaveHasNoPokemon"];
                return;
            }

            var matches = await Task.Run(
                () => settings.Search(allPkms).Where(p => p.Species != 0).ToList(),
                search.Token);
            search.Token.ThrowIfCancellationRequested();

            foreach (var pk in matches)
                Results.Add(new PKMDatabaseEntry(pk, _spriteRenderer));

            StatusText = LocalizedStrings.Instance.Format("PKMDatabase_FoundMatchesInSave", Results.Count);
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizedStrings.Instance["PKMDatabase_SearchCancelled"];
        }
        catch (Exception ex)
        {
            StatusText = LocalizedStrings.Instance.Format("PKMDatabase_SearchErrorStatus", ex.Message);
            await _dialogService.ShowErrorAsync(LocalizedStrings.Instance["PKMDatabase_SearchErrorTitle"], ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartSearch))]
    private async Task LoadFolderAsync()
    {
        var path = await _dialogService.OpenFolderAsync(LocalizedStrings.Instance["PKMDatabase_SelectFolderToScanTitle"]);
        if (string.IsNullOrEmpty(path)) return;

        Results.Clear();
        using var search = BeginSearch();
        StatusText = LocalizedStrings.Instance["PKMDatabase_ScanningFolder"];

        try
        {
            var settings = Filter.GetSearchSettings();
            var progress = new Progress<int>(count => SearchProgress = count);
            var scan = await Task.Run(
                () => ScanFolder(path, _sav, settings, progress, search.Token),
                search.Token);
            search.Token.ThrowIfCancellationRequested();

            foreach (var pk in scan.Matches)
                Results.Add(new PKMDatabaseEntry(pk, _spriteRenderer));

            StatusText = LocalizedStrings.Instance.Format(
                "PKMDatabase_FolderScanSummary",
                scan.FilesScanned,
                scan.FilesSkipped,
                Results.Count);
            if (scan.ResultLimitReached)
            {
                StatusText += " " + LocalizedStrings.Instance.Format(
                    "PKMDatabase_FolderScanLimit",
                    MaxFolderResults);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizedStrings.Instance["PKMDatabase_SearchCancelled"];
        }
        catch (Exception ex)
        {
            StatusText = LocalizedStrings.Instance.Format("PKMDatabase_SearchErrorStatus", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(IsSearching))]
    private void CancelSearch() => _searchCts?.Cancel();

    private SearchScope BeginSearch()
    {
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        IsSearching = true;
        SearchProgress = 0;
        return new SearchScope(this, cts);
    }

    private static FolderScanResult ScanFolder(
        string path,
        SaveFile referenceSave,
        SearchSettings settings,
        IProgress<int> progress,
        CancellationToken cancellationToken)
    {
        var matches = new List<PKM>();
        var filesScanned = 0;
        var filesSkipped = 0;
        var resultLimitReached = false;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
        };

        foreach (var file in Directory.EnumerateFiles(path, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            filesScanned++;

            try
            {
                var info = new FileInfo(file);
                if (!info.Exists ||
                    (!SaveUtil.IsSizeValid(info.Length) && !EntityDetection.IsSizePlausible(info.Length)))
                {
                    filesSkipped++;
                    progress.Report(filesScanned);
                    continue;
                }

                switch (FileUtil.GetSupportedFile(file, referenceSave))
                {
                    case SaveFile save:
                        matches.AddRange(settings.Search(save.BoxData.Concat(save.PartyData))
                            .Where(p => p.Species != 0));
                        break;
                    case PKM pk when pk.Species != 0 && settings.Search([pk]).Any():
                        matches.Add(pk);
                        break;
                    default:
                        filesSkipped++;
                        break;
                }
            }
            catch
            {
                // One unreadable or malformed file must not discard the rest of the scan.
                filesSkipped++;
            }

            progress.Report(filesScanned);
            if (matches.Count >= MaxFolderResults)
            {
                matches = matches.Take(MaxFolderResults).ToList();
                resultLimitReached = true;
                break;
            }
        }

        return new FolderScanResult(matches, filesScanned, filesSkipped, resultLimitReached);
    }

    private sealed record FolderScanResult(
        IReadOnlyList<PKM> Matches,
        int FilesScanned,
        int FilesSkipped,
        bool ResultLimitReached);

    private sealed class SearchScope(PKMDatabaseViewModel owner, CancellationTokenSource cts) : IDisposable
    {
        public CancellationToken Token => cts.Token;

        public void Dispose()
        {
            if (ReferenceEquals(owner._searchCts, cts))
            {
                owner._searchCts = null;
                owner.IsSearching = false;
            }

            cts.Dispose();
        }
    }

    public event Action<PKM>? PokemonSelected;

    public void RefreshLanguage()
    {
        Filter.RefreshLanguage();
        foreach (var entry in Results)
            entry.Refresh();
        OnPropertyChanged(nameof(SearchProgressText));
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
    public byte[]? Sprite { get; }
    
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
                if (formList != null && PKM.Form < formList.Length && !string.IsNullOrEmpty(formList[PKM.Form]))
                    name += $" ({formList[PKM.Form]})";
            }
            return name;
        }
    }

    public string Level => PKM.CurrentLevel.ToString();
    public string NatureName => StringResourceLookup.Nature((int)PKM.Nature);
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
