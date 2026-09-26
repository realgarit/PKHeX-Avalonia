using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class MainWindowViewModel
{
    // MenuItem.CommandParameter is an object. Keep the workspace-tab values typed so Avalonia's
    // MenuItem can call RelayCommand<int>.CanExecute while a submenu opens; a XAML attribute such as
    // CommandParameter="0" remains a string and throws before the user can select the menu item.
    public const int BoxWorkspaceTabIndex = 0;
    public const int PartyWorkspaceTabIndex = 1;
    public const int TrainerWorkspaceTabIndex = 2;
    public const int InventoryWorkspaceTabIndex = 3;

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
    private readonly ObservableCollection<ToolMenuGroup> _toolMenuGroups = [];

    public bool IsPokemonWorkspace => ActiveWorkspace == MainWorkspace.Pokemon;
    public bool IsSaveWorkspace => ActiveWorkspace == MainWorkspace.Save;
    public bool IsReportsWorkspace => ActiveWorkspace == MainWorkspace.Reports;
    public bool IsTrainerNavigationSelected => IsSaveWorkspace && SelectedWorkspaceIndex == TrainerWorkspaceTabIndex;
    public bool IsInventoryNavigationSelected => IsSaveWorkspace && SelectedWorkspaceIndex == InventoryWorkspaceTabIndex;
    public bool IsSaveNavigationSelected => IsSaveWorkspace && SelectedWorkspaceIndex >= 4;
    public bool IsEventsWorkspace => IsSaveWorkspace && EventFlagsEditor?.IsSupported == true;
    public bool IsGiftsWorkspace => IsSaveWorkspace && MysteryGiftEditor?.HasAnySupport == true;
    public IReadOnlyList<ToolLauncherItem> ToolLauncherItems => _capabilityRegistry;
    public IReadOnlyList<ToolMenuGroup> ToolMenuGroups => _toolMenuGroups;
    public IEnumerable<ToolMenuGroup> AvailableToolMenuGroups => _toolMenuGroups.Where(group => group.IsAvailable);
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
        NotifyWorkspaceNavigation();
    }

    partial void OnSelectedWorkspaceIndexChanged(int value)
    {
        if (value <= 1 && ActiveWorkspace != MainWorkspace.Pokemon)
            ActiveWorkspace = MainWorkspace.Pokemon;
        else if (value >= 2 && ActiveWorkspace != MainWorkspace.Save)
            ActiveWorkspace = MainWorkspace.Save;
        NotifyWorkspaceNavigation();
    }

    private void NotifyWorkspaceNavigation()
    {
        OnPropertyChanged(nameof(IsTrainerNavigationSelected));
        OnPropertyChanged(nameof(IsInventoryNavigationSelected));
        OnPropertyChanged(nameof(IsSaveNavigationSelected));
    }

    [RelayCommand]
    private void SelectSaveNavigation() => SelectWorkspaceTab(
        EventFlagsEditor?.IsSupported == true ? 4 : MysteryGiftEditor?.HasAnySupport == true ? 5 : 6);

    [RelayCommand]
    private void SelectWorkspace(MainWorkspace workspace) => ActiveWorkspace = workspace;

    [RelayCommand]
    private void SelectWorkspaceTab(int index)
    {
        ActiveWorkspace = index <= 1 ? MainWorkspace.Pokemon : MainWorkspace.Save;
        SelectedWorkspaceIndex = index;
    }

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
            "Dialog_SaveFolderList",
            string.Empty,
            "M 3,3 H 13 V 13 H 3 Z M 6,6 H 10 M 6,9 H 10 M 6,12 H 8",
            OpenFolderListCommand,
            menuGroupTitleKey: "Menu_Tools_Data",
            showInLauncher: true));

        _capabilityRegistry.Add(new(
            "Menu_Data_BoxReport",
            "Launcher_BoxReport_Description",
            "M 2,3 H 14 V 14 H 2 Z M 5,6 H 11 M 5,9 H 11 M 5,12 H 8",
            OpenBoxReportCommand,
            menuGroupTitleKey: "Menu_Tools_Data",
            showInReports: true));
        _capabilityRegistry.Add(new(
            "Menu_Data_LegalityAudit",
            "Launcher_Legality_Description",
            "M 8,1 L 14,3 V 8 C 14,12 11,15 8,17 C 5,15 2,12 2,8 V 3 Z M 5,8 L 7,10 L 11,6",
            OpenLegalityAuditCommand,
            menuGroupTitleKey: "Menu_Tools_Data",
            showInReports: true));
        _capabilityRegistry.Add(new(
            "Menu_Data_PKMDatabase",
            "Launcher_Database_Description",
            "M 2,4 C 2,2 14,2 14,4 V 14 C 14,16 2,16 2,14 Z M 2,4 C 2,6 14,6 14,4 M 2,9 C 2,11 14,11 14,9",
            OpenPKMDatabaseCommand,
            menuGroupTitleKey: "Menu_Tools_Data",
            showInReports: true));
        _capabilityRegistry.Add(new(
            "Menu_Data_EncounterDatabase",
            "Launcher_Encounter_Description",
            "M 3,3 H 13 V 13 H 3 Z M 6,6 H 10 M 6,9 H 10 M 6,12 H 8",
            OpenEncounterDatabaseCommand,
            menuGroupTitleKey: "Menu_Tools_Data",
            showInReports: true));
        _capabilityRegistry.Add(new(
            "Menu_Tools_BatchEditor",
            "Launcher_Batch_Description",
            "M 2,3 H 14 V 5 H 2 Z M 2,8 H 14 V 10 H 2 Z M 2,13 H 14 V 15 H 2 Z",
            OpenBatchEditorCommand,
            menuGroupTitleKey: "Menu_Tools_SaveEditors",
            showInReports: true));

        RegisterMenuCapability("Menu_Tools_OpenBoxWorkspace", OpenBoxWorkspaceCommand, "Workspace_SectionLabel");
        RegisterMenuCapability("Menu_Tools_OpenPartyWorkspace", OpenPartyWorkspaceCommand, "Workspace_SectionLabel");

        RegisterMenuCapability("Menu_Showdown_ImportSet", ImportShowdownCommand, "Menu_Pokemon");
        RegisterMenuCapability("Menu_Showdown_ExportSet", ExportShowdownCommand, "Menu_Pokemon");
        RegisterMenuCapability("Menu_Showdown_ImportTeam", ImportShowdownTeamCommand, "Menu_Pokemon");
        RegisterMenuCapability("Menu_Showdown_ExportBox", ExportShowdownBoxCommand, "Menu_Pokemon");
        RegisterMenuCapability("Menu_Showdown_ExportAllBoxes", ExportShowdownAllBoxesCommand, "Menu_Pokemon");
        RegisterMenuCapability("Menu_Tools_AutoLegalityMod", OpenAutoLegalityModCommand, "Menu_Pokemon");
        RegisterMenuCapability("Menu_QR_ShowCurrent", ShowQrCodeCommand, "Menu_Pokemon");
        RegisterMenuCapability("Menu_QR_ImportImage", ImportQrCodeCommand, "Menu_Pokemon");

        RegisterMenuCapability("Menu_Data_LoadBoxes", LoadBoxesCommand, "Menu_Tools_Data");
        RegisterMenuCapability("Menu_Data_DumpBoxes", DumpBoxesCommand, "Menu_Tools_Data");
        RegisterMenuCapability("Menu_Data_MysteryGiftDatabase", OpenMysteryGiftDatabaseCommand, "Menu_Tools_Data");
        RegisterMenuCapability("Menu_Data_BlockEditor", OpenBlockEditorCommand, "Menu_Tools_Data");

        RegisterMenuCapability("Menu_Tools_SaveHandlerTroubleshooter", OpenSaveHandlerTroubleshooterCommand, "Menu_Tools_SaveEditors");
        RegisterMenuCapability("Menu_Tools_LiveHeX", OpenLiveHeXCommand, "Menu_Tools_SaveEditors");
        RegisterMenuCapability("Menu_Tools_GenerateLivingDex", OpenLivingDexGeneratorCommand, "Menu_Tools_SaveEditors");
        RegisterMenuCapability("Menu_Tools_BackupManager", OpenBackupManagerCommand, "Menu_Tools_SaveEditors");
        RegisterMenuCapability("Menu_Tools_CompareSaves", OpenSaveDiffCommand, "Menu_Tools_SaveEditors");
        RegisterMenuCapability("Menu_Tools_BoxManipulation", OpenBoxManipCommand, "Menu_Tools_SaveEditors");
        RegisterMenuCapability("Menu_Save_Daycare", OpenDaycareCommand, "Menu_Tools_SaveEditors");
        RegisterMenuCapability("Menu_Save_Records", OpenRecordsCommand, "Menu_Tools_SaveEditors");
        RegisterMenuCapability("Menu_Save_HallOfFame", OpenHallOfFameCommand, "Menu_Tools_SaveEditors");
        RegisterMenuCapability("Menu_Save_MailBox", OpenMailBoxCommand, "Menu_Tools_SaveEditors");
        RegisterMenuCapability("Menu_Save_BoxLayout", OpenBoxLayoutCommand, "Menu_Tools_SaveEditors");

        RegisterMenuCapability("Menu_Gen1_EventReset", OpenEventReset1Command, "Menu_Gen1");
        RegisterMenuCapability("Menu_Save_HallOfFame", OpenHallOfFame1Command, "Menu_Gen1");
        RegisterMenuCapability("Menu_Gen2_EventFlags", OpenEventFlags2Command, "Menu_Gen2");
        RegisterMenuCapability("Menu_Gen2_Misc", OpenMisc2Command, "Menu_Gen2");

        RegisterMenuCapability("Menu_Misc", OpenMisc3Command, "Menu_Gen3");
        RegisterMenuCapability("Menu_Roamer", OpenRoamer3Command, "Menu_Gen3");
        RegisterMenuCapability("Menu_Gen3_SecretBase", OpenSecretBaseCommand, "Menu_Gen3");
        RegisterMenuCapability("Menu_Gen3_RTC", OpenRTC3Command, "Menu_Gen3");
        RegisterMenuCapability("Menu_Gen3_PokeblockCase", OpenPokeBlock3CaseCommand, "Menu_Gen3");
        RegisterMenuCapability("Menu_Save_HallOfFame", OpenHallOfFame3Command, "Menu_Gen3");

        RegisterMenuCapability("Menu_Misc", OpenMisc4Command, "Menu_Gen4");
        RegisterMenuCapability("Menu_Gen4_PoffinsDPPt", OpenPoffinCaseCommand, "Menu_Gen4");
        RegisterMenuCapability("Menu_Gen4_PoketchDPPt", OpenPoketchCommand, "Menu_Gen4");
        RegisterMenuCapability("Menu_Gen4_BlockLayout", OpenBoxLayoutCommand, "Menu_Gen4");
        RegisterMenuCapability("Menu_Gen4_HoneyTrees", OpenHoneyTreeCommand, "Menu_Gen4");
        RegisterMenuCapability("Menu_Gen4_PokeGearHGSS", OpenPokeGear4Command, "Menu_Gen4");
        RegisterMenuCapability("Menu_Gen4_PokeathlonHGSS", OpenPokeathlonCommand, "Menu_Gen4");
        RegisterMenuCapability("Menu_Gen4_GeonetDPPt", OpenGeonet4Command, "Menu_Gen4");
        RegisterMenuCapability("Menu_Gen4_BattlePassPBR", OpenBattlePassCommand, "Menu_Gen4");
        RegisterMenuCapability("Menu_Pokedex", OpenPokedexCommand, "Menu_Gen4");

        RegisterMenuCapability("Menu_Misc", OpenMisc5Command, "Menu_Gen5");
        RegisterMenuCapability("Menu_Pokedex", OpenPokedexCommand, "Menu_Gen5");
        RegisterMenuCapability("Menu_Gen5_Chatter", OpenChatterCommand, "Menu_Gen5");
        RegisterMenuCapability("Menu_Gen5_MedalRally", OpenMedalCommand, "Menu_Gen5");
        RegisterMenuCapability("Menu_Gen5_JoinAvenue", OpenJoinAvenueCommand, "Menu_Gen5");
        RegisterMenuCapability("Menu_Gen5_UnityTower", OpenUnityTower5Command, "Menu_Gen5");
        RegisterMenuCapability("Menu_Gen5_EntralinkForest", OpenEntralinkCommand, "Menu_Gen5");
        RegisterMenuCapability("Menu_Gen5_GlobalLink", OpenGlobalLink5Command, "Menu_Gen5");
        RegisterMenuCapability("Menu_Gen5_DLC", OpenDLC5Command, "Menu_Gen5");

        RegisterMenuCapability("Menu_Pokedex", OpenPokedexCommand, "Menu_Gen6");
        RegisterMenuCapability("Menu_Gen6_OPowers", OpenOPowerCommand, "Menu_Gen6");
        _capabilityRegistry.Add(new("FriendSafari_Title", string.Empty, string.Empty,
            UnlockFriendSafariCommand, menuGroupTitleKey: "Menu_Gen6",
            capabilityPredicate: () => CurrentSave is SAV6XY));
        RegisterMenuCapability("Menu_Gen6_SuperTraining", OpenSuperTrainingCommand, "Menu_Gen6");
        RegisterMenuCapability("Menu_Roamer", OpenRoamer6Command, "Menu_Gen6");
        RegisterMenuCapability("Menu_Gen6_PokemonLink", OpenLink6Command, "Menu_Gen6");
        RegisterMenuCapability("Menu_Gen6_SecretBaseEditor", OpenSecretBase6Command, "Menu_Gen6");

        RegisterMenuCapability("Menu_Misc", OpenMisc7Command, "Menu_Gen7");
        RegisterMenuCapability("Menu_Pokedex", OpenPokedexCommand, "Menu_Gen7");
        RegisterMenuCapability("Menu_Gen7_PokeBeans", OpenPokebeanCommand, "Menu_Gen7");
        RegisterMenuCapability("Menu_Gen7_FestivalPlaza", OpenFestivalPlazaCommand, "Menu_Gen7");
        RegisterMenuCapability("Menu_Gen7_ZygardeCells", OpenZygardeCellCommand, "Menu_Gen7");
        RegisterMenuCapability("Menu_Gen7_CaptureRecordsLGPE", OpenCapture7GGCommand, "Menu_Gen7");
        RegisterMenuCapability("Menu_Gen7_HallOfFameSMUSUM", OpenHallOfFame7Command, "Menu_Gen7");

        RegisterMenuCapability("Menu_Pokedex", OpenPokedexCommand, "Menu_Gen8");
        RegisterMenuCapability("Menu_Gen8_MaxRaidsSWSH", OpenRaidEditorCommand, "Menu_Gen8");
        RegisterMenuCapability("Menu_Gen8_FashionSWSH", OpenFashionCommand, "Menu_Gen8");
        RegisterMenuCapability("Menu_Gen8_TrainerCardSWSH", OpenTrainerCard8Command, "Menu_Gen8");
        RegisterMenuCapability("Menu_Gen8_MiscBDSP", OpenMisc8bCommand, "Menu_Gen8");
        RegisterMenuCapability("Menu_Gen8_UndergroundBDSP", OpenUnderground8bCommand, "Menu_Gen8");
        RegisterMenuCapability("Menu_Gen8_SealStickersBDSP", OpenSealStickers8bCommand, "Menu_Gen8");
        RegisterMenuCapability("Menu_Gen8_PoffinsBDSP", OpenPoffin8bCommand, "Menu_Gen8");

        RegisterMenuCapability("Menu_Misc", OpenMisc9Command, "Menu_Gen9");
        RegisterMenuCapability("Menu_Pokedex", OpenPokedexCommand, "Menu_Gen9");
        RegisterMenuCapability("Menu_Gen9_TeraRaidsSV", OpenRaid9Command, "Menu_Gen9");
        RegisterMenuCapability("Menu_Gen9_SevenStarRaidsSV", OpenRaidSevenStar9Command, "Menu_Gen9");
        RegisterMenuCapability("Menu_Gen9_FashionSVZA", OpenFashion9Command, "Menu_Gen9");
        RegisterMenuCapability("Menu_Gen9_DonutPLZA", OpenDonutCommand, "Menu_Gen9");

        InitializeToolMenuGroups();
    }

    private void RegisterMenuCapability(string titleKey, ICommand command, string menuGroupTitleKey, bool showInLauncher = false)
    {
        _capabilityRegistry.Add(new(
            titleKey,
            string.Empty,
            string.Empty,
            command,
            showInLauncher: showInLauncher,
            menuGroupTitleKey: menuGroupTitleKey,
            capabilityPredicate: GetMenuCapabilityPredicate(menuGroupTitleKey)));
    }

    private void InitializeToolMenuGroups()
    {
        if (_toolMenuGroups.Count != 0)
            return;

        var grouped = _capabilityRegistry
            .Where(item => item.MenuGroupTitleKey is not null)
            .GroupBy(item => item.MenuGroupTitleKey!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group, StringComparer.Ordinal);
        var order = new[]
        {
            "Workspace_SectionLabel",
            "Menu_Pokemon",
            "Menu_Tools_Data",
            "Menu_Tools_SaveEditors",
            "Menu_Gen1",
            "Menu_Gen2",
            "Menu_Gen3",
            "Menu_Gen4",
            "Menu_Gen5",
            "Menu_Gen6",
            "Menu_Gen7",
            "Menu_Gen8",
            "Menu_Gen9",
        };

        foreach (var key in order)
        {
            if (grouped.Remove(key, out var group))
                _toolMenuGroups.Add(new ToolMenuGroup(group.Key, group));
        }

        // Preserve forward compatibility if a future descriptor introduces a new menu group but
        // forgets to add it to the explicit presentation order above.
        foreach (var group in grouped.Values)
            _toolMenuGroups.Add(new ToolMenuGroup(group.Key, group));
    }

    private Func<bool>? GetMenuCapabilityPredicate(string menuGroupTitleKey) => menuGroupTitleKey switch
    {
        "Menu_Gen1" => () => CurrentSave?.Generation == 1,
        "Menu_Gen2" => () => CurrentSave?.Generation == 2,
        "Menu_Gen3" => () => CurrentSave?.Generation == 3,
        "Menu_Gen4" => () => CurrentSave?.Generation == 4,
        "Menu_Gen5" => () => CurrentSave?.Generation == 5,
        "Menu_Gen6" => () => CurrentSave?.Generation == 6,
        "Menu_Gen7" => () => CurrentSave?.Generation == 7,
        "Menu_Gen8" => () => CurrentSave?.Generation == 8,
        "Menu_Gen9" => () => CurrentSave?.Generation == 9,
        _ => null,
    };

    internal void RefreshToolLauncherLocalization()
    {
        foreach (var item in _capabilityRegistry)
            item.RefreshLocalization();
        foreach (var group in _toolMenuGroups)
            group.RefreshLocalization();
        OnPropertyChanged(nameof(FilteredToolLauncherItems));
        OnPropertyChanged(nameof(ReportToolItems));
        OnPropertyChanged(nameof(AvailableToolMenuGroups));
    }

    internal void RefreshCapabilityAvailability()
    {
        foreach (var item in _capabilityRegistry)
            item.RefreshAvailability();
        foreach (var group in _toolMenuGroups)
            group.RefreshAvailability();
        OnPropertyChanged(nameof(FilteredToolLauncherItems));
        OnPropertyChanged(nameof(ReportToolItems));
        OnPropertyChanged(nameof(AvailableToolMenuGroups));
    }
}

/// <summary>Presentation-layer descriptor shared by the command launcher and Reports surface.</summary>
public sealed class ToolLauncherItem : ObservableObject
{
    private readonly string _titleKey;
    private readonly string _descriptionKey;
    private readonly Func<bool>? _capabilityPredicate;
    private bool _isAvailable;

    public string IconData { get; }
    public ICommand Command { get; }
    public bool ShowInLauncher { get; }
    public bool ShowInReports { get; }
    public string? MenuGroupTitleKey { get; }
    public bool IsAvailable => _isAvailable;
    public string Title => LocalizedStrings.Instance[_titleKey];
    public string Description => string.IsNullOrEmpty(_descriptionKey)
        ? string.Empty
        : LocalizedStrings.Instance[_descriptionKey];

    public ToolLauncherItem(
        string titleKey,
        string descriptionKey,
        string iconData,
        ICommand command,
        bool showInLauncher = true,
        bool showInReports = false,
        string? menuGroupTitleKey = null,
        Func<bool>? capabilityPredicate = null)
    {
        _titleKey = titleKey;
        _descriptionKey = descriptionKey;
        IconData = iconData;
        Command = command;
        ShowInLauncher = showInLauncher;
        ShowInReports = showInReports;
        MenuGroupTitleKey = menuGroupTitleKey;
        _capabilityPredicate = capabilityPredicate;
        _isAvailable = command.CanExecute(null) && (capabilityPredicate?.Invoke() ?? true);
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
        var available = Command.CanExecute(null) && (_capabilityPredicate?.Invoke() ?? true);
        if (_isAvailable == available)
            return;

        _isAvailable = available;
        OnPropertyChanged(nameof(IsAvailable));
    }
}

/// <summary>One visible submenu in the capability-driven Tools menu.</summary>
public sealed class ToolMenuGroup : ObservableObject
{
    private readonly string _titleKey;

    public IReadOnlyList<ToolLauncherItem> Items { get; }
    public string Title => LocalizedStrings.Instance[_titleKey];
    public bool IsAvailable => Items.Any(item => item.IsAvailable);

    public ToolMenuGroup(string titleKey, IEnumerable<ToolLauncherItem> items)
    {
        _titleKey = titleKey;
        Items = items.ToArray();
    }

    public void RefreshLocalization() => OnPropertyChanged(nameof(Title));

    public void RefreshAvailability() => OnPropertyChanged(nameof(IsAvailable));
}
