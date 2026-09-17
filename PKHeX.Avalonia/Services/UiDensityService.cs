using Avalonia;
using Avalonia.Controls;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// Avalonia implementation of <see cref="IUiDensityService"/>. The service owns the app-wide
/// semantic spacing/control-size resources; individual views consume those resources through
/// <c>DynamicResource</c> rather than embedding density-specific numbers.
/// </summary>
public sealed class UiDensityService : IUiDensityService
{
    private readonly AppSettings _settings;
    private readonly ISettingsStore _settingsStore;

    public UiDensityService(AppSettings settings, ISettingsStore settingsStore)
    {
        _settings = settings;
        _settingsStore = settingsStore;
    }

    public AppDensity CurrentDensity => _settings.Density.Selected;

    /// <summary>Applies the persisted density before the first window is created.</summary>
    public void Initialize() => ApplyResources(_settings.Density.Selected);

    public void ApplyDensity(AppDensity density)
    {
        if (!Enum.IsDefined(density))
            density = AppDensity.Compact;

        _settings.Density.Selected = density;
        _settingsStore.Save(_settings);
        ApplyResources(density);
    }

    private static void ApplyResources(AppDensity density)
    {
        var resources = global::Avalonia.Application.Current?.Resources;
        if (resources is null)
            return;

        var compact = density == AppDensity.Compact;

        Set(resources, UiDensityResourceKeys.CardPadding, compact ? new Thickness(4) : new Thickness(8));
        Set(resources, UiDensityResourceKeys.CardRadius, compact ? new CornerRadius(6) : new CornerRadius(8));
        Set(resources, UiDensityResourceKeys.PrimaryButtonPadding, compact ? new Thickness(16, 8) : new Thickness(20, 10));
        Set(resources, UiDensityResourceKeys.BadgePadding, compact ? new Thickness(6, 3) : new Thickness(8, 4));
        Set(resources, UiDensityResourceKeys.BadgeRadius, compact ? new CornerRadius(12) : new CornerRadius(14));
        Set(resources, UiDensityResourceKeys.HeaderPadding, compact ? new Thickness(12, 8) : new Thickness(16, 10));
        Set(resources, UiDensityResourceKeys.SectionPadding, compact ? new Thickness(6) : new Thickness(12));
        Set(resources, UiDensityResourceKeys.SectionRadius, compact ? new CornerRadius(6) : new CornerRadius(8));
        Set(resources, UiDensityResourceKeys.ViewPadding, compact ? new Thickness(8) : new Thickness(16));
        Set(resources, UiDensityResourceKeys.StatusBarPadding, compact ? new Thickness(8, 4) : new Thickness(12, 6));
        Set(resources, UiDensityResourceKeys.ControlHeight, compact ? 32d : 36d);
        Set(resources, UiDensityResourceKeys.FormFieldPadding, compact ? new Thickness(6, 3) : new Thickness(8, 5));
        Set(resources, UiDensityResourceKeys.InputPadding, compact ? new Thickness(10, 5) : new Thickness(12, 7));
        Set(resources, UiDensityResourceKeys.ComboBoxPadding, compact ? new Thickness(12, 5, 0, 7) : new Thickness(14, 7, 0, 9));
        Set(resources, UiDensityResourceKeys.NumericInputPadding, compact ? new Thickness(10, 4) : new Thickness(12, 6));
        Set(resources, UiDensityResourceKeys.ComboItemPadding, compact ? new Thickness(11, 6) : new Thickness(13, 8));
        Set(resources, UiDensityResourceKeys.PopupPadding, compact ? new Thickness(4) : new Thickness(6));
        Set(resources, UiDensityResourceKeys.ControlRadius, compact ? new CornerRadius(7) : new CornerRadius(9));
        Set(resources, UiDensityResourceKeys.InlineControlHeight, compact ? 24d : 30d);
        Set(resources, UiDensityResourceKeys.InlineControlMinWidth, compact ? 50d : 56d);
        Set(resources, UiDensityResourceKeys.DataGridRowHeight, compact ? 36d : 44d);
        Set(resources, UiDensityResourceKeys.DataGridCellPadding, compact ? new Thickness(6, 3) : new Thickness(10, 6));
        Set(resources, UiDensityResourceKeys.DataGridEditPadding, compact ? new Thickness(4, 2) : new Thickness(8, 4));
        Set(resources, UiDensityResourceKeys.DataGridEditHeight, compact ? 28d : 36d);
        Set(resources, UiDensityResourceKeys.CompactCheckBoxPadding, compact ? new Thickness(4, 2) : new Thickness(8, 4));
        Set(resources, UiDensityResourceKeys.CompactCheckBoxHeight, compact ? 24d : 30d);
        Set(resources, UiDensityResourceKeys.EditorTabPadding, compact ? new Thickness(2, 5) : new Thickness(6, 7));
        Set(resources, UiDensityResourceKeys.EditorTabMargin, compact ? new Thickness(0, 0, 1, 0) : new Thickness(0, 0, 3, 0));
        Set(resources, UiDensityResourceKeys.WorkspaceTabPadding, compact ? new Thickness(12, 8) : new Thickness(16, 10));
        Set(resources, UiDensityResourceKeys.FilterableComboBoxPadding, compact ? new Thickness(10, 6, 6, 5) : new Thickness(12, 8, 8, 7));
        Set(resources, UiDensityResourceKeys.ViewStackSpacing, compact ? 12d : 16d);
        Set(resources, UiDensityResourceKeys.ViewSectionMargin, compact ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 0, 16));
        Set(resources, UiDensityResourceKeys.ViewFooterMargin, compact ? new Thickness(0, 12, 0, 0) : new Thickness(0, 16, 0, 0));
        Set(resources, UiDensityResourceKeys.SectionHeaderMargin, compact ? new Thickness(0, 0, 0, 8) : new Thickness(0, 0, 0, 12));
    }

    private static void Set(IResourceDictionary resources, string key, object value) => resources[key] = value;
}

/// <summary>Stable resource keys shared by the density service and Avalonia styles.</summary>
internal static class UiDensityResourceKeys
{
    public const string CardPadding = "UiDensityCardPadding";
    public const string CardRadius = "UiDensityCardRadius";
    public const string PrimaryButtonPadding = "UiDensityPrimaryButtonPadding";
    public const string BadgePadding = "UiDensityBadgePadding";
    public const string BadgeRadius = "UiDensityBadgeRadius";
    public const string HeaderPadding = "UiDensityHeaderPadding";
    public const string SectionPadding = "UiDensitySectionPadding";
    public const string SectionRadius = "UiDensitySectionRadius";
    public const string ViewPadding = "UiDensityViewPadding";
    public const string StatusBarPadding = "UiDensityStatusBarPadding";
    public const string ControlHeight = "UiDensityControlHeight";
    public const string FormFieldPadding = "UiDensityFormFieldPadding";
    public const string InputPadding = "UiDensityInputPadding";
    public const string ComboBoxPadding = "UiDensityComboBoxPadding";
    public const string NumericInputPadding = "UiDensityNumericInputPadding";
    public const string ComboItemPadding = "UiDensityComboItemPadding";
    public const string PopupPadding = "UiDensityPopupPadding";
    public const string ControlRadius = "UiDensityControlRadius";
    public const string InlineControlHeight = "UiDensityInlineControlHeight";
    public const string InlineControlMinWidth = "UiDensityInlineControlMinWidth";
    public const string DataGridRowHeight = "UiDensityDataGridRowHeight";
    public const string DataGridCellPadding = "UiDensityDataGridCellPadding";
    public const string DataGridEditPadding = "UiDensityDataGridEditPadding";
    public const string DataGridEditHeight = "UiDensityDataGridEditHeight";
    public const string CompactCheckBoxPadding = "UiDensityCompactCheckBoxPadding";
    public const string CompactCheckBoxHeight = "UiDensityCompactCheckBoxHeight";
    public const string EditorTabPadding = "UiDensityEditorTabPadding";
    public const string EditorTabMargin = "UiDensityEditorTabMargin";
    public const string WorkspaceTabPadding = "UiDensityWorkspaceTabPadding";
    public const string FilterableComboBoxPadding = "UiDensityFilterableComboBoxPadding";
    public const string ViewStackSpacing = "UiDensityViewStackSpacing";
    public const string ViewSectionMargin = "UiDensityViewSectionMargin";
    public const string ViewFooterMargin = "UiDensityViewFooterMargin";
    public const string SectionHeaderMargin = "UiDensitySectionHeaderMargin";
}
