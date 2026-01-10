using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PKHeX.Avalonia.Models;

public partial class PartySlotData : ObservableObject
{
    [ObservableProperty] private int _slot;
    [ObservableProperty] private ushort _species;
    [ObservableProperty] private Bitmap? _sprite;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _isShiny;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _nickname = string.Empty;
    [ObservableProperty] private byte _level;
    [ObservableProperty] private string _speciesName = string.Empty;
}
