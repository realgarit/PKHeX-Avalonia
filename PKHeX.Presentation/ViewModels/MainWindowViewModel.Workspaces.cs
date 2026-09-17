using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class MainWindowViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPokemonWorkspace))]
    [NotifyPropertyChangedFor(nameof(IsSaveWorkspace))]
    [NotifyPropertyChangedFor(nameof(IsReportsWorkspace))]
    [NotifyPropertyChangedFor(nameof(IsEventsWorkspace))]
    [NotifyPropertyChangedFor(nameof(IsGiftsWorkspace))]
    private MainWorkspace _activeWorkspace = MainWorkspace.Pokemon;

    /// <summary>Selected tab in the existing save-level workspace control.</summary>
    [ObservableProperty] private int _selectedWorkspaceIndex;

    [ObservableProperty] private bool _isToolLauncherOpen;
    [ObservableProperty] private string _toolSearchText = string.Empty;

    // One registry feeds the launcher and Reports surface. The same descriptor owns the title,
    // description, command, availability, and icon data so those surfaces cannot drift apart.
    private readonly ObservableCollection<ToolLauncherItem> _capabilityRegistry = [];

    public bool IsPokemonWorkspace => ActiveWorkspace == MainWorkspace.Pokemon;
    public bool IsSaveWorkspace => ActiveWorkspace == MainWorkspace.Save;
    public bool IsReportsWorkspace => ActiveWorkspace == MainWorkspace.Reports;
    public bool IsEventsWorkspace => IsSaveWorkspace && EventFlagsEditor?.IsSupported == true;
    public bool IsGiftsWorkspace => IsSaveWorkspace && MysteryGiftEditor?.HasAnySupport == true;
    public IReadOnlyList<ToolLauncherItem> ToolLauncherItems => _capabilityRegistry;
    public IEnumerable<ToolLauncherItem> ReportToolItems =>
        _capabilityRegistry.Where(item => item.ShowInReports && item.IsAvailable);
    public IEnumerable<ToolLauncherItem> FilteredToolLauncherItems =>
        string.IsNullOrWhiteSpace(ToolSearchText)
            ? _capabilityRegistry.Where(item => item.ShowInLauncher && item.IsAvailable)
            : _capabilityRegistry.Where(item => item.ShowInLauncher && item.IsAvailable && item.Matches(ToolSearchText));

    partial void OnActiveWorkspaceChanged(MainWorkspace value)
    {
        if (value == MainWorkspace.Pokemon && SelectedWorkspaceIndex > 1)
            SelectedWorkspaceIndex = 0;
        else if (value == MainWorkspace.Save && SelectedWorkspaceIndex < 2)
            SelectedWorkspaceIndex = 2;
    }

    partial void OnSelectedWorkspaceIndexChanged(int value)
    {
        if (value <= 1 && ActiveWorkspace != MainWorkspace.Pokemon)
            ActiveWorkspace = MainWorkspace.Pokemon;
        else if (value >= 2 && ActiveWorkspace != MainWorkspace.Save)
            ActiveWorkspace = MainWorkspace.Save;
    }

    [RelayCommand]
    private void SelectWorkspace(MainWorkspace workspace) => ActiveWorkspace = workspace;

    [RelayCommand]
    private void SelectWorkspaceTab(int index) => SelectedWorkspaceIndex = index;

    [RelayCommand]
    private void OpenToolLauncher()
    {
        ToolSearchText = string.Empty;
        IsToolLauncherOpen = true;
    }

    [RelayCommand]
    private void CloseToolLauncher()
    {
        ToolSearchText = string.Empty;
        IsToolLauncherOpen = false;
    }

    partial void OnToolSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredToolLauncherItems));

    [RelayCommand(CanExecute = nameof(HasSave))]
    private void OpenBoxWorkspace()
    {
        if (BoxViewer is not null)
            _windowService.ShowTool(BoxViewer, LocalizedStrings.Instance["Tab_Box"]);
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private void OpenPartyWorkspace()
    {
        if (PartyViewer is not null)
            _windowService.ShowTool(PartyViewer, LocalizedStrings.Instance["Tab_Party"]);
    }

    internal void InitializeToolLauncherItems()
    {
        if (_capabilityRegistry.Count != 0)
            return;

        _capabilityRegistry.Add(new(
            "Menu_Data_BoxReport",
            "Launcher_BoxReport_Description",
            "M 2,3 H 14 V 14 H 2 Z M 5,6 H 11 M 5,9 H 11 M 5,12 H 8",
            OpenBoxReportCommand,
            showInReports: true));
        _capabilityRegistry.Add(new(
            "Menu_Data_LegalityAudit",
            "Launcher_Legality_Description",
            "M 8,1 L 14,3 V 8 C 14,12 11,15 8,17 C 5,15 2,12 2,8 V 3 Z M 5,8 L 7,10 L 11,6",
            OpenLegalityAuditCommand,
            showInReports: true));
        _capabilityRegistry.Add(new(
            "Menu_Data_PKMDatabase",
            "Launcher_Database_Description",
            "M 2,4 C 2,2 14,2 14,4 V 14 C 14,16 2,16 2,14 Z M 2,4 C 2,6 14,6 14,4 M 2,9 C 2,11 14,11 14,9",
            OpenPKMDatabaseCommand,
            showInReports: true));
        _capabilityRegistry.Add(new(
            "Menu_Data_EncounterDatabase",
            "Launcher_Encounter_Description",
            "M 3,3 H 13 V 13 H 3 Z M 6,6 H 10 M 6,9 H 10 M 6,12 H 8",
            OpenEncounterDatabaseCommand,
            showInReports: true));
        _capabilityRegistry.Add(new(
            "Menu_Tools_BatchEditor",
            "Launcher_Batch_Description",
            "M 2,3 H 14 V 5 H 2 Z M 2,8 H 14 V 10 H 2 Z M 2,13 H 14 V 15 H 2 Z",
            OpenBatchEditorCommand,
            showInReports: true));
    }

    internal void RefreshToolLauncherLocalization()
    {
        foreach (var item in _capabilityRegistry)
            item.RefreshLocalization();
        OnPropertyChanged(nameof(FilteredToolLauncherItems));
        OnPropertyChanged(nameof(ReportToolItems));
    }

    internal void RefreshCapabilityAvailability()
    {
        foreach (var item in _capabilityRegistry)
            item.RefreshAvailability();
        OnPropertyChanged(nameof(FilteredToolLauncherItems));
        OnPropertyChanged(nameof(ReportToolItems));
    }
}

/// <summary>Presentation-layer descriptor shared by the command launcher and Reports surface.</summary>
public sealed class ToolLauncherItem : ObservableObject
{
    private readonly string _titleKey;
    private readonly string _descriptionKey;
    private bool _isAvailable;

    public string IconData { get; }
    public ICommand Command { get; }
    public bool ShowInLauncher { get; }
    public bool ShowInReports { get; }
    public bool IsAvailable => _isAvailable;
    public string Title => LocalizedStrings.Instance[_titleKey];
    public string Description => LocalizedStrings.Instance[_descriptionKey];

    public ToolLauncherItem(
        string titleKey,
        string descriptionKey,
        string iconData,
        ICommand command,
        bool showInLauncher = true,
        bool showInReports = false)
    {
        _titleKey = titleKey;
        _descriptionKey = descriptionKey;
        IconData = iconData;
        Command = command;
        ShowInLauncher = showInLauncher;
        ShowInReports = showInReports;
        _isAvailable = command.CanExecute(null);
    }

    public bool Matches(string query) => Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || Description.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
    }

    public void RefreshAvailability()
    {
        var available = Command.CanExecute(null);
        if (_isAvailable == available)
            return;

        _isAvailable = available;
        OnPropertyChanged(nameof(IsAvailable));
    }
}
