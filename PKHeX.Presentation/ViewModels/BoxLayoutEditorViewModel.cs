using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Application.Abstractions;
using PKHeX.Core;

namespace PKHeX.Presentation.ViewModels;

public partial class BoxLayoutEditorViewModel : ViewModelBase, ICloseableDialog
{
    private readonly SaveFile _source;
    private readonly SaveFile _working;
    private readonly IBoxDetailNameRead? _nameReader;
    private readonly IBoxDetailName? _nameWriter;
    private readonly IBoxDetailWallpaper? _wallpaper;

    public Action? CloseRequested { get; set; }

    public BoxLayoutEditorViewModel(SaveFile sav)
    {
        _source = sav;
        var editedBeforeClone = sav.State.Edited;
        _working = sav.Clone();
        sav.State.Edited = editedBeforeClone;
        _nameReader = _working as IBoxDetailNameRead;
        _nameWriter = _working as IBoxDetailName;
        _wallpaper = _working as IBoxDetailWallpaper;

        IsSupported = _nameReader is not null || _wallpaper is not null;
        CanEditNames = _nameWriter is not null;
        CanEditWallpaper = _wallpaper is not null;
        CanEditUnlocked = _working.BoxesUnlocked > 0;

        // Build list of possible unlocked box counts
        for (int i = 0; i <= _working.BoxCount; i++)
            UnlockedOptions.Add(i);
        
        _unlockedBoxes = Math.Min(_working.BoxCount, _working.BoxesUnlocked);

        LoadWallpaperNames();
        LoadBoxes();
    }

    public bool IsSupported { get; }
    public bool CanEditNames { get; }
    public bool CanEditWallpaper { get; }
    public bool CanEditUnlocked { get; }

    [ObservableProperty]
    private ObservableCollection<BoxLayoutItemViewModel> _boxes = [];

    [ObservableProperty]
    private ObservableCollection<int> _unlockedOptions = [];

    [ObservableProperty]
    private ObservableCollection<string> _wallpaperNames = [];

    [ObservableProperty]
    private int _unlockedBoxes;

    partial void OnUnlockedBoxesChanged(int value)
    {
        if (CanEditUnlocked && value >= 0 && value <= _working.BoxCount)
            _working.BoxesUnlocked = value;
    }

    private void LoadWallpaperNames()
    {
        WallpaperNames.Clear();
        var names = GameInfo.Strings.wallpapernames;
        
        int count = _working.Generation switch
        {
            3 when _working is SAV3 or SAV3RSBox => 16,
            4 or 5 or 6 => 24,
            7 => 16,
            8 when _working is SAV8BS => 32,
            8 => 19,
            9 => 20,
            _ => 0
        };

        for (int i = 0; i < count; i++)
        {
            if (i < names.Length)
                WallpaperNames.Add(names[i]);
            else
                WallpaperNames.Add($"Wallpaper {i + 1}");
        }
    }

    private void LoadBoxes()
    {
        Boxes.Clear();
        for (int i = 0; i < _working.BoxCount; i++)
        {
            var name = _nameReader?.GetBoxName(i) ?? BoxDetailNameExtensions.GetDefaultBoxName(i);
            var wallpaper = _wallpaper?.GetBoxWallpaper(i) ?? 0;
            Boxes.Add(new BoxLayoutItemViewModel(i, _working.BoxCount, name, wallpaper, OnBoxNameChanged, OnBoxWallpaperChanged));
        }
    }

    private void OnBoxNameChanged(int box, string name)
    {
        _nameWriter?.SetBoxName(box, name);
    }

    private void OnBoxWallpaperChanged(int box, int wallpaper)
    {
        if (_wallpaper is not null && wallpaper >= 0 && wallpaper < WallpaperNames.Count)
            _wallpaper.SetBoxWallpaper(box, wallpaper);
    }

    [RelayCommand]
    private void Save()
    {
        if (!IsSupported)
            return;

        _source.CopyChangesFrom(_working);
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    [RelayCommand]
    private void MoveUp(BoxLayoutItemViewModel? box)
    {
        if (box is null || box.Index <= 0)
            return;

        if (_working.SwapBox(box.Index, box.Index - 1))
            LoadBoxes();
    }

    [RelayCommand]
    private void MoveDown(BoxLayoutItemViewModel? box)
    {
        if (box is null || box.Index >= _working.BoxCount - 1)
            return;

        if (_working.SwapBox(box.Index, box.Index + 1))
            LoadBoxes();
    }
}

public partial class BoxLayoutItemViewModel : ViewModelBase
{
    private readonly Action<int, string> _onNameChanged;
    private readonly Action<int, int> _onWallpaperChanged;

    public BoxLayoutItemViewModel(int index, int boxCount, string name, int wallpaper, Action<int, string> onNameChanged, Action<int, int> onWallpaperChanged)
    {
        Index = index;
        BoxCount = boxCount;
        _name = name;
        _wallpaper = wallpaper;
        _onNameChanged = onNameChanged;
        _onWallpaperChanged = onWallpaperChanged;
    }

    public int Index { get; }
    public int BoxCount { get; }
    public string DisplayIndex => $"Box {Index + 1}";
    public bool CanMoveUp => Index > 0;
    public bool CanMoveDown => Index < BoxCount - 1;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private int _wallpaper;

    partial void OnNameChanged(string value) => _onNameChanged(Index, value);
    partial void OnWallpaperChanged(int value) => _onWallpaperChanged(Index, value);
}
