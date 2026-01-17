using CommunityToolkit.Mvvm.ComponentModel;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

/// <summary>
/// ViewModel wrapper for a single ribbon, supporting both boolean and byte (count) ribbons.
/// </summary>
public partial class RibbonItemViewModel : ObservableObject
{
    private readonly PKM _pk;
    private readonly string _propertyName;
    
    public string DisplayName { get; }
    public bool IsBooleanRibbon { get; }
    public int MaxCount { get; }
    
    [ObservableProperty]
    private bool _hasRibbon;
    
    [ObservableProperty]
    private int _ribbonCount;

    [ObservableProperty]
    private global::Avalonia.Media.Imaging.Bitmap? _icon;
    
    public RibbonItemViewModel(PKM pk, RibbonInfo info)
    {
        _pk = pk;
        _propertyName = info.Name;
        IsBooleanRibbon = info.Type == RibbonValueType.Boolean;
        
        // Clean up display name by removing "Ribbon" prefix
        DisplayName = info.Name.StartsWith("Ribbon") 
            ? info.Name[6..] // Remove "Ribbon" prefix
            : info.Name;
        
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
