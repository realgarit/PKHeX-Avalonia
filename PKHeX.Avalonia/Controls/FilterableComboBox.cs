using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using PKHeX.Core;

namespace PKHeX.Avalonia.Controls;

/// <summary>A searchable value-backed picker. Popup highlights are drafts until accepted.</summary>
public class FilterableComboBox : AutoCompleteBox
{
    public static readonly StyledProperty<int> SelectedValueProperty =
        AvaloniaProperty.Register<FilterableComboBox, int>(nameof(SelectedValue), defaultBindingMode: BindingMode.TwoWay);

    public int SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    private bool _syncing;
    private bool _editing;
    private ComboItem? _committed;
    private ISelectionAdapter? _acceptanceAdapter;

    protected override Type StyleKeyOverride => typeof(AutoCompleteBox);

    static FilterableComboBox()
    {
        // AutoCompleteBox handles a new ItemsSource in its own class handler, which clears its view and
        // with it SelectedItem and Text. Registered after that one, this handler restores the committed
        // entry last and subscribes to collection changes after AutoCompleteBox does.
        ItemsSourceProperty.Changed.AddClassHandler<FilterableComboBox>((x, e) => x.OnItemsSourceChanged(e));
    }

    public FilterableComboBox()
    {
        FilterMode = AutoCompleteFilterMode.Contains;
        ValueMemberBinding = new Binding("Text");
        MinimumPrefixLength = 0;
        MinimumPopulateDelay = TimeSpan.FromMilliseconds(150);
        MaxDropDownHeight = 300;
        SelectionChanged += OnSelectionChanged;
        GotFocus += OnGotFocus;
        // Clearing the search text repopulates the list and makes AutoCompleteBox raise DropDownClosed
        // again. Restoring the committed text there would filter the list it is about to open.
        DropDownClosed += (_, _) =>
        {
            if (!_syncing)
                RestoreDisplay();
        };
    }

    protected override ISelectionAdapter? GetSelectionAdapterPart(INameScope nameScope)
    {
        if (_acceptanceAdapter is not null)
            _acceptanceAdapter.Commit -= OnAccepted;
        var adapter = base.GetSelectionAdapterPart(nameScope);
        // Subscribe before AutoCompleteBox attaches its own close handler. Mouse and keyboard
        // acceptance publish the candidate before closing restores the committed display.
        if (adapter is not null)
            adapter.Commit += OnAccepted;
        return _acceptanceAdapter = adapter;
    }

    private void OnAccepted(object? sender, RoutedEventArgs e) => AcceptSelection();

    private void OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (_editing || ItemsSource is null)
            return;
        _editing = true;
        _syncing = true;
        try { Text = string.Empty; }
        finally { _syncing = false; }
        IsDropDownOpen = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Do not inspect or rewrite text input: AutoCompleteBox/TextBox retain native IME
        // composition. Native adapter Commit handles Enter. Tab accepts before focus moves.
        if (!e.Handled && e.Key == Key.Tab)
            AcceptSelection();
        if (!e.Handled && !IsDropDownOpen && e.Key is Key.Down or Key.F4)
        {
            _syncing = true;
            try { Text = string.Empty; }
            finally { _syncing = false; }
        }
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Enter)
            AcceptSelection();
        if (e.Key == Key.Escape)
            RestoreDisplay();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedValueProperty)
            SynchronizeSelection();
    }

    private void OnItemsSourceChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.OldValue is INotifyCollectionChanged oldItems)
            oldItems.CollectionChanged -= OnItemsChanged;
        if (ItemsSource is INotifyCollectionChanged newItems)
            newItems.CollectionChanged += OnItemsChanged;
        SynchronizeSelection();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SynchronizeSelection();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (ItemsSource is INotifyCollectionChanged items)
        {
            items.CollectionChanged -= OnItemsChanged;
            items.CollectionChanged += OnItemsChanged;
        }
        SynchronizeSelection();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (ItemsSource is INotifyCollectionChanged items)
            items.CollectionChanged -= OnItemsChanged;
        _editing = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // SelectedItem changes during typing and arrow navigation. Neither may write a bound
        // model (inventory replacement clears several flags). Programmatic selection outside
        // an editing session remains supported for existing value-only consumers.
        if (!_syncing && !_editing && !IsDropDownOpen && SelectedItem is ComboItem entry)
            Commit(entry);
    }

    private void AcceptSelection()
    {
        if (SelectedItem is ComboItem entry && ContainsEntry(entry))
            Commit(entry);
    }

    private void Commit(ComboItem entry)
    {
        _syncing = true;
        try
        {
            SetCurrentValue(SelectedValueProperty, entry.Value);
            _committed = entry;
        }
        finally { _syncing = false; }
        // A model may normalize an unavailable selection or replace its choices while
        // accepting it. Render the resulting bound value, not the stale candidate.
        SynchronizeSelection();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (IsKeyboardFocusWithin)
            return;
        _editing = false;
        RestoreDisplay();
    }

    private void SynchronizeSelection()
    {
        if (_syncing)
            return;
        var entry = _committed;
        if (entry is null || !ContainsEntry(entry) || entry.Value != SelectedValue)
            entry = FindEntry(SelectedValue);
        _committed = entry;
        RestoreDisplay();
    }

    private void RestoreDisplay()
    {
        _syncing = true;
        try
        {
            SelectedItem = _committed;
            Text = _committed?.Text ?? string.Empty;
        }
        finally { _syncing = false; }
    }

    private bool ContainsEntry(ComboItem entry)
    {
        if (ItemsSource is not null)
            foreach (var item in ItemsSource)
                if (ReferenceEquals(item, entry))
                    return true;
        return false;
    }

    private ComboItem? FindEntry(int value)
    {
        if (ItemsSource is not null)
            foreach (var item in ItemsSource)
                if (item is ComboItem entry && entry.Value == value)
                    return entry;
        return null;
    }
}
