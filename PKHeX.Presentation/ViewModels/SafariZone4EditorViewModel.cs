using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// HeartGold/SoulSilver Safari Zone editor. All edits are staged in this view model; <see cref="Save"/> rebuilds the
/// Safari block on a detached copy of the General block and copies only the bytes that differ back into the save.
/// </summary>
public partial class SafariZone4EditorViewModel : ViewModelBase, ICloseableDialog
{
    /// <summary> Number of known <see cref="SafariZoneType4"/> values. </summary>
    public const int MapCount = (int)SafariZoneType4.Desert + 1;

    /// <summary> Number of known <see cref="SafariZoneObjectType4"/> values. </summary>
    public const int ObjectTypeCount = (int)SafariZoneObjectType4.TrashCan + 1;

    private static readonly string[] MapKeys =
    [
        "Safari4_Map_Plains", "Safari4_Map_Meadow", "Safari4_Map_Savannah", "Safari4_Map_Peak",
        "Safari4_Map_RockyBeach", "Safari4_Map_Wetland", "Safari4_Map_Forest", "Safari4_Map_Swamp",
        "Safari4_Map_Marshland", "Safari4_Map_Wasteland", "Safari4_Map_Mountain", "Safari4_Map_Desert",
    ];

    private readonly SAV4HGSS _source;
    private readonly byte[] _original;
    private bool _loading;

    public SafariZone4EditorViewModel(SAV4HGSS save)
    {
        _source = save;
        _original = save.General.ToArray();
        var cells = new SafariZoneCellViewModel[SafariZoneMap4.WorldSize * SafariZoneMap4.WorldSize];
        for (int z = 0; z < SafariZoneMap4.WorldSize; z++)
        for (int x = 0; x < SafariZoneMap4.WorldSize; x++)
            cells[(z * SafariZoneMap4.WorldSize) + x] = new SafariZoneCellViewModel(x, z);
        Cells = cells;
        Load();
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (_, _) => RefreshLanguage());
    }

    public Action? CloseRequested { get; set; }

    /// <summary> 32 x 32 world tiles, row-major by Z then X. </summary>
    public IReadOnlyList<SafariZoneCellViewModel> Cells { get; }

    public IReadOnlyList<SafariZoneSetViewModel> Sets { get; private set; } = [];

    [ObservableProperty] private SafariZoneSetViewModel? _selectedSet;
    [ObservableProperty] private SafariZoneAreaViewModel? _selectedArea;
    [ObservableProperty] private SafariZoneObjectRow? _selectedObject;
    [ObservableProperty] private string _statusText = string.Empty;

    [ObservableProperty] private int _dayDelay;
    [ObservableProperty] private int _elapsedPlayMinutes;
    [ObservableProperty] private int _objectUnlockLevel;
    [ObservableProperty] private bool _isAreaSwitcherUnlocked;

    [ObservableProperty] private bool _isLinked;
    [ObservableProperty] private string _linkTrainerName = string.Empty;
    [ObservableProperty] private long _linkTrainerId;
    [ObservableProperty] private int _linkGender;
    [ObservableProperty] private int _linkLanguage;
    [ObservableProperty] private int _linkVersion;

    private int _origDayDelay, _origElapsed, _origUnlock;
    private bool _origSwitcher, _origLinked;
    private string _origName = string.Empty;
    private long _origId;
    private int _origGender, _origLanguage, _origVersion;

    /// <summary> Summary of objects whose placement the game would reject, for the selected area. </summary>
    public string ProblemText
    {
        get
        {
            var count = SelectedArea?.Objects.Count(o => o.Placement != SafariZonePlacement4.None) ?? 0;
            return count == 0 ? string.Empty : LocalizedStrings.Instance.Format("Safari4_Problems", count);
        }
    }

    /// <summary> Gets the localized name of an area type, or an unknown-value label for raw values outside the enum. </summary>
    public static string GetMapName(int value) => (uint)value < MapCount
        ? LocalizedStrings.Instance[MapKeys[value]]
        : LocalizedStrings.Instance.Format("Safari4_UnknownValue", value);

    /// <summary> Gets the localized name of an object type, or an unknown-value label for raw values outside the enum. </summary>
    public static string GetObjectName(int value)
    {
        var names = GameInfo.Strings.safariblocks;
        return (uint)value < ObjectTypeCount && value < names.Length
            ? names[value]
            : LocalizedStrings.Instance.Format("Safari4_UnknownValue", value);
    }

    private void Load()
    {
        _loading = true;
        var safari = CreateStaging().Safari;
        var self = new SafariZoneSetViewModel("Safari4_SetSelf", safari.Self, OnObjectChanged);
        var link = new SafariZoneSetViewModel("Safari4_SetLink", safari.Link, OnObjectChanged);
        Sets = [self, link];
        OnPropertyChanged(nameof(Sets));

        DayDelay = _origDayDelay = safari.DayDelay;
        ElapsedPlayMinutes = _origElapsed = safari.ElapsedPlayMinutes;
        ObjectUnlockLevel = _origUnlock = safari.ObjectUnlockLevel;
        IsAreaSwitcherUnlocked = _origSwitcher = safari.AreaSwitcherState == 1;

        var leader = safari.LinkLeader;
        IsLinked = _origLinked = leader.IsLinked;
        LinkTrainerName = _origName = leader.OriginalTrainerName;
        LinkTrainerId = _origId = leader.ID32;
        LinkGender = _origGender = leader.Gender;
        LinkLanguage = _origLanguage = leader.Language;
        LinkVersion = _origVersion = (int)leader.Version;

        _loading = false;
        SelectedSet = self;
        StatusText = string.Empty;
    }

    /// <summary> Builds a detached HG/SS save whose General block is a copy of the block as it was when the editor opened. </summary>
    private SAV4HGSS CreateStaging()
    {
        var staging = new SAV4HGSS();
        _original.CopyTo(staging.General);
        return staging;
    }

    partial void OnSelectedSetChanged(SafariZoneSetViewModel? value)
    {
        SelectedArea = value?.Areas[0];
    }

    partial void OnSelectedAreaChanged(SafariZoneAreaViewModel? value)
    {
        SelectedObject = null;
        StatusText = string.Empty;
        RefreshMap();
    }

    partial void OnSelectedObjectChanged(SafariZoneObjectRow? value) => RefreshSelection();

    private void OnObjectChanged(SafariZoneAreaViewModel area)
    {
        if (_loading || !ReferenceEquals(area, SelectedArea))
            return;
        RefreshMap();
    }

    private void RefreshMap()
    {
        var area = SelectedArea;
        var occupant = new int[Cells.Count];
        Array.Fill(occupant, -1);
        if (area is not null)
        {
            // Later rows are drawn on top, so they win the tile.
            foreach (var row in area.Objects)
            {
                if (row.IsEmpty)
                    continue;
                var (w, d) = SafariZoneMap4.GetSize((SafariZoneObjectType4)row.Type);
                for (int dz = 0; dz < d; dz++)
                for (int dx = 0; dx < w; dx++)
                {
                    int x = row.X + dx, z = row.Z + dz;
                    if (x < SafariZoneMap4.WorldSize && z < SafariZoneMap4.WorldSize)
                        occupant[(z * SafariZoneMap4.WorldSize) + x] = row.Index;
                }
            }
        }

        var map = (SafariZoneType4)(area?.Map ?? 0);
        bool knownMap = area is not null && (uint)area.Map < MapCount;
        for (int i = 0; i < Cells.Count; i++)
        {
            var cell = Cells[i];
            var row = occupant[i] >= 0 ? area!.Objects[occupant[i]] : null;
            cell.Update(knownMap, map, row);
        }
        RefreshSelection();
        OnPropertyChanged(nameof(ProblemText));
    }

    private void RefreshSelection()
    {
        var row = SelectedObject;
        foreach (var cell in Cells)
            cell.IsSelected = row is not null && cell.Occupant is { } o && ReferenceEquals(o, row);
    }

    /// <summary> Selects the object on the tile, or claims the first empty row for an object placed on the tile. </summary>
    [RelayCommand]
    private void SelectTile(SafariZoneCellViewModel? cell)
    {
        var area = SelectedArea;
        if (cell is null || area is null)
            return;
        StatusText = string.Empty;
        if (cell.Occupant is { } occupant)
        {
            SelectedObject = occupant;
            return;
        }

        var free = area.Objects.FirstOrDefault(o => o.IsEmpty);
        if (free is null)
        {
            StatusText = LocalizedStrings.Instance["Safari4_SlotsFull"];
            return;
        }
        free.SetPosition(cell.X, cell.Z, Math.Max(0, cell.Height));
        SelectedObject = free;
    }

    [RelayCommand]
    private void RemoveObject()
    {
        if (SelectedObject is { } row)
            row.Clear();
    }

    [RelayCommand]
    private void ClearObjects()
    {
        if (SelectedArea is not { } area)
            return;
        area.ClearObjects();
        SelectedObject = null;
    }

    [RelayCommand]
    private void MaxAges()
    {
        foreach (var age in SelectedSet?.Ages ?? [])
            age.Value = byte.MaxValue;
    }

    [RelayCommand]
    private void ClearAges()
    {
        foreach (var age in SelectedSet?.Ages ?? [])
            age.Value = 0;
    }

    [RelayCommand]
    private void Reset()
    {
        int set = SelectedSet is null ? 0 : Sets.ToList().IndexOf(SelectedSet);
        int area = SelectedArea?.Slot ?? 0;
        Load();
        SelectedSet = Sets[Math.Max(0, set)];
        SelectedArea = SelectedSet.Areas[area];
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    [RelayCommand]
    private void Save()
    {
        Commit();
        CloseRequested?.Invoke();
    }

    /// <summary> Writes the staged values and returns the number of save bytes that changed. </summary>
    public int Commit()
    {
        var staging = CreateStaging();
        var safari = staging.Safari;
        Sets[0].WriteTo(safari.Self);
        Sets[1].WriteTo(safari.Link);

        if (DayDelay != _origDayDelay) safari.DayDelay = (byte)DayDelay;
        if (ElapsedPlayMinutes != _origElapsed) safari.ElapsedPlayMinutes = (ushort)ElapsedPlayMinutes;
        if (ObjectUnlockLevel != _origUnlock) safari.ObjectUnlockLevel = (byte)ObjectUnlockLevel;
        if (IsAreaSwitcherUnlocked != _origSwitcher) safari.AreaSwitcherState = (byte)(IsAreaSwitcherUnlocked ? 1 : 0);

        var leader = safari.LinkLeader;
        if (IsLinked != _origLinked) leader.IsLinked = IsLinked;
        if (LinkGender != _origGender) leader.Gender = (byte)LinkGender;
        if (LinkLanguage != _origLanguage) leader.Language = (byte)LinkLanguage;
        if (LinkVersion != _origVersion) leader.Version = (GameVersion)LinkVersion;
        if (LinkTrainerId != _origId) leader.ID32 = (uint)LinkTrainerId;
        if (LinkTrainerName != _origName)
            leader.OriginalTrainerName = LinkTrainerName;

        // Only the Safari block was rebuilt on the detached copy; copy back the bytes that actually differ.
        ReadOnlySpan<byte> staged = staging.General;
        var live = _source.General;
        int changed = 0;
        for (int i = 0; i < staged.Length; i++)
        {
            if (staged[i] == _original[i])
                continue;
            live[i] = staged[i];
            changed++;
        }
        if (changed != 0)
            _source.State.Edited = true;
        return changed;
    }

    private void RefreshLanguage()
    {
        foreach (var set in Sets)
            set.RefreshLanguage();
        foreach (var cell in Cells)
            cell.RefreshLanguage();
        OnPropertyChanged(nameof(ProblemText));
        StatusText = string.Empty;
    }
}

/// <summary> A choice in a raw-value combo box. </summary>
public sealed record SafariZoneChoice(int Value, string Text);

/// <summary> One of the two area sets (the player's own zone and the linked trainer's zone). </summary>
public sealed partial class SafariZoneSetViewModel : ObservableObject
{
    private readonly string _nameKey;

    public SafariZoneSetViewModel(string nameKey, SafariZoneAreaSet4 set, Action<SafariZoneAreaViewModel> objectChanged)
    {
        _nameKey = nameKey;
        Areas = Enumerable.Range(0, SafariZoneAreaSet4.MapCountConfiguration)
            .Select(i => new SafariZoneAreaViewModel(i, set.Configurations[i], objectChanged)).ToArray();
        Ages = Enumerable.Range(0, SafariZone4EditorViewModel.MapCount)
            .Select(i => new SafariZoneAgeRow(i, set.GetMapAge((SafariZoneType4)i))).ToArray();
    }

    public string Name => LocalizedStrings.Instance[_nameKey];
    public IReadOnlyList<SafariZoneAreaViewModel> Areas { get; }
    public IReadOnlyList<SafariZoneAgeRow> Ages { get; }

    public void WriteTo(SafariZoneAreaSet4 set)
    {
        for (int i = 0; i < Areas.Count; i++)
        {
            Areas[i].WriteTo(set.Configurations[i]);
            set.FlushConfiguration(i);
        }
        foreach (var age in Ages)
            set.SetMapAge((SafariZoneType4)age.Map, (byte)age.Value);
        set.FlushMapAges();
    }

    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(Name));
        foreach (var area in Areas)
            area.RefreshLanguage();
        foreach (var age in Ages)
            age.RefreshLanguage();
    }
}

/// <summary> Age (days placed) of one area type. </summary>
public sealed partial class SafariZoneAgeRow(int map, byte value) : ObservableObject
{
    public int Map { get; } = map;
    public string Name => SafariZone4EditorViewModel.GetMapName(Map);
    [ObservableProperty] private int _value = value;
    public void RefreshLanguage() => OnPropertyChanged(nameof(Name));
}

/// <summary> One of the six configurable area slots, with its 30 placed objects. </summary>
public sealed partial class SafariZoneAreaViewModel : ObservableObject
{
    private readonly Action<SafariZoneAreaViewModel> _objectChanged;
    private readonly SafariZonePlacement4[] _placements = new SafariZonePlacement4[SafariZoneArea4.BlockCount];
    private bool _bulk;

    public SafariZoneAreaViewModel(int slot, SafariZoneArea4 area, Action<SafariZoneAreaViewModel> objectChanged)
    {
        _objectChanged = objectChanged;
        Slot = slot;
        _map = (int)area.Map;
        _count = area.Count;
        Objects = area.Objects.Select((o, i) => new SafariZoneObjectRow(i, o)).ToArray();
        foreach (var row in Objects)
            row.PropertyChanged += RowChanged;
        MapChoices = BuildMapChoices();
        Evaluate();
    }

    public int Slot { get; }
    public IReadOnlyList<SafariZoneObjectRow> Objects { get; }
    [ObservableProperty] private IReadOnlyList<SafariZoneChoice> _mapChoices;
    [ObservableProperty] private int _map;
    [ObservableProperty] private int _count;
    [ObservableProperty] private bool _isAutoCount = true;

    public string Name => LocalizedStrings.Instance.Format("Safari4_SlotName", Slot + 1, SafariZone4EditorViewModel.GetMapName(Map));

    partial void OnMapChanged(int value)
    {
        OnPropertyChanged(nameof(Name));
        Evaluate();
        _objectChanged(this);
    }

    partial void OnIsAutoCountChanged(bool value)
    {
        if (value)
            UpdateCount();
    }

    private void RowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_bulk || e.PropertyName is not (nameof(SafariZoneObjectRow.Type) or nameof(SafariZoneObjectRow.X)
                or nameof(SafariZoneObjectRow.Z) or nameof(SafariZoneObjectRow.Y)))
            return;
        AfterObjectsChanged();
    }

    private void AfterObjectsChanged()
    {
        if (IsAutoCount)
            UpdateCount();
        Evaluate();
        _objectChanged(this);
    }

    private void UpdateCount() => Count = Objects.Count(o => !o.IsEmpty);

    public void ClearObjects()
    {
        _bulk = true;
        foreach (var row in Objects)
            row.Clear();
        _bulk = false;
        Count = 0;
        Evaluate();
        _objectChanged(this);
    }

    private void Evaluate()
    {
        var objects = Objects.Select(o => o.ToObject()).ToArray();
        SafariZoneMap4.Evaluate((SafariZoneType4)Map, objects, _placements);
        for (int i = 0; i < Objects.Count; i++)
            Objects[i].Placement = _placements[i];
    }

    public void WriteTo(SafariZoneArea4 area)
    {
        area.Map = (SafariZoneType4)Map;
        area.Count = (byte)Count;
        for (int i = 0; i < Objects.Count; i++)
            area.Objects[i] = Objects[i].ToObject();
    }

    private IReadOnlyList<SafariZoneChoice> BuildMapChoices()
    {
        var list = Enumerable.Range(0, SafariZone4EditorViewModel.MapCount)
            .Select(i => new SafariZoneChoice(i, SafariZone4EditorViewModel.GetMapName(i))).ToList();
        if ((uint)Map >= SafariZone4EditorViewModel.MapCount)
            list.Add(new SafariZoneChoice(Map, SafariZone4EditorViewModel.GetMapName(Map)));
        return list;
    }

    public void RefreshLanguage()
    {
        // Replacing the items clears the combo box selection; re-announce the unchanged value afterwards.
        MapChoices = BuildMapChoices();
        OnPropertyChanged(nameof(Map));
        OnPropertyChanged(nameof(Name));
        foreach (var row in Objects)
            row.RefreshLanguage();
    }
}

/// <summary> One of the 30 object records of an area. </summary>
public sealed partial class SafariZoneObjectRow : ObservableObject
{
    public SafariZoneObjectRow(int index, SafariZoneObject4 obj)
    {
        Index = index;
        _type = (int)obj.Type;
        _x = obj.X;
        _z = obj.Z;
        _y = obj.Y;
        _typeChoices = BuildTypeChoices();
    }

    public int Index { get; }
    public int Number => Index + 1;
    [ObservableProperty] private IReadOnlyList<SafariZoneChoice> _typeChoices;
    [ObservableProperty] private int _type;
    [ObservableProperty] private int _x;
    [ObservableProperty] private int _z;
    [ObservableProperty] private int _y;
    [ObservableProperty] private SafariZonePlacement4 _placement;

    public bool IsEmpty => Type == 0 && X == 0 && Y == 0 && Z == 0;
    public string TypeName => SafariZone4EditorViewModel.GetObjectName(Type);

    public string ProblemText => Placement switch
    {
        SafariZonePlacement4.None => string.Empty,
        SafariZonePlacement4.Overlap => LocalizedStrings.Instance["Safari4_Overlap"],
        SafariZonePlacement4.Unplaceable => LocalizedStrings.Instance["Safari4_Unplaceable"],
        _ => LocalizedStrings.Instance["Safari4_Overlap"] + "; " + LocalizedStrings.Instance["Safari4_Unplaceable"],
    };

    public string Summary => IsEmpty
        ? LocalizedStrings.Instance.Format("Safari4_ObjectEmpty", Number)
        : LocalizedStrings.Instance.Format("Safari4_ObjectRow", Number, TypeName, X, Z, Y);

    partial void OnTypeChanged(int value) => Changed();
    partial void OnXChanged(int value) => Changed();
    partial void OnZChanged(int value) => Changed();
    partial void OnYChanged(int value) => Changed();
    partial void OnPlacementChanged(SafariZonePlacement4 value) => OnPropertyChanged(nameof(ProblemText));

    private void Changed()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(TypeName));
        OnPropertyChanged(nameof(Summary));
    }

    public void SetPosition(int x, int z, int y)
    {
        X = x;
        Z = z;
        Y = y;
    }

    public void Clear()
    {
        Type = 0;
        SetPosition(0, 0, 0);
    }

    public SafariZoneObject4 ToObject() => new()
    {
        Type = (SafariZoneObjectType4)Type,
        X = (byte)X,
        Z = (byte)Z,
        Y = (byte)Y,
    };

    private IReadOnlyList<SafariZoneChoice> BuildTypeChoices()
    {
        var list = Enumerable.Range(0, SafariZone4EditorViewModel.ObjectTypeCount)
            .Select(i => new SafariZoneChoice(i, SafariZone4EditorViewModel.GetObjectName(i))).ToList();
        if ((uint)Type >= SafariZone4EditorViewModel.ObjectTypeCount)
            list.Add(new SafariZoneChoice(Type, SafariZone4EditorViewModel.GetObjectName(Type)));
        return list;
    }

    public void RefreshLanguage()
    {
        TypeChoices = BuildTypeChoices();
        OnPropertyChanged(nameof(Type));
        Changed();
        OnPropertyChanged(nameof(ProblemText));
    }
}

/// <summary> One tile of the 32 x 32 Safari Zone world. </summary>
public sealed partial class SafariZoneCellViewModel(int x, int z) : ObservableObject
{
    private const string LandColor = "#BEDCA0";
    private const string WaterColor = "#96BEEB";
    private const string NoTerrainColor = "#3C3C3C";

    private bool _isLand, _isWater;

    public int X { get; } = x;
    public int Z { get; } = z;

    /// <summary> Terrain height (Y) or -1 when the tile has no terrain. </summary>
    public int Height { get; private set; } = -1;

    /// <summary> Object drawn on the tile, if any. </summary>
    public SafariZoneObjectRow? Occupant { get; private set; }

    [ObservableProperty] private string _fill = NoTerrainColor;
    [ObservableProperty] private string _labelForeground = "#FFFFFF";
    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private bool _hasProblem;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _accessibleName = string.Empty;

    public void Update(bool knownMap, SafariZoneType4 map, SafariZoneObjectRow? occupant)
    {
        _isWater = knownMap && SafariZoneMap4.IsWater(map, X, Z);
        _isLand = knownMap && SafariZoneMap4.IsLand(map, X, Z);
        Height = knownMap ? SafariZoneMap4.GetHeight(map, X, Z) : -1;
        Occupant = occupant;
        if (occupant is null)
        {
            Fill = _isWater ? WaterColor : _isLand ? LandColor : NoTerrainColor;
            Label = string.Empty;
            HasProblem = false;
        }
        else
        {
            var (fill, bright) = GetColor(occupant.Type, occupant.Index);
            Fill = fill;
            LabelForeground = bright ? "#000000" : "#FFFFFF";
            // Number the object on its anchor tile only.
            Label = occupant.X == X && occupant.Z == Z ? occupant.Number.ToString() : string.Empty;
            HasProblem = occupant.Placement != SafariZonePlacement4.None;
        }
        RefreshLanguage();
    }

    public void RefreshLanguage()
    {
        var s = LocalizedStrings.Instance;
        var name = _isWater ? s.Format("Safari4_CellWater", X, Z, Height)
            : _isLand ? s.Format("Safari4_CellLand", X, Z, Height)
            : s.Format("Safari4_CellNone", X, Z);
        if (Occupant is { } o)
        {
            name = s.Format("Safari4_CellObject", name, o.Number, o.TypeName);
            if (o.ProblemText.Length != 0)
                name = s.Format("Safari4_CellWithProblem", name, o.ProblemText);
        }
        AccessibleName = name;
    }

    /// <summary> Hue is chosen by object type, lightness by row index (matches the upstream visualizer). </summary>
    private static (string Hex, bool Bright) GetColor(int type, int index)
    {
        double h = type * 47 % 360;
        const double s = 0.7;
        double l = 0.35 + (0.07 * (index % 6));
        double c = (1 - Math.Abs((2 * l) - 1)) * s;
        double x = c * (1 - Math.Abs((h / 60 % 2) - 1));
        double m = l - (c / 2);
        var (r, g, b) = (int)(h / 60) switch
        {
            0 => (c, x, 0d),
            1 => (x, c, 0d),
            2 => (0d, c, x),
            3 => (0d, x, c),
            4 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        int R = (int)Math.Round((r + m) * 255), G = (int)Math.Round((g + m) * 255), B = (int)Math.Round((b + m) * 255);
        return ($"#{R:X2}{G:X2}{B:X2}", l > 0.55);
    }
}
