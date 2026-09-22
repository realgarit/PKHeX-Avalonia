using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Application.Abstractions;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class FolderListViewModel : ViewModelBase, ICloseableDialog, IDisposable
{
    private readonly ISaveFileGateway _saveFileService;
    private readonly AppSettings _settings;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource _scanCts = new();

    public ObservableCollection<SaveFilePreviewViewModel> RecentSaves { get; } = [];
    public ObservableCollection<SaveFilePreviewViewModel> FilteredRecentSaves { get; } = [];

    public ObservableCollection<SaveFilePreviewViewModel> BackupSaves { get; } = [];
    public ObservableCollection<SaveFilePreviewViewModel> FilteredBackupSaves { get; } = [];

    [ObservableProperty]
    private SaveFilePreviewViewModel? _selectedRecentSave;
    
    [ObservableProperty]
    private SaveFilePreviewViewModel? _selectedBackupSave;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = LocalizedStrings.Instance["FolderList_Ready"];
    
    // Filter
    [ObservableProperty]
    private string _filterText = string.Empty;

    public Action? CloseRequested { get; set; }

    public FolderListViewModel(ISaveFileGateway saveFileService, AppSettings settings, IDialogService dialogService, bool loadOnConstruct = true)
    {
        _saveFileService = saveFileService;
        _settings = settings;
        _dialogService = dialogService;

        if (loadOnConstruct)
            _ = LoadSavesAsync(_scanCts.Token);
    }

    partial void OnFilterTextChanged(string value)
        => ApplyFilter();

    private void ApplyFilter()
    {
        var filter = FilterText.Trim();
        FilteredRecentSaves.Clear();
        FilteredBackupSaves.Clear();

        foreach (var save in RecentSaves.Where(x => MatchesFilter(x, filter)))
            FilteredRecentSaves.Add(save);
        foreach (var save in BackupSaves.Where(x => MatchesFilter(x, filter)))
            FilteredBackupSaves.Add(save);
    }

    private static bool MatchesFilter(SaveFilePreviewViewModel save, string filter)
    {
        if (filter.Length == 0)
            return true;

        return save.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || save.FilePath.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || save.Version.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || save.TrainerName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || save.PlayTime.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || save.BadgeCount.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadSavesAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        StatusText = LocalizedStrings.Instance["FolderList_Scanning"];

        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var backupPath = Path.Combine(baseDir, "bak");
            var recentFiles = _settings.Startup.RecentlyLoaded;
            var extraPaths = _settings.Backup.OtherBackupPaths;

            var (validRecents, validBackups) = await Task.Run(() =>
            {
                // 1. Recent Saves
                var validRecents = new List<SaveFilePreviewViewModel>();
                var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var path in recentFiles)
                    TryLoadPreview(path, validRecents, seenPaths, cancellationToken);

                // 2. Backup Saves
                var validBackups = new List<SaveFilePreviewViewModel>();
                var allBackupPaths = new List<string> { backupPath };
                allBackupPaths.AddRange(extraPaths);
                
                foreach (var folder in allBackupPaths.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!Directory.Exists(folder))
                        continue;

                    foreach (var path in EnumerateFilesSafe(folder, cancellationToken))
                    {
                        TryLoadPreview(path, validBackups, seenPaths, cancellationToken);
                    }
                }

                return (validRecents, validBackups);
            }, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Back on the UI thread (async command continuation) — assign the observable collections.
            RecentSaves.Clear();
            foreach (var save in validRecents)
                RecentSaves.Add(save);
            BackupSaves.Clear();
            foreach (var save in validBackups)
                BackupSaves.Add(save);
            ApplyFilter();
            StatusText = LocalizedStrings.Instance.Format("FolderList_Loaded", RecentSaves.Count, BackupSaves.Count);
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizedStrings.Instance["FolderList_Cancelled"];
        }
        catch (Exception ex)
        {
            StatusText = LocalizedStrings.Instance.Format("FolderList_Error", ex.Message);
        }
        finally
        {
            if (cancellationToken == _scanCts.Token)
                IsLoading = false;
        }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();

            string[] files;
            try { files = Directory.EnumerateFiles(directory).ToArray(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Continue scanning sibling directories when one path is inaccessible.
                files = [];
            }

            foreach (var file in files)
                yield return file;

            string[] children;
            try { children = Directory.EnumerateDirectories(directory).ToArray(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Continue scanning sibling directories when one path is inaccessible.
                children = [];
            }

            foreach (var child in children)
                pending.Push(child);
        }
    }

    private static void TryLoadPreview(string path, List<SaveFilePreviewViewModel> destination, HashSet<string> seenPaths, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (!File.Exists(path))
                return;

            var canonical = Path.GetFullPath(path);
            if (!seenPaths.Add(canonical))
                return;

            var info = new FileInfo(canonical);
            if (!SaveUtil.IsSizeValid(info.Length))
                return;

            var sav = SaveUtil.GetSaveFile(canonical);
            if (sav != null)
                destination.Add(new SaveFilePreviewViewModel(sav, canonical));
        }
        catch
        {
            // One inaccessible or malformed path must not abort the complete scan.
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        _scanCts.Cancel();
        _scanCts.Dispose();
        _scanCts = new CancellationTokenSource();
        await LoadSavesAsync(_scanCts.Token);
    }

    [RelayCommand]
    private void CancelScan() => _scanCts.Cancel();

    [RelayCommand]
    private void Close()
    {
        _scanCts.Cancel();
        CloseRequested?.Invoke();
    }

    public void Dispose()
    {
        _scanCts.Cancel();
        _scanCts.Dispose();
    }

    [RelayCommand]
    private async Task OpenSave(SaveFilePreviewViewModel? vm)
    {
        if (vm == null) return;
        
        // Use SaveFileService to load
        // But we already loaded it? 
        // SaveFileService usually takes a path and loads it.
        // Or we can pass the object if we have it fully loaded.
        // SaveUtil.GetSaveFile returns 'SaveFile' or 'SaveFile<T>'.
        
        // If we kept the SaveFile object in ViewModel (we should), pass it.
        // But usually we want to set it as CurrentSave.
        // Assuming _saveFileService has a method to set current save or load from path.
        
        await _saveFileService.LoadSaveFileAsync(vm.FilePath);
        
        // Close dialog/window? 
        // This is likely a dialog or a tab. If dialog, we can signal close.
        CloseRequested?.Invoke();
    }
    
    [RelayCommand]
    private void OpenFolder(SaveFilePreviewViewModel? vm)
    {
        if (vm == null || string.IsNullOrEmpty(vm.FilePath)) return;
        
        var folder = Path.GetDirectoryName(vm.FilePath);
        if (Directory.Exists(folder))
        {
            // Cross platform open folder?
            // Process.Start... or ILauncher?
            // For now simple Process.Start(folder) might work on Windows, less on Mac.
            // Avalonia usually needs a launcher service.
            // We'll leave a TODO or try simple dotnet ways.
            try 
            {
                if (OperatingSystem.IsWindows())
                    Process.Start("explorer.exe", folder);
                else if (OperatingSystem.IsMacOS())
                    Process.Start("open", folder);
                else if (OperatingSystem.IsLinux())
                    Process.Start("xdg-open", folder);
            }
            catch {}
        }
    }
}

public class SaveFilePreviewViewModel : ViewModelBase
{
    public string FileName { get; }
    public string FilePath { get; }
    public string Version { get; }
    public string TrainerName { get; }
    public string PlayTime { get; }
    public DateTime LastModified { get; }
    public string BadgeCount { get; }
    
    public SaveFilePreviewViewModel(SaveFile sav, string? sourcePath = null)
    {
        FilePath = sourcePath ?? sav.Metadata.FilePath ?? "Unknown";
        FileName = Path.GetFileName(FilePath);
        Version = sav.Version.ToString();
        TrainerName = sav.OT;
        PlayTime = $"{sav.PlayedHours:00}:{sav.PlayedMinutes:00}:{sav.PlayedSeconds:00}";
        LastModified = File.Exists(FilePath) ? File.GetLastWriteTime(FilePath) : DateTime.MinValue;
        BadgeCount = GetBadgeCount(sav);
    }

    private static string GetBadgeCount(SaveFile sav)
    {
        var property = sav.GetType().GetProperty("Badges");
        if (property is null)
            return string.Empty;

        try
        {
            var flags = Convert.ToUInt64(property.GetValue(sav));
            return sav is SAV8SWSH
                ? flags.ToString()
                : BitOperations.PopCount((ulong)flags).ToString();
        }
        catch
        {
            return string.Empty;
        }
    }
}
