using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class InventoryEditorViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly IReadOnlyList<InventoryPouch> _originalPouches;

    public InventoryEditorViewModel(SaveFile sav)
    {
        _sav = sav;
        _originalPouches = sav.Inventory;

        // Build item name list
        var itemStrings = GameInfo.Strings.GetItemStrings(sav.Context, sav.Version);
        _itemNames = new string[itemStrings.Length];
        for (int i = 0; i < itemStrings.Length; i++)
        {
            _itemNames[i] = string.IsNullOrEmpty(itemStrings[i])
                ? $"(Item #{i:000})"
                : itemStrings[i];
        }

        // Create pouch view models
        foreach (var pouch in _originalPouches)
        {
            Pouches.Add(new InventoryPouchViewModel(pouch, _itemNames));
        }

        if (Pouches.Count > 0)
            SelectedPouch = Pouches[0];
    }

    private readonly string[] _itemNames;

    [ObservableProperty]
    private ObservableCollection<InventoryPouchViewModel> _pouches = [];

    [ObservableProperty]
    private InventoryPouchViewModel? _selectedPouch;

    [RelayCommand]
    private void Save()
    {
        foreach (var pouch in Pouches)
        {
            pouch.ApplyChanges();
        }
        _sav.Inventory = _originalPouches;
    }

    [RelayCommand]
    private void Reset()
    {
        foreach (var pouch in Pouches)
        {
            pouch.LoadFromPouch();
        }
    }

    [RelayCommand]
    private void SortByName()
    {
        SelectedPouch?.SortByName(_itemNames);
    }

    [RelayCommand]
    private void SortByCount()
    {
        SelectedPouch?.SortByCount();
    }

    [RelayCommand]
    private void GiveAll()
    {
        SelectedPouch?.GiveAllItems();
    }

    [RelayCommand]
    private void ClearAll()
    {
        SelectedPouch?.ClearAllItems();
    }
}

public partial class InventoryPouchViewModel : ViewModelBase
{
    private readonly InventoryPouch _pouch;
    private readonly string[] _itemNames;

    public InventoryPouchViewModel(InventoryPouch pouch, string[] itemNames)
    {
        _pouch = pouch;
        _itemNames = itemNames;
        PouchName = pouch.Type.ToString();
        MaxCount = pouch.MaxCount;

        // Build item list for combo box
        var validItems = pouch.Info.GetItems(pouch.Type).ToArray();
        ItemList = validItems
            .Where(id => id < itemNames.Length)
            .Select(id => new ComboItem(itemNames[id], id))
            .OrderBy(x => x.Text)
            .ToList();

        LoadFromPouch();
    }

    public string PouchName { get; }
    public int MaxCount { get; }
    public IReadOnlyList<ComboItem> ItemList { get; }

    [ObservableProperty]
    private ObservableCollection<InventoryItemViewModel> _items = [];

    public void LoadFromPouch()
    {
        Items.Clear();
        foreach (var item in _pouch.Items)
        {
            var name = item.Index < _itemNames.Length ? _itemNames[item.Index] : $"Item #{item.Index}";
            Items.Add(new InventoryItemViewModel(item, name, ItemList, MaxCount));
        }
    }

    public void ApplyChanges()
    {
        for (int i = 0; i < Items.Count && i < _pouch.Items.Length; i++)
        {
            _pouch.Items[i].Index = Items[i].ItemId;
            _pouch.Items[i].Count = Items[i].Count;
        }
    }

    public void SortByName(string[] names)
    {
        var sorted = Items.OrderBy(i => i.ItemId == 0 ? 1 : 0)
                          .ThenBy(i => i.ItemId < names.Length ? names[i.ItemId] : "")
                          .ToList();
        Items.Clear();
        foreach (var item in sorted)
            Items.Add(item);
    }

    public void SortByCount()
    {
        var sorted = Items.OrderBy(i => i.Count == 0 ? 1 : 0)
                          .ThenByDescending(i => i.Count)
                          .ToList();
        Items.Clear();
        foreach (var item in sorted)
            Items.Add(item);
    }

    public void GiveAllItems()
    {
        var validItems = _pouch.Info.GetItems(_pouch.Type);
        int slot = 0;
        foreach (var itemId in validItems)
        {
            if (slot >= Items.Count) break;
            Items[slot].ItemId = itemId;
            Items[slot].Count = MaxCount;
            slot++;
        }
    }

    public void ClearAllItems()
    {
        foreach (var item in Items)
        {
            item.ItemId = 0;
            item.Count = 0;
        }
    }
}

public partial class InventoryItemViewModel : ViewModelBase
{
    private readonly InventoryItem _item;

    public InventoryItemViewModel(InventoryItem item, string name, IReadOnlyList<ComboItem> itemList, int maxCount)
    {
        _item = item;
        _itemId = item.Index;
        _count = item.Count;
        _itemName = name;
        ItemList = itemList;
        MaxCount = maxCount;
    }

    public IReadOnlyList<ComboItem> ItemList { get; }
    public int MaxCount { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ItemName))]
    private int _itemId;

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private string _itemName;

    partial void OnItemIdChanged(int value)
    {
        var item = ItemList.FirstOrDefault(i => i.Value == value);
        ItemName = item?.Text ?? $"Item #{value}";
    }
}
