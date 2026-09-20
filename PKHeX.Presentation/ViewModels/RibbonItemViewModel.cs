using CommunityToolkit.Mvvm.ComponentModel;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// ViewModel wrapper for a single ribbon, supporting both boolean and byte (count) ribbons.
/// </summary>
public partial class RibbonItemViewModel : ObservableObject
{
    private readonly PKM _pk;
    private readonly string _propertyName;
    
    public string DisplayName { get; }
    public string StatusText { get; }
    public bool HasStatus => !string.IsNullOrEmpty(StatusText);
    public bool IsBooleanRibbon { get; }
    public int MaxCount { get; }
    
    [ObservableProperty]
    private bool _hasRibbon;
    
    [ObservableProperty]
    private int _ribbonCount;

    /// <summary>Bare ribbon icon resource name (lowercased). The View resolves it to an image asset.</summary>
    [ObservableProperty]
    private string? _iconResource;
    
    public RibbonItemViewModel(PKM pk, RibbonInfo info, RibbonResult? verification = null)
    {
        _pk = pk;
        _propertyName = info.Name;
        IsBooleanRibbon = info.Type == RibbonValueType.Boolean;
        
        DisplayName = GameInfo.Strings.Ribbons.GetNameSafe(info.Name, out var localizedName)
            ? localizedName
            : info.Name.StartsWith("Ribbon") ? info.Name[6..] : info.Name;

        StatusText = verification is null
            ? LocalizedStrings.Instance["RibbonEditor_Valid"]
            : verification.Value.IsMissing
                ? LocalizedStrings.Instance["RibbonEditor_Missing"]
                : LocalizedStrings.Instance["RibbonEditor_Invalid"];
        
        if (IsBooleanRibbon)
        {
            HasRibbon = info.HasRibbon;
            MaxCount = 0;
        }
        else
        {
            RibbonCount = info.RibbonCount;
            MaxCount = info.MaxCount;
        }
    }
    
    partial void OnHasRibbonChanged(bool value)
    {
        if (IsBooleanRibbon)
        {
            ReflectUtil.SetValue(_pk, _propertyName, value);
        }
    }
    
    partial void OnRibbonCountChanged(int value)
    {
        if (!IsBooleanRibbon)
        {
            ReflectUtil.SetValue(_pk, _propertyName, (byte)value);
        }
    }
}
