using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class InventoryEditorViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly ISpriteRenderer _spriteRenderer;
    private readonly PlayerBag? _bag;
    private readonly bool _haXMode;
    private readonly IReadOnlyList<InventoryPouch> _originalPouches;

    public InventoryEditorViewModel(SaveFile sav, ISpriteRenderer spriteRenderer, bool haXMode = false)
    {
        _sav = sav;
        _spriteRenderer = spriteRenderer;
        _haXMode = haXMode;

        // sav.Inventory can throw on blank SCBlock-based saves (LA, SV, ZA) where
        // blocks have Type=None and are not yet populated. Fall back to empty.
        PlayerBag? bag = null;
        IReadOnlyList<InventoryPouch> pouches;
        try
        {
            bag = sav.Inventory;
            pouches = bag.Pouches;
        }
        catch
        {
            pouches = Array.Empty<InventoryPouch>();
        }
        _bag = bag;
        _originalPouches = pouches;

        var availableItemIds = _haXMode && sav.Generation > 1
            ? GameInfo.Sources.GetItemDataSource(sav.Version, sav.Context, sav.HeldItems, true)
                .Where(item => item.Value <= sav.MaxItemID)
                .Select(item => item.Value)
                .ToArray()
            : null;
        var maxItemCount = _haXMode && bag is not null ? bag.MaxQuantityHaX : (int?)null;

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
            Pouches.Add(new InventoryPouchViewModel(pouch, _itemNames, _spriteRenderer, availableItemIds, maxItemCount, sav.Context, sav.Version));
        }

        if (Pouches.Count > 0)
            SelectedPouch = Pouches[0];
    }

    private string[] _itemNames;

    public void RefreshLanguage()
    {
        // Rebuild item name list
        var itemStrings = GameInfo.Strings.GetItemStrings(_sav.Context, _sav.Version);
        for (int i = 0; i < itemStrings.Length && i < _itemNames.Length; i++)
        {
            _itemNames[i] = string.IsNullOrEmpty(itemStrings[i])
                ? $"(Item #{i:000})"
                : itemStrings[i];
        }

        // Notify pouches to refresh their item lists
        foreach (var pouch in Pouches)
        {
            pouch.RefreshLanguage();
        }
    }

    [ObservableProperty]
    private ObservableCollection<InventoryPouchViewModel> _pouches = [];

    [ObservableProperty]
    private InventoryPouchViewModel? _selectedPouch;

    [RelayCommand]
    private void Save()
    {
        var changed = _sav is SAV9ZA
            ? Pouches.Any(p => p.Items.Any(i => i.HasChanges))
            : Pouches.Any(p => p.HasChanges);
        if (!changed)
            return;
        if (_sav is SAV9ZA za)
        {
            // ZA is an item-indexed table. Generic bag writes normalize untouched slots
            // and discard flag edits on empty items. Write changed records only.
            foreach (var pouch in Pouches)
                pouch.WriteZaRecords(za);
        }
        else
        {
            foreach (var pouch in Pouches)
                pouch.ApplyChanges();
            _bag?.CopyTo(_sav);
        }
        _sav.State.Edited = true;
        foreach (var pouch in Pouches)
            pouch.LoadFromPouch();
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
    private readonly EntityContext _context;
    private readonly GameVersion _version;
    private readonly InventoryPouch _pouch;
    private readonly string[] _itemNames;
    private readonly ISpriteRenderer _spriteRenderer;
    private readonly IReadOnlyList<int> _availableItemIds;

    public InventoryPouchViewModel(InventoryPouch pouch, string[] itemNames, ISpriteRenderer spriteRenderer, IReadOnlyList<int>? availableItemIds = null, int? maxCount = null, EntityContext context = EntityContext.Gen4, GameVersion version = GameVersion.Any)
    {
        _context = context;
        _version = version;
        _pouch = pouch;
        _itemNames = itemNames;
        _spriteRenderer = spriteRenderer;
        _availableItemIds = availableItemIds ?? pouch.GetAllItems().ToArray().Select(id => (int)id).ToArray();
        MaxCount = maxCount ?? pouch.MaxCount;

        // Build item list for combo box
        var validItems = _availableItemIds;
        ItemList = validItems
            .Where(id => id < itemNames.Length)
            .Select(id => new ComboItem(itemNames[id], id))
            .OrderBy(x => x.Text)
            .ToList();

        LoadFromPouch();
    }

    /// <summary>Core pouch category; the view maps it to the shared pouch icon.</summary>
    public InventoryType PouchType => _pouch.Type;

    /// <summary>Localized pouch caption ("Key Items", "TMs &amp; HMs"), never the enum identifier.</summary>
    public string PouchName => GetPouchName(_pouch.Type);

    /// <summary>Localized display name of a Core pouch category.</summary>
    public static string GetPouchName(InventoryType type) => LocalizedStrings.Instance[$"InventoryPouch_{type}"];
    public int MaxCount { get; }
    public bool SupportsFavorite => _pouch.Items.Any(i => i is IItemFavorite);
    public bool SupportsNew => _pouch.Items.Any(i => i is IItemNewFlag);
    public bool SupportsShopNew => _pouch.Items.Any(i => i is IItemNewShopFlag);
    public bool SupportsHeld => _pouch.Items.Any(i => i is IItemHeldFlag);
    public bool HasChanges => !Items.Select(i => i.CreateRecord()).SequenceEqual(_pouch.Items);
    [ObservableProperty] private IReadOnlyList<ComboItem> _itemList;

    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(PouchName));

        // Rebuild item list for combo box
        var validItems = _availableItemIds;
        ItemList = validItems
            .Where(id => id < _itemNames.Length)
            .Select(id => new ComboItem(_itemNames[id], id))
            .OrderBy(x => x.Text)
            .ToList();

        // Refresh current items display names
        foreach (var item in Items)
        {
            item.RefreshLanguage();
        }
    }

    [ObservableProperty]
    private ObservableCollection<InventoryItemViewModel> _items = [];

    public void LoadFromPouch()
    {
        Items.Clear();
        foreach (var item in _pouch.Items)
        {
            var name = item.Index < _itemNames.Length ? _itemNames[item.Index] : $"Item #{item.Index}";
            Items.Add(new InventoryItemViewModel(item, name, ItemList, MaxCount, _spriteRenderer, _context, _version));
        }
    }

    public void ApplyChanges()
    {
        for (int i = 0; i < Items.Count && i < _pouch.Items.Length; i++)
        {
            _pouch.Items[i] = Items[i].CreateRecord();
        }
    }

    public void WriteZaRecords(SAV9ZA save)
    {
        var changed = Items.Where(row => row.HasChanges).ToArray();
        foreach (var row in changed)
        {
            var originalIndex = row.OriginalItemId;
            if (row.ItemId != originalIndex && originalIndex > 0)
            {
                var removed = save.Items.GetItem((ushort)originalIndex);
                removed.Count = 0;
                removed.IsFavorite = removed.IsNew = removed.IsNewShop = removed.IsHeld = false;
                removed.Write(InventoryPouch9a.GetItemSpan(save.Items.Data, (ushort)originalIndex));
            }
        }
        foreach (var row in changed)
        {
            if (row.CreateRecord() is not InventoryItem9a record)
                continue;
            if (record.Index == 0)
                continue;
            if (record.Index != row.OriginalItemId || record.Count != row.OriginalCount)
                record.Pouch = MyItem9a.GetPouchIndex(_pouch.Type);
            record.Write(InventoryPouch9a.GetItemSpan(save.Items.Data, (ushort)record.Index));
        }
        // Reload the bag too, so Reset after Apply reflects committed flags.
        _pouch.GetPouch(save.Items.Data);
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
        var validItems = _availableItemIds;
        var existing = Items.Where(i => i.ItemId != 0).GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.First());
        int slot = 0;
        foreach (var itemId in validItems)
        {
            if (slot >= Items.Count) break;
            if (!existing.TryGetValue(itemId, out var row))
            {
                var item = _pouch.GetEmpty(itemId);
                row = new InventoryItemViewModel(item, _itemNames[itemId], ItemList, MaxCount,
                    _spriteRenderer, _context, _version);
            }
            row.Count = MaxCount;
            Items[slot] = row;
            slot++;
        }
    }

    public void ClearAllItems()
    {
        foreach (var item in Items)
        {
            item.ItemId = 0;
            item.Count = 0;
            item.ClearStatus();
        }
    }
}

public partial class InventoryItemViewModel : ViewModelBase
{
    private readonly EntityContext _context;
    private readonly GameVersion _version;
    private readonly InventoryItem _item;
    private readonly ISpriteRenderer _spriteRenderer;

    public InventoryItemViewModel(InventoryItem item, string name, IReadOnlyList<ComboItem> itemList, int maxCount, ISpriteRenderer spriteRenderer, EntityContext context = EntityContext.Gen4, GameVersion version = GameVersion.Any)
    {
        _context = context;
        _version = version;
        _item = item with { };
        _itemId = item.Index;
        _count = item.Count;
        _itemName = name;
        ItemList = itemList;
        MaxCount = maxCount;
        _spriteRenderer = spriteRenderer;
        _isFavorite = (item as IItemFavorite)?.IsFavorite ?? false;
        _isNew = (item as IItemNewFlag)?.IsNew ?? false;
        _isNewShop = (item as IItemNewShopFlag)?.IsNewShop ?? false;
        _isHeld = (item as IItemHeldFlag)?.IsHeld ?? false;
    }

    public int OriginalItemId => _item.Index;
    public int OriginalCount => _item.Count;
    public bool HasChanges => CreateRecord() != _item;
    public bool SupportsFavorite => _item is IItemFavorite;
    public bool SupportsNew => _item is IItemNewFlag;
    public bool SupportsShopNew => _item is IItemNewShopFlag;
    public bool SupportsHeld => _item is IItemHeldFlag;
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private bool _isNewShop;
    [ObservableProperty] private bool _isHeld;

    public void ClearStatus() => IsFavorite = IsNew = IsNewShop = IsHeld = false;

    public InventoryItem CreateRecord()
    {
        var record = _item with { Index = ItemId, Count = Count };
        if (record is IItemFavorite favorite) favorite.IsFavorite = IsFavorite;
        if (record is IItemNewFlag newFlag) newFlag.IsNew = IsNew;
        if (record is IItemNewShopFlag shop) shop.IsNewShop = IsNewShop;
        if (record is IItemHeldFlag held) held.IsHeld = IsHeld;
        return record;
    }

    [ObservableProperty] private IReadOnlyList<ComboItem> _itemList;
    public int MaxCount { get; }

    public byte[]? Sprite => _spriteRenderer.GetItemSprite(ItemId, _context, _version);

    public void RefreshLanguage()
    {
        // Refresh item name based on current ID and list
        var item = ItemList.FirstOrDefault(i => i.Value == ItemId);
        ItemName = item?.Text ?? $"Item #{ItemId}";
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ItemName))]
    private int _itemId;

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private string _itemName;

    partial void OnItemIdChanged(int value)
    {
        // Status describes the item identity; replacement/clearing starts unmarked.
        ClearStatus();
        var item = ItemList.FirstOrDefault(i => i.Value == value);
        ItemName = item?.Text ?? $"Item #{value}";
        OnPropertyChanged(nameof(Sprite));
    }
}
