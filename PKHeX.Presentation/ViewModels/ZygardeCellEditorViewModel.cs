using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class ZygardeCellEditorViewModel : ViewModelBase, ICloseableDialog
{
    private readonly SaveFile _sav;
    private readonly SAV7? _sav7;
    private bool _loading;

    public ZygardeCellEditorViewModel(SaveFile sav)
    {
        _sav = sav;
        _sav7 = sav as SAV7;
        IsSupported = _sav7 is not null;

        if (IsSupported)
            LoadData();
    }

    public bool IsSupported { get; }
    public bool IsTotemSticker => _sav7 is SAV7USUM;
    public bool IsZygardeCell => !IsTotemSticker;

    [ObservableProperty]
    private int _cellsTotal;

    partial void OnCellsTotalChanged(int value) => NotifyCounterValidity();

    [ObservableProperty]
    private int _cellsCollected;

    partial void OnCellsCollectedChanged(int value) => NotifyCounterValidity();

    public System.Action? CloseRequested { get; set; }
    public bool CanSave => IsSupported && CellsTotal is >= 0 and <= ushort.MaxValue &&
        CellsCollected is >= 0 and <= ushort.MaxValue && Cells.All(c => c.State is >= 0 and <= 2);
    public bool HasCounterWarning => CellsCollected < Cells.Count(c => c.State == 2);

    private void NotifyCounterValidity()
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(HasCounterWarning));
        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (!CanSave || _sav7 is null) return;
        foreach (var cell in Cells)
            _sav7.EventWork.SetZygardeCell(cell.Index, (ushort)cell.State);
        _sav7.EventWork.ZygardeCellTotal = (ushort)CellsTotal;
        _sav7.EventWork.ZygardeCellCount = (ushort)CellsCollected;
        if (_sav7 is SAV7USUM) _sav7.SetRecord(72, CellsCollected);
        _sav.State.Edited = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        LoadData();
        CloseRequested?.Invoke();
    }

    [ObservableProperty]
    private ObservableCollection<ZygardeCellViewModel> _cells = [];

    private void LoadData()
    {
        if (_sav7?.EventWork is not { } ew) return;

        _loading = true;
        try
        {
            CellsTotal = ew.ZygardeCellTotal;
            CellsCollected = ew.ZygardeCellCount;

            Cells.Clear();
            var locations = _sav7 is SAV7SM ? LocationsSM : LocationsUSUM;
            var count = ew.TotalZygardeCellCount;

            for (int i = 0; i < count; i++)
            {
                var state = ew.GetZygardeCell(i);
                var locationName = i < locations.Length ? locations[i] : $"Cell {i + 1}";
                Cells.Add(new ZygardeCellViewModel(i, locationName, state, _sav7 is SAV7USUM, SetCellState));
            }
        }
        finally
        {
            _loading = false;
            NotifyCounterValidity();
        }
    }

    private void SetCellState(int index, int oldState, int state)
    {
        if (_loading || _sav7 is null) return;
        // Preserve cells already spent on assembly and the five core-related counts.
        // Only entering/leaving Received changes the counters; Available is not Stored.
        var delta = (state == 2 ? 1 : 0) - (oldState == 2 ? 1 : 0);
        CellsCollected = System.Math.Clamp(CellsCollected + delta, 0, ushort.MaxValue);
        if (!IsTotemSticker)
            CellsTotal = System.Math.Clamp(CellsTotal + delta, 0, ushort.MaxValue);
        NotifyCounterValidity();
    }

    [RelayCommand]
    private void CollectAll()
    {
        foreach (var cell in Cells) cell.State = 2;
    }

    [RelayCommand]
    private void Refresh() => LoadData();

    private static readonly string[] LocationsSM =
    [
        "Verdant Cave - Trial Site", "Ruins of Conflict - Outside", "Route 1 (Day)", "Route 3",
        "Route 3 (Day)", "Kala'e Bay", "Hau'oli Cemetery", "Route 2",
        "Route 1 - Trainer School (Night)", "Hau'oli City - Shopping District", "Route 1 - Outskirts",
        "Hau'oli City - Shopping District (Night)", "Route 1", "Iki Town (Night)", "Route 4",
        "Paniola Ranch (Night)", "Paniola Ranch (Day)", "Wela Volcano Park - Top", "Lush Jungle - Cave",
        "Route 7", "Akala Outskirts", "Royal Avenue (Day)", "Royal Avenue (Night)", "Konikoni City (Night)",
        "Heahea City (Night)", "Route 8", "Route 8 (Day)", "Route 5", "Hano Beach (Day)", "Heahea City",
        "Diglett's Tunnel", "Hano Beach", "Malie Garden", "Malie City - Community Center (Night)",
        "Malie City (Day)", "Malie City - Outer Cape (Day)", "Route 11 (Night)", "Route 12 (Day)",
        "Route 12", "Secluded Shore (Night)", "Blush Mountain", "Route 13", "Haina Desert",
        "Ruins of Abundance - Outside", "Route 14", "Route 14 (Night)", "Tapu Village", "Route 15",
        "Aether House (Day)", "Ula'ula Meadow - Boardwalk", "Route 16 (Day)", "Ula'ula Meadow - Grass",
        "Route 17 - Building", "Route 17 - Ledge", "Po Town (Night)", "Route 10 (Day)",
        "Hokulani Observatory (Night)", "Mount Lanakila - Mountainside", "Mount Lanakila - High Mountainside",
        "Secluded Shore (Day)", "Route 13 (Night)", "Po Town (Day)", "Seafolk Village - Blue Food Boat",
        "Seafolk Village - Unbuilt House", "Poni Wilds (Day)", "Poni Wilds (Night)", "Poni Wilds",
        "Ancient Poni Path - Near Well (Day)", "Ancient Poni Path (Night)", "Poni Breaker Coast (Day)",
        "Ruins of Hope", "Poni Grove - Mountain Corner", "Poni Grove - Near a Bush", "Poni Plains (Day)",
        "Poni Plains (Night)", "Poni Plains", "Poni Meadow", "Poni Coast (Night)", "Poni Coast",
        "Poni Gauntlet - On Bridge", "Poni Gauntlet - Island w/ Trainer", "Resolution Cave - 1F (Day)",
        "Resolution Cave - B1F (Night)", "Vast Poni Canyon - 3F", "Vast Poni Canyon - 2F",
        "Vast Poni Canyon - Top", "Vast Poni Canyon - Inside", "Ancient Poni Path - Brickwall (Day)",
        "Poni Breaker Coast (Night)", "Resolution Cave - B1F", "Aether Foundation B2F - Right Hallway",
        "Aether Foundation 1F - Outside - Right Side", "Aether Foundation 1F - Outside (Day)",
        "Aether Foundation 1F - Entrance (Night)", "Aether Foundation 1F - Main Building",
    ];

    private static readonly string[] LocationsUSUM =
    [
        "Hau'oli City (Shopping) - Salon (Outside)", "Hau'oli City (Shopping) - Malasada Shop (Outside)",
        "Hau'oli City (Shopping) - Ilima's House (2F)", "Malie City - Library (1F)",
        "Hau'oli City (Marina) - Pier", "Route 2 - Southeast House",
        "Hau'oli City (Shopping) - Ilima's House (Outside)", "Hau'oli City (Shopping) - City Hall",
        "Heahea City - Hotel (3F)", "Route 2 - Berry Fields House", "Route 2 - Berry Fields House (Outside)",
        "Royal Avenue - Northeast", "Hau'oli City (Shopping) - Pokemon Center (Outside)",
        "Royal Avenue - South", "Hokulani Observatory - Room", "Hokulani Observatory - Reception",
        "Hau'oli City (Shopping) - City Hall (Outside)", "Konikoni City - Olivia's Jewelry Shop (2F)",
        "Heahea City - Surfboard (Outside)", "Po Town - Southwest", "Hano Resort Lobby - Southwest Water",
        "Hau'oli City (Shopping) - Northwest of Police Station", "Hau'oli City (Marina) - Ferry Terminal (Outside)",
        "Route 2 - Southeast House (Outside)", "Route 2 - Pokemon Center (Outside)", "Heahea City - West",
        "Heahea City - Hotel West (Outside)", "Heahea City - Hotel East (Outside)",
        "Heahea City - Research Lab East (Outside)", "Heahea City - Research Lab South (Outside)",
        "Heahea City - Game Freak", "Hokulani Observatory - Dead End", "Heahea City - Game Freak Building (3F)",
        "Heahea City - Research Lab", "Heahea City - Hotel (1F)", "Battle Royal Dome - 2F",
        "Paniola Town - West", "Paniola Town - Kiawe's House (1F)", "Paniola Town - Kiawe's House (2F)",
        "Paniola Ranch - Northwest", "Paniola Ranch - Southeast", "Hano Beach", "Hano Resort - South",
        "Hano Resort - North", "Konikoni City Lighthouse (Through Diglett's Tunnel)", "Battle Royal Dome - 1F",
        "Route 8 - Aether Base (Outside)", "Route 8 - Fossil Restoration Center (Outside)",
        "Konikoni City - West", "Konikoni City - Restaurant (1F)", "Iki Town - Southwest",
        "Hau'oli City (Shopping) - Ilima's House Pool", "Wela Volcano Park - Rocks Behind Sign",
        "Route 5 - South of Pokemon Center", "Hano Beach - Below Sandygast",
        "Malie City (Outer Cape) - Recycling Plant (Outside)", "Malie City - Ferry Terminal (Outside)",
        "Malie City - Apparel Shop (Outside)", "Malie City - Salon (Outside)",
        "Route 16 - Aether Base (Outside)", "Blush Mountain - Power Plant (Outside)",
        "Malie City - Library (2F)", "Malie Garden - Northeast", "Malie City - CommunityCenter",
        "Hokulani Observatory - Outside", "Mount Hokulani", "Blush Mountain - Power Plant", "Route 13",
        "Route 14 - Front of Abandoned Megamart", "Route 14 - North", "Route 15 - Islet Surfboard (Outside)",
        "Route 17 - Police Station (Outside)", "Route 17 - Police Station", "Po Town - Pokemon Center (Outside)",
        "Exeggutor Island - Under Rock", "Po Town - Shady House East (Outside)", "Po Town - Pokemon Center",
        "Po Town - Shady House (1F)", "Route 13 - Motel (Outside)", "Po Town - Shady House 2F (Outside)",
        "Route 17 - South of Po Town", "Ula'ula Meadow", "Po Town - Shady House West Rocks (Outside) 1",
        "Po Town - Shady House West Rocks (Outside) 2", "Po Town - Shady House West Rocks (Outside) 3",
        "Seafolk Village - Southeast Whiscash (Mina's Ship) (Outside)", "Seafolk Village - Southwest Huntail",
        "Seafolk Village - Southwest Huntail (Outside)", "Seafolk Village - Southeast Whiscash (Mina's Ship)",
        "Seafolk Village - West Wailord (Restaurant)", "Seafolk Village - East Steelix",
        "Poni Wilds - Southeast", "Ancient Poni Path - Hapu's House (Kitchen)", "Seafolk Village - Northeast",
        "Ancient Poni Path - Hapu's House (Bedroom)", "Ancient Poni Path - Southwest",
        "Ancient Poni Path - Hapu's House (Courtyard)", "Ancient Poni Path - Hapu's House (Outside Behind Well)",
        "Ancient Poni Path - Northeast", "Battle Tree - Entrance",
    ];
}

public partial class ZygardeCellViewModel : ViewModelBase
{
    private readonly System.Action<int, int, int> _onStateChanged;

    public ZygardeCellViewModel(int index, string location, int state, bool isTotemSticker, System.Action<int, int, int> onStateChanged)
    {
        Index = index;
        Location = location;
        _state = state;
        _isTotemSticker = isTotemSticker;
        _onStateChanged = onStateChanged;
    }

    private readonly bool _isTotemSticker;

    public int Index { get; }
    public string Location { get; }
    public string DisplayName => $"{Index + 1}: {Location}";

    [ObservableProperty]
    private int _state;

    partial void OnStateChanged(int oldValue, int newValue)
    {
        _onStateChanged(Index, oldValue, newValue);
        OnPropertyChanged(nameof(StateName));
    }

    public string StateName => State switch
    {
        0 => LocalizedStrings.Instance[_isTotemSticker ? "ZygardeCellEditor_TotemStickerNone" : "ZygardeCellEditor_None"],
        1 => LocalizedStrings.Instance[_isTotemSticker ? "ZygardeCellEditor_TotemStickerAvailable" : "ZygardeCellEditor_Available"],
        2 => LocalizedStrings.Instance[_isTotemSticker ? "ZygardeCellEditor_TotemStickerReceived" : "ZygardeCellEditor_Received"],
        _ => string.Empty
    };
}
