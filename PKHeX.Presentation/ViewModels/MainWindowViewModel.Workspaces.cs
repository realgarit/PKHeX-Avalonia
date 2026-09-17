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

    private readonly ObservableCollection<ToolLauncherItem> _toolLauncherItems = [];

    public bool IsPokemonWorkspace => ActiveWorkspace == MainWorkspace.Pokemon;
    public bool IsSaveWorkspace => ActiveWorkspace == MainWorkspace.Save;
    public bool IsReportsWorkspace => ActiveWorkspace == MainWorkspace.Reports;
    public bool IsEventsWorkspace => IsSaveWorkspace && EventFlagsEditor?.IsSupported == true;
    public bool IsGiftsWorkspace => IsSaveWorkspace && MysteryGiftEditor?.HasAnySupport == true;
    public IReadOnlyList<ToolLauncherItem> ToolLauncherItems => _toolLauncherItems;
    public IEnumerable<ToolLauncherItem> FilteredToolLauncherItems =>
        string.IsNullOrWhiteSpace(ToolSearchText)
            ? _toolLauncherItems
            : _toolLauncherItems.Where(item => item.Matches(ToolSearchText));

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
        if (_toolLauncherItems.Count != 0)
            return;

        _toolLauncherItems.Add(new("Menu_Data_BoxReport", "Launcher_BoxReport_Description", "▤", OpenBoxReportCommand));
        _toolLauncherItems.Add(new("Menu_Data_LegalityAudit", "Launcher_Legality_Description", "✓", OpenLegalityAuditCommand));
        _toolLauncherItems.Add(new("Menu_Data_PKMDatabase", "Launcher_Database_Description", "▦", OpenPKMDatabaseCommand));
        _toolLauncherItems.Add(new("Menu_Data_EncounterDatabase", "Launcher_Encounter_Description", "⌁", OpenEncounterDatabaseCommand));
        _toolLauncherItems.Add(new("Menu_Tools_BatchEditor", "Launcher_Batch_Description", "≡", OpenBatchEditorCommand));
    }

    internal void RefreshToolLauncherLocalization()
    {
        foreach (var item in _toolLauncherItems)
            item.RefreshLocalization();
        OnPropertyChanged(nameof(FilteredToolLauncherItems));
    }
}

/// <summary>Presentation-layer item shared by the command launcher and future tool menus.</summary>
public sealed class ToolLauncherItem : ObservableObject
{
    private readonly string _titleKey;
    private readonly string _descriptionKey;

    public string Glyph { get; }
    public ICommand Command { get; }
    public string Title => LocalizedStrings.Instance[_titleKey];
    public string Description => LocalizedStrings.Instance[_descriptionKey];

    public ToolLauncherItem(string titleKey, string descriptionKey, string glyph, ICommand command)
    {
        _titleKey = titleKey;
        _descriptionKey = descriptionKey;
        Glyph = glyph;
        Command = command;
    }

    public bool Matches(string query) => Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || Description.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
    }
}
