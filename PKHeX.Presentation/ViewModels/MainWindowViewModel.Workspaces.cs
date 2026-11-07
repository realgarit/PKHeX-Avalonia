using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Application.Shortcuts;
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
    public const int EventsWorkspaceTabIndex = 4;
    public const int GiftsWorkspaceTabIndex = 5;
    public const int BatchWorkspaceTabIndex = 6;

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
    public bool IsEventsNavigationSelected => IsSaveWorkspace && SelectedWorkspaceIndex == EventsWorkspaceTabIndex;
    public bool IsGiftsNavigationSelected => IsSaveWorkspace && SelectedWorkspaceIndex == GiftsWorkspaceTabIndex;
    public bool IsBatchNavigationSelected => IsSaveWorkspace && SelectedWorkspaceIndex == BatchWorkspaceTabIndex;
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
        OnPropertyChanged(nameof(IsEventsNavigationSelected));
        OnPropertyChanged(nameof(IsGiftsNavigationSelected));
        OnPropertyChanged(nameof(IsBatchNavigationSelected));
    }

    [RelayCommand]
    private void SelectSaveNavigation() => SelectWorkspaceTab(
        EventFlagsEditor?.IsSupported == true ? 4 : MysteryGiftEditor?.HasAnySupport == true ? 5 : 6);

    [RelayCommand]
    private void SelectWorkspace(MainWorkspace workspace) => ActiveWorkspace = workspace;

    /// <summary>
    /// True when the workspace tab can show a working editor for the current save. Events and Gifts
    /// are hidden for saves without that storage, so navigation never lands on a dead-end page.
    /// </summary>
    public bool IsWorkspaceTabAvailable(int index) => index switch
    {
        EventsWorkspaceTabIndex => EventFlagsEditor?.IsSupported == true,
        GiftsWorkspaceTabIndex => MysteryGiftEditor?.HasAnySupport == true,
        _ => index is >= BoxWorkspaceTabIndex and <= BatchWorkspaceTabIndex,
    };

    [RelayCommand]
    private void SelectWorkspaceTab(int index)
    {
        if (!IsWorkspaceTabAvailable(index))
            return;
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
            BoxViewer.OpenDetachedToolCommand.Execute(null);
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private void OpenPartyWorkspace()
    {
        PartyViewer?.OpenDetachedToolCommand.Execute(null);
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
        RegisterMenuCapability("Menu_Tools_OpenPokemonEditorWorkspace", OpenPokemonEditorWorkspaceCommand, "Workspace_SectionLabel");

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
        RegisterMenuCapability("Menu_Save_Daycare", OpenDaycareCommand, "Menu_Tools_SaveEditors",
            applies: SaveEditorCapabilities.HasDaycare);
        RegisterMenuCapability("Menu_Save_Records", OpenRecordsCommand, "Menu_Tools_SaveEditors",
            applies: SaveEditorCapabilities.HasRecords);
        _capabilityRegistry.Add(new(
            "Menu_Save_GiftRibbons",
            string.Empty,
            "M 3,3 H 13 V 13 H 3 Z M 5,6 H 11 M 5,9 H 11 M 5,12 H 9",
            OpenGiftRibbonsCommand,
            showInLauncher: true,
            menuGroupTitleKey: "Menu_Tools_SaveEditors",
            capabilityPredicate: CanOpenGiftRibbons));
        RegisterMenuCapability("Menu_Save_HallOfFame", OpenHallOfFameCommand, "Menu_Tools_SaveEditors",
            applies: static s => s is SAV6XY or SAV6AO);
        RegisterMenuCapability("Menu_Save_MailBox", OpenMailBoxCommand, "Menu_Tools_SaveEditors",
            applies: static s => s is SAV2 or SAV3 or SAV4 or SAV5);
        RegisterMenuCapability("Menu_Save_BoxLayout", OpenBoxLayoutCommand, "Menu_Tools_SaveEditors",
            applies: SaveEditorCapabilities.HasBoxLayout);
        RegisterConditionalMenuCapability("RsBoxWallpaper_Title", OpenRsBoxWallpaperCommand,
            "Menu_Gen3", () => CurrentSave is SAV3RSBox);
        RegisterConditionalMenuCapability("Dialog_GroupViewer", OpenGroupViewerCommand,
            "Menu_Tools_SaveEditors", () => CurrentSave is SAV_STADIUM);
        RegisterConditionalMenuCapability("Dialog_SimpleTrainerEditor", OpenSimpleTrainerCommand,
            "Menu_Tools_SaveEditors", () => CurrentSave is SAV1 or SAV2 or SAV3 or SAV4 or SAV5);

        RegisterMenuCapability("Menu_Gen1_EventReset", OpenEventReset1Command, "Menu_Gen1",
            applies: static s => s is SAV1);
        RegisterMenuCapability("Menu_Save_HallOfFame", OpenHallOfFame1Command, "Menu_Gen1",
            applies: static s => s is SAV1);
        RegisterMenuCapability("Menu_Gen2_EventFlags", OpenEventFlags2Command, "Menu_Gen2",
            applies: static s => s is SAV2);
        RegisterMenuCapability("Menu_Gen2_Misc", OpenMisc2Command, "Menu_Gen2",
            applies: static s => s is SAV2);

        RegisterMenuCapability("Menu_Misc", OpenMisc3Command, "Menu_Gen3",
            applies: static s => s is SAV3);
        RegisterConditionalMenuCapability("Ticket3Flow_Title", OpenGen3TicketsCommand, "Menu_Gen3", () => CurrentSave is SAV3RS or SAV3E or SAV3FRLG);
        RegisterMenuCapability("Menu_Roamer", OpenRoamer3Command, "Menu_Gen3",
            applies: static s => s is SAV3);
        RegisterMenuCapability("Menu_Gen3_SecretBase", OpenSecretBaseCommand, "Menu_Gen3",
            applies: static s => s is SAV3RS or SAV3E);
        RegisterMenuCapability("Menu_Gen3_RTC", OpenRTC3Command, "Menu_Gen3",
            applies: static s => s is SAV3RS or SAV3E);
        RegisterMenuCapability("Menu_Gen3_PokeblockCase", OpenPokeBlock3CaseCommand, "Menu_Gen3",
            applies: static s => s is SAV3RS or SAV3E);
        RegisterMenuCapability("Menu_Save_HallOfFame", OpenHallOfFame3Command, "Menu_Gen3",
            applies: static s => s is SAV3);
        RegisterConditionalMenuCapability("Dialog_SecretBaseEditorRSE", OpenSecretBase3Command,
            "Menu_Gen3", () => CurrentSave is SAV3RS or SAV3E);
        RegisterConditionalMenuCapability("Dialog_RTCEditor", OpenRTCCommand,
            "Menu_Gen3", () => CurrentSave is SAV3RS or SAV3E);

        RegisterMenuCapability("Menu_Misc", OpenMisc4Command, "Menu_Gen4",
            applies: static s => s is SAV4);
        RegisterConditionalMenuCapability("Cosmetic4_Title", OpenCosmeticInventory4Command,
            "Menu_Gen4", () => CurrentSave is SAV4);
        RegisterMenuCapability("Menu_Gen4_PoffinsDPPt", OpenPoffinCaseCommand, "Menu_Gen4",
            applies: static s => s is SAV4Sinnoh);
        RegisterMenuCapability("Menu_Gen4_PoketchDPPt", OpenPoketchCommand, "Menu_Gen4",
            applies: static s => s is SAV4Sinnoh);
        RegisterMenuCapability("Menu_Gen4_BlockLayout", OpenBoxLayoutCommand, "Menu_Gen4",
            applies: SaveEditorCapabilities.HasBoxLayout);
        RegisterMenuCapability("Menu_Gen4_HoneyTrees", OpenHoneyTreeCommand, "Menu_Gen4",
            applies: static s => s is SAV4Sinnoh);
        RegisterMenuCapability("Menu_Gen4_PokeGearHGSS", OpenPokeGear4Command, "Menu_Gen4",
            applies: static s => s is SAV4HGSS);
        RegisterMenuCapability("Menu_Gen4_PokeathlonHGSS", OpenPokeathlonCommand, "Menu_Gen4",
            applies: static s => s is SAV4HGSS);
        RegisterMenuCapability("Menu_Gen4_GeonetDPPt", OpenGeonet4Command, "Menu_Gen4",
            applies: static s => s is SAV4);
        RegisterMenuCapability("Menu_Gen4_BattlePassPBR", OpenBattlePassCommand, "Menu_Gen4",
            applies: static s => s is SAV4BR);
        RegisterMenuCapability("Menu_Pokedex", OpenPokedexCommand, "Menu_Gen4");
        RegisterConditionalMenuCapability("Dialog_ApricornEditor", OpenApricornCommand,
            "Menu_Gen4", () => CurrentSave is SAV4HGSS);
        RegisterConditionalMenuCapability("Safari4_Title", OpenSafariZone4Command,
            "Menu_Gen4", () => CurrentSave is SAV4HGSS);
        RegisterConditionalMenuCapability("Dialog_UndergroundEditor", OpenUndergroundCommand,
            "Menu_Gen4", () => CurrentSave is SAV4Sinnoh);
        RegisterConditionalMenuCapability("Dialog_GearEditorBattleRevolution", OpenGearBRCommand,
            "Menu_Gen4", () => CurrentSave is SAV4BR);
        RegisterConditionalMenuCapability("Dialog_BattleVideo4", OpenBattleVideo4Command,
            "Menu_Gen4", CanOpenBattleVideo4);

        RegisterMenuCapability("Menu_Misc", OpenMisc5Command, "Menu_Gen5",
            applies: static s => s is SAV5);
        RegisterMenuCapability("Menu_Pokedex", OpenPokedexCommand, "Menu_Gen5");
        RegisterMenuCapability("Menu_Gen5_Chatter", OpenChatterCommand, "Menu_Gen5",
            applies: static s => s is SAV5);
        RegisterMenuCapability("Menu_Gen5_MedalRally", OpenMedalCommand, "Menu_Gen5",
            applies: static s => s is SAV5B2W2);
        RegisterMenuCapability("Menu_Gen5_JoinAvenue", OpenJoinAvenueCommand, "Menu_Gen5",
            applies: static s => s is SAV5B2W2);
        RegisterMenuCapability("Menu_Gen5_UnityTower", OpenUnityTower5Command, "Menu_Gen5",
            applies: static s => s is SAV5);
        RegisterMenuCapability("Menu_Gen5_EntralinkForest", OpenEntralinkCommand, "Menu_Gen5",
            applies: static s => s is SAV5);
        RegisterMenuCapability("Menu_Gen5_GlobalLink", OpenGlobalLink5Command, "Menu_Gen5",
            applies: static s => s is SAV5);
        RegisterMenuCapability("Menu_Gen5_DLC", OpenDLC5Command, "Menu_Gen5",
            applies: static s => s is SAV5);

        RegisterMenuCapability("Menu_Pokedex", OpenPokedexCommand, "Menu_Gen6");
        RegisterMenuCapability("Menu_Gen6_OPowers", OpenOPowerCommand, "Menu_Gen6",
            applies: static s => s is ISaveBlock6Main);
        _capabilityRegistry.Add(new("FriendSafari_Title", string.Empty, string.Empty,
            UnlockFriendSafariCommand, menuGroupTitleKey: "Menu_Gen6",
            capabilityPredicate: () => CurrentSave is SAV6XY));
        RegisterMenuCapability("Menu_Gen6_SuperTraining", OpenSuperTrainingCommand, "Menu_Gen6",
            applies: static s => s is SAV6 and ISaveBlock6Main);
        RegisterMenuCapability("Menu_Roamer", OpenRoamer6Command, "Menu_Gen6",
            applies: static s => s is SAV6XY);
        RegisterMenuCapability("Menu_Gen6_PokemonLink", OpenLink6Command, "Menu_Gen6",
            applies: static s => s is SAV6);
        _capabilityRegistry.Add(new("Menu_Gen6_PSSExport", string.Empty, string.Empty,
            OpenPssExportCommand, menuGroupTitleKey: "Menu_Gen6",
            capabilityPredicate: () => CurrentSave is SAV6 { PSS: >= 0 }));
        RegisterMenuCapability("Menu_Gen6_SecretBaseEditor", OpenSecretBase6Command, "Menu_Gen6",
            applies: static s => s is SAV6AO);
        RegisterConditionalMenuCapability("Dialog_PokePuffEditor", OpenPokepuffCommand,
            "Menu_Gen6", () => CurrentSave is ISaveBlock6Main);
        RegisterConditionalMenuCapability("Dialog_PokeblockEditor", OpenPokeBlockCommand,
            "Menu_Gen6", () => CurrentSave is SAV6AO);
        RegisterConditionalMenuCapability("Dialog_BerryFieldEditor", OpenBerryFieldCommand,
            "Menu_Gen6", () => CurrentSave is SAV6AO);

        RegisterMenuCapability("Menu_Misc", OpenMisc7Command, "Menu_Gen7",
            applies: static s => s is SAV7);
        RegisterMenuCapability("Menu_Pokedex", OpenPokedexCommand, "Menu_Gen7");
        RegisterMenuCapability("Menu_Gen7_PokeBeans", OpenPokebeanCommand, "Menu_Gen7",
            applies: static s => s is SAV7);
        RegisterMenuCapability("Menu_Gen7_FestivalPlaza", OpenFestivalPlazaCommand, "Menu_Gen7",
            applies: static s => s is SAV7);
        RegisterMenuCapability("Menu_Gen7_ZygardeCells", OpenZygardeCellCommand, "Menu_Gen7",
            applies: static s => s is SAV7);
        RegisterMenuCapability("Menu_Gen7_CaptureRecordsLGPE", OpenCapture7GGCommand, "Menu_Gen7",
            applies: static s => s is SAV7b);
        RegisterMenuCapability("Menu_Gen7_HallOfFameSMUSUM", OpenHallOfFame7Command, "Menu_Gen7",
            applies: static s => s is SAV7);
        RegisterConditionalMenuCapability("Dialog_MiscEditorLetsGo", OpenMisc7bCommand,
            "Menu_Gen7", () => CurrentSave is SAV7b);

        RegisterMenuCapability("Menu_Pokedex", OpenPokedexCommand, "Menu_Gen8");
        RegisterMenuCapability("Menu_Gen8_MaxRaidsSWSH", OpenRaidEditorCommand, "Menu_Gen8",
            applies: static s => s is SAV8SWSH);
        RegisterConditionalMenuCapability("Dialog_RaidEventImport", OpenRaidEventImportCommand,
            "Menu_Gen8", () => CurrentSave is SAV8SWSH);
        RegisterMenuCapability("Menu_Gen8_FashionSWSH", OpenFashionCommand, "Menu_Gen8",
            applies: static s => s is SAV8SWSH);
        RegisterMenuCapability("Menu_Gen8_TrainerCardSWSH", OpenTrainerCard8Command, "Menu_Gen8",
            applies: static s => s is SAV8SWSH);
        RegisterMenuCapability("Menu_Gen8_MiscBDSP", OpenMisc8bCommand, "Menu_Gen8",
            applies: static s => s is SAV8BS);
        RegisterMenuCapability("Menu_Gen8_UndergroundBDSP", OpenUnderground8bCommand, "Menu_Gen8",
            applies: static s => s is SAV8BS);
        RegisterMenuCapability("Menu_Gen8_SealStickersBDSP", OpenSealStickers8bCommand, "Menu_Gen8",
            applies: static s => s is SAV8BS);
        RegisterMenuCapability("Menu_Gen8_PoffinsBDSP", OpenPoffin8bCommand, "Menu_Gen8",
            applies: static s => s is SAV8BS);
        RegisterConditionalMenuCapability("Dialog_MiscEditorSWSH", OpenMisc8Command,
            "Menu_Gen8", () => CurrentSave is SAV8SWSH);
        RegisterConditionalMenuCapability("Dialog_MiscEditorLegendsArceus", OpenMisc8aCommand,
            "Menu_Gen8", () => CurrentSave is SAV8LA);

        RegisterMenuCapability("Menu_Misc", OpenMisc9Command, "Menu_Gen9",
            applies: static s => s is SAV9SV);
        RegisterMenuCapability("Menu_Pokedex", OpenPokedexCommand, "Menu_Gen9");
        RegisterMenuCapability("Menu_Gen9_TeraRaidsSV", OpenRaid9Command, "Menu_Gen9",
            applies: static s => s is SAV9SV);
        RegisterMenuCapability("Menu_Gen9_SevenStarRaidsSV", OpenRaidSevenStar9Command, "Menu_Gen9",
            applies: static s => s is SAV9SV);
        RegisterConditionalMenuCapability("Dialog_RaidEventImport", OpenRaidEventImportCommand,
            "Menu_Gen9", () => CurrentSave is SAV9SV);
        RegisterMenuCapability("Menu_Gen9_FashionSVZA", OpenFashion9Command, "Menu_Gen9",
            applies: static s => s is SAV9SV or SAV9ZA);
        RegisterMenuCapability("Menu_Gen9_DonutPLZA", OpenDonutCommand, "Menu_Gen9",
            applies: SaveEditorCapabilities.HasDonuts);

        InitializeToolMenuGroups();
    }

    // Both helpers forward the caller's command expression: without it the descriptor would capture
    // the parameter name "command" instead of the command property, and derive its action ID from that.
    // `applies` narrows a descriptor to the exact save types its editor operates on, so a visible
    // menu, launcher or shortcut entry never opens a dead-end page or silently does nothing (#255).
    private void RegisterMenuCapability(string titleKey, ICommand command, string menuGroupTitleKey, bool showInLauncher = false,
        Func<SaveFile, bool>? applies = null,
        [CallerArgumentExpression(nameof(command))] string commandExpression = "")
    {
        var groupPredicate = GetMenuCapabilityPredicate(menuGroupTitleKey);
        var predicate = applies is null
            ? groupPredicate
            : () => CurrentSave is { } save && (groupPredicate?.Invoke() ?? true) && applies(save);
        _capabilityRegistry.Add(new(
            titleKey,
            string.Empty,
            string.Empty,
            command,
            showInLauncher: showInLauncher,
            menuGroupTitleKey: menuGroupTitleKey,
            capabilityPredicate: predicate,
            commandExpression: commandExpression));
    }

    private void RegisterConditionalMenuCapability(string titleKey, ICommand command,
        string menuGroupTitleKey, Func<bool> isApplicable,
        [CallerArgumentExpression(nameof(command))] string commandExpression = "")
    {
        _capabilityRegistry.Add(new(titleKey, string.Empty, string.Empty, command,
            showInLauncher: true, menuGroupTitleKey: menuGroupTitleKey,
            capabilityPredicate: isApplicable, commandExpression: commandExpression));
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
    private bool _isApplicable;
    private ShortcutChord? _shortcut;
    private string _shortcutText = string.Empty;

    public string IconData { get; }
    public ICommand Command { get; }
    public bool ShowInLauncher { get; }
    public bool ShowInReports { get; }
    public string? MenuGroupTitleKey { get; }
    public bool IsAvailable => _isAvailable;

    /// <summary>
    /// True when the descriptor's save-type predicate accepts the current save. Menus hide entries
    /// that do not apply (no dead-end pages); an applicable entry whose command cannot run right
    /// now (for example, no save is loaded) stays visible but disabled.
    /// </summary>
    public bool IsApplicable => _isApplicable;
    public string Title => LocalizedStrings.Instance[_titleKey];
    public string Description => string.IsNullOrEmpty(_descriptionKey)
        ? string.Empty
        : LocalizedStrings.Instance[_descriptionKey];

    /// <summary>
    /// Stable keyboard-shortcut action ID. Every descriptor of one command shares it (the Pokedex in
    /// each generation group is one action), and different commands never do.
    /// </summary>
    public string ActionId { get; }

    /// <summary>Effective shortcut of <see cref="ActionId"/>, or <see langword="null"/>.</summary>
    public ShortcutChord? Shortcut => _shortcut;

    /// <summary>Platform display text of <see cref="Shortcut"/>, or an empty string.</summary>
    public string ShortcutText => _shortcutText;

    public bool HasShortcut => _shortcut is not null;

    internal string TitleKey => _titleKey;

    /// <param name="actionId">
    /// Explicit shortcut action ID. When omitted, the ID is the command property name without its
    /// <c>Command</c> suffix, taken from <paramref name="commandExpression"/>.
    /// </param>
    /// <param name="commandExpression">Source text of the command argument; supplied by the compiler.</param>
    public ToolLauncherItem(
        string titleKey,
        string descriptionKey,
        string iconData,
        ICommand command,
        bool showInLauncher = true,
        bool showInReports = false,
        string? menuGroupTitleKey = null,
        Func<bool>? capabilityPredicate = null,
        string? actionId = null,
        [CallerArgumentExpression(nameof(command))] string commandExpression = "")
    {
        _titleKey = titleKey;
        _descriptionKey = descriptionKey;
        IconData = iconData;
        Command = command;
        ShowInLauncher = showInLauncher;
        ShowInReports = showInReports;
        MenuGroupTitleKey = menuGroupTitleKey;
        _capabilityPredicate = capabilityPredicate;
        ActionId = actionId ?? ShortcutActionId.FromExpression(commandExpression);
        _isApplicable = IsApplicableNow();
        _isAvailable = CanInvoke();
    }

    /// <summary>
    /// Live availability: <see cref="Command"/> can execute and the save-type predicate accepts the
    /// current save. Unlike <see cref="IsAvailable"/> this is never a cached value.
    /// </summary>
    public bool CanInvoke() => Command.CanExecute(null) && IsApplicableNow();

    private bool IsApplicableNow() => _capabilityPredicate?.Invoke() ?? true;

    public bool Matches(string query) => Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || Description.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
    }

    public void RefreshAvailability()
    {
        var applicable = IsApplicableNow();
        if (_isApplicable != applicable)
        {
            _isApplicable = applicable;
            OnPropertyChanged(nameof(IsApplicable));
        }

        var available = CanInvoke();
        if (_isAvailable == available)
            return;

        _isAvailable = available;
        OnPropertyChanged(nameof(IsAvailable));
    }

    /// <summary>Publishes the action's resolved shortcut; raises change notifications only for real changes.</summary>
    internal void SetShortcut(ShortcutChord? chord, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var chordChanged = _shortcut != chord;
        var textChanged = !string.Equals(_shortcutText, text, StringComparison.Ordinal);
        _shortcut = chord;
        _shortcutText = text;

        if (chordChanged)
        {
            OnPropertyChanged(nameof(Shortcut));
            OnPropertyChanged(nameof(HasShortcut));
        }
        if (textChanged)
            OnPropertyChanged(nameof(ShortcutText));
    }
}

/// <summary>One visible submenu in the capability-driven Tools menu.</summary>
public sealed class ToolMenuGroup : ObservableObject
{
    private readonly string _titleKey;

    public IReadOnlyList<ToolLauncherItem> Items { get; }
    public string Title => LocalizedStrings.Instance[_titleKey];
    internal string TitleKey => _titleKey;
    public bool IsAvailable => Items.Any(item => item.IsAvailable);

    public ToolMenuGroup(string titleKey, IEnumerable<ToolLauncherItem> items)
    {
        _titleKey = titleKey;
        Items = items.ToArray();
    }

    public void RefreshLocalization() => OnPropertyChanged(nameof(Title));

    public void RefreshAvailability() => OnPropertyChanged(nameof(IsAvailable));
}
