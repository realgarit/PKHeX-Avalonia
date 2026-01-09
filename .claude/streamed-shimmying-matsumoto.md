# PKHeX Avalonia Migration - Vertical Slice Action Plan

## Executive Summary

This plan creates a **cross-platform proof-of-concept** for PKHeX using Avalonia UI, targeting **.NET 10 Preview**. The vertical slice demonstrates:
- Main window shell with Fluent theme
- Save file loading/saving (via PKHeX.Core)
- Box Viewer with native Avalonia rendering (no System.Drawing)
- Full keyboard navigation between slots

**Key Decisions:**
- **Target:** .NET 10 Preview (cutting edge, test future compatibility)
- **Sprites:** Placeholder rendering (colored boxes with species numbers)
- **Navigation:** Full keyboard support (arrow keys between slots)

**Guiding Principles:** SRP, DIP, DRY, YAGNI, KISS

---

## 1. Project Structure

```
PKHeX.Avalonia/
├── App.axaml                    # Application definition
├── App.axaml.cs                 # DI container setup
├── Program.cs                   # Entry point
├── ViewLocator.cs               # View resolution for MVVM
├── Models/
│   └── SlotData.cs              # Box slot view model data
├── Services/
│   ├── ISaveFileService.cs      # Save file operations interface
│   ├── SaveFileService.cs       # PKHeX.Core wrapper
│   ├── IDialogService.cs        # File picker abstraction
│   ├── DialogService.cs         # Avalonia file dialogs
│   ├── ISpriteRenderer.cs       # Sprite rendering abstraction
│   └── AvaloniaSpriteRenderer.cs # SkiaSharp/DrawingContext impl
├── ViewModels/
│   ├── ViewModelBase.cs         # INPC base class
│   ├── MainWindowViewModel.cs   # Main window state
│   └── BoxViewerViewModel.cs    # Box viewer state
├── Views/
│   ├── MainWindow.axaml         # Main window layout
│   ├── MainWindow.axaml.cs      # Minimal code-behind
│   └── BoxViewer.axaml          # Box viewer control
├── Controls/
│   └── BoxSlotControl.axaml     # Reusable slot (DRY)
└── Resources/
    └── Sprites/                 # Sprite assets (if needed)
```

---

## 2. NuGet Packages

```xml
<!-- PKHeX.Avalonia.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>

  <ItemGroup>
    <!-- Avalonia UI -->
    <PackageReference Include="Avalonia" Version="11.2.3" />
    <PackageReference Include="Avalonia.Desktop" Version="11.2.3" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.2.3" />
    <PackageReference Include="Avalonia.Fonts.Inter" Version="11.2.3" />

    <!-- MVVM -->
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />

    <!-- DI -->
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.1" />

    <!-- Rendering (for sprites) -->
    <PackageReference Include="Avalonia.Skia" Version="11.2.3" />
    <PackageReference Include="SkiaSharp" Version="3.116.1" />

    <!-- Diagnostics (dev only) -->
    <PackageReference Include="Avalonia.Diagnostics" Version="11.2.3" Condition="'$(Configuration)' == 'Debug'" />
  </ItemGroup>

  <ItemGroup>
    <!-- Reference existing PKHeX.Core (no modifications!) -->
    <ProjectReference Include="..\PKHeX.Core\PKHeX.Core.csproj" />
  </ItemGroup>
</Project>
```

> **Note:** Versions listed are latest stable as of Jan 2025. For .NET 10 preview, verify compatibility.

---

## 3. Step-by-Step Implementation Guide

### Phase 1: Project Setup (Steps 1-4)

#### Step 1: Create Avalonia Project
```bash
# From PKHeX solution root
dotnet new avalonia.app -n PKHeX.Avalonia -o PKHeX.Avalonia
```

#### Step 2: Add to Solution
```bash
dotnet sln PKHeX.sln add PKHeX.Avalonia/PKHeX.Avalonia.csproj
```

#### Step 3: Configure .csproj
Replace generated `.csproj` with the NuGet configuration from Section 2.

#### Step 4: Create Folder Structure
Create the directories outlined in Section 1:
- `Models/`, `Services/`, `ViewModels/`, `Views/`, `Controls/`, `Resources/`

---

### Phase 2: Core Infrastructure (Steps 5-9)

#### Step 5: Program.cs (Entry Point)
```csharp
// PKHeX.Avalonia/Program.cs
using Avalonia;
using System;

namespace PKHeX.Avalonia;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
```

#### Step 6: App.axaml (Application Definition)
```xml
<!-- PKHeX.Avalonia/App.axaml -->
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="PKHeX.Avalonia.App"
             RequestedThemeVariant="Default">
    <Application.Styles>
        <FluentTheme />
    </Application.Styles>
</Application>
```

#### Step 7: App.axaml.cs (DI Container Setup)
```csharp
// PKHeX.Avalonia/App.axaml.cs
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.ViewModels;
using PKHeX.Avalonia.Views;
using System;

namespace PKHeX.Avalonia;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Build DI container
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Services (Singleton - shared state)
        services.AddSingleton<ISaveFileService, SaveFileService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ISpriteRenderer, AvaloniaSpriteRenderer>();

        // ViewModels (Transient - created fresh each time)
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<BoxViewerViewModel>();
    }
}
```

#### Step 8: ViewModelBase.cs
```csharp
// PKHeX.Avalonia/ViewModels/ViewModelBase.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace PKHeX.Avalonia.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
}
```

#### Step 9: Service Interfaces

**ISaveFileService.cs:**
```csharp
// PKHeX.Avalonia/Services/ISaveFileService.cs
using PKHeX.Core;
using System.Threading.Tasks;

namespace PKHeX.Avalonia.Services;

public interface ISaveFileService
{
    SaveFile? CurrentSave { get; }
    bool HasSave { get; }

    Task<bool> LoadSaveFileAsync(string path);
    Task<bool> SaveFileAsync(string? path = null);
    void CloseSave();

    event Action<SaveFile?>? SaveFileChanged;
}
```

**IDialogService.cs:**
```csharp
// PKHeX.Avalonia/Services/IDialogService.cs
using System.Threading.Tasks;

namespace PKHeX.Avalonia.Services;

public interface IDialogService
{
    Task<string?> OpenFileAsync(string title, string[]? filters = null);
    Task<string?> SaveFileAsync(string title, string? defaultFileName = null, string[]? filters = null);
    Task ShowErrorAsync(string title, string message);
}
```

**ISpriteRenderer.cs:**
```csharp
// PKHeX.Avalonia/Services/ISpriteRenderer.cs
using Avalonia.Media.Imaging;
using PKHeX.Core;

namespace PKHeX.Avalonia.Services;

public interface ISpriteRenderer
{
    Bitmap? GetSprite(PKM pk, bool isEgg = false);
    Bitmap? GetEmptySlot();
    void Initialize(SaveFile sav);
}
```

---

### Phase 3: Service Implementations (Steps 10-12)

#### Step 10: SaveFileService.cs
```csharp
// PKHeX.Avalonia/Services/SaveFileService.cs
using PKHeX.Core;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PKHeX.Avalonia.Services;

public sealed class SaveFileService : ISaveFileService
{
    public SaveFile? CurrentSave { get; private set; }
    public bool HasSave => CurrentSave is not null;

    private string? _currentPath;

    public event Action<SaveFile?>? SaveFileChanged;

    public Task<bool> LoadSaveFileAsync(string path)
    {
        return Task.Run(() =>
        {
            try
            {
                var data = File.ReadAllBytes(path);
                if (!SaveUtil.TryGetSaveFile(data, out var sav) || sav is null)
                    return false;

                CurrentSave = sav;
                _currentPath = path;

                SaveFileChanged?.Invoke(CurrentSave);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public Task<bool> SaveFileAsync(string? path = null)
    {
        return Task.Run(() =>
        {
            if (CurrentSave is null)
                return false;

            try
            {
                var savePath = path ?? _currentPath;
                if (string.IsNullOrEmpty(savePath))
                    return false;

                var data = CurrentSave.Write().ToArray();
                File.WriteAllBytes(savePath, data);

                if (path is not null)
                    _currentPath = path;

                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public void CloseSave()
    {
        CurrentSave = null;
        _currentPath = null;
        SaveFileChanged?.Invoke(null);
    }
}
```

#### Step 11: DialogService.cs
```csharp
// PKHeX.Avalonia/Services/DialogService.cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System.Linq;
using System.Threading.Tasks;

namespace PKHeX.Avalonia.Services;

public sealed class DialogService : IDialogService
{
    private Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

    public async Task<string?> OpenFileAsync(string title, string[]? filters = null)
    {
        var window = GetMainWindow();
        if (window is null) return null;

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters?.Select(f => new FilePickerFileType(f) { Patterns = [f] }).ToList()
        };

        var result = await window.StorageProvider.OpenFilePickerAsync(options);
        return result.FirstOrDefault()?.Path.LocalPath;
    }

    public async Task<string?> SaveFileAsync(string title, string? defaultFileName = null, string[]? filters = null)
    {
        var window = GetMainWindow();
        if (window is null) return null;

        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName
        };

        var result = await window.StorageProvider.SaveFilePickerAsync(options);
        return result?.Path.LocalPath;
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        var window = GetMainWindow();
        if (window is null) return;

        // Simple message box (could use a proper dialog library)
        var dialog = new Window
        {
            Title = title,
            Width = 300,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new TextBlock { Text = message, Margin = new Thickness(20) }
        };
        await dialog.ShowDialog(window);
    }
}
```

#### Step 12: AvaloniaSpriteRenderer.cs (Initial Skeleton)
```csharp
// PKHeX.Avalonia/Services/AvaloniaSpriteRenderer.cs
using Avalonia.Media.Imaging;
using PKHeX.Core;
using SkiaSharp;
using System;
using System.IO;

namespace PKHeX.Avalonia.Services;

/// <summary>
/// Renders PKM sprites using SkiaSharp (no System.Drawing).
/// This is a placeholder that will need sprite asset loading logic.
/// </summary>
public sealed class AvaloniaSpriteRenderer : ISpriteRenderer
{
    private const int SpriteWidth = 68;
    private const int SpriteHeight = 56;

    public void Initialize(SaveFile sav)
    {
        // Initialize sprite builder context if needed
        // Could determine sprite style based on generation
    }

    public Bitmap? GetSprite(PKM pk, bool isEgg = false)
    {
        if (pk.Species == 0)
            return GetEmptySlot();

        // TODO: Load actual sprite from resources
        // For now, create a colored placeholder
        return CreatePlaceholderSprite(pk);
    }

    public Bitmap? GetEmptySlot()
    {
        return CreateEmptySlotBitmap();
    }

    private Bitmap CreatePlaceholderSprite(PKM pk)
    {
        using var surface = SKSurface.Create(new SKImageInfo(SpriteWidth, SpriteHeight));
        var canvas = surface.Canvas;

        // Background based on type
        var color = pk.IsShiny
            ? new SKColor(255, 215, 0, 128) // Gold tint for shiny
            : new SKColor(100, 150, 200, 128); // Blue-ish

        canvas.Clear(color);

        // Draw species number for identification
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 14,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };
        canvas.DrawText($"#{pk.Species}", SpriteWidth / 2, SpriteHeight / 2 + 5, paint);

        return ConvertToBitmap(surface);
    }

    private Bitmap CreateEmptySlotBitmap()
    {
        using var surface = SKSurface.Create(new SKImageInfo(SpriteWidth, SpriteHeight));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        return ConvertToBitmap(surface);
    }

    private static Bitmap ConvertToBitmap(SKSurface surface)
    {
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        return new Bitmap(stream);
    }
}
```

---

### Phase 4: ViewModels (Steps 13-14)

#### Step 13: MainWindowViewModel.cs
```csharp
// PKHeX.Avalonia/ViewModels/MainWindowViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using System.Threading.Tasks;

namespace PKHeX.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ISaveFileService _saveFileService;
    private readonly IDialogService _dialogService;
    private readonly ISpriteRenderer _spriteRenderer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSave))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private SaveFile? _currentSave;

    [ObservableProperty]
    private BoxViewerViewModel? _boxViewer;

    public bool HasSave => CurrentSave is not null;

    public string WindowTitle => CurrentSave is not null
        ? $"PKHeX Avalonia - {CurrentSave.Version}"
        : "PKHeX Avalonia";

    public MainWindowViewModel(
        ISaveFileService saveFileService,
        IDialogService dialogService,
        ISpriteRenderer spriteRenderer)
    {
        _saveFileService = saveFileService;
        _dialogService = dialogService;
        _spriteRenderer = spriteRenderer;

        _saveFileService.SaveFileChanged += OnSaveFileChanged;
    }

    private void OnSaveFileChanged(SaveFile? sav)
    {
        CurrentSave = sav;
        if (sav is not null)
        {
            _spriteRenderer.Initialize(sav);
            BoxViewer = new BoxViewerViewModel(sav, _spriteRenderer);
        }
        else
        {
            BoxViewer = null;
        }
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var path = await _dialogService.OpenFileAsync(
            "Open Save File",
            ["*.sav", "*.bin", "*.*"]);

        if (string.IsNullOrEmpty(path))
            return;

        var success = await _saveFileService.LoadSaveFileAsync(path);
        if (!success)
        {
            await _dialogService.ShowErrorAsync("Error", "Failed to load save file.");
        }
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task SaveFileAsync()
    {
        var success = await _saveFileService.SaveFileAsync();
        if (!success)
        {
            await _dialogService.ShowErrorAsync("Error", "Failed to save file.");
        }
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private async Task SaveFileAsAsync()
    {
        var path = await _dialogService.SaveFileAsync(
            "Save As",
            CurrentSave?.Metadata.FileName);

        if (string.IsNullOrEmpty(path))
            return;

        var success = await _saveFileService.SaveFileAsync(path);
        if (!success)
        {
            await _dialogService.ShowErrorAsync("Error", "Failed to save file.");
        }
    }

    [RelayCommand(CanExecute = nameof(HasSave))]
    private void CloseFile()
    {
        _saveFileService.CloseSave();
    }
}
```

#### Step 14: BoxViewerViewModel.cs
```csharp
// PKHeX.Avalonia/ViewModels/BoxViewerViewModel.cs
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Avalonia.Models;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using System.Collections.ObjectModel;

namespace PKHeX.Avalonia.ViewModels;

public partial class BoxViewerViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly ISpriteRenderer _spriteRenderer;

    private const int Columns = 6;

    [ObservableProperty]
    private int _currentBox;

    [ObservableProperty]
    private string _boxName = string.Empty;

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private ObservableCollection<SlotData> _slots = [];

    public int BoxCount => _sav.BoxCount;
    public int SlotsPerBox => _sav.BoxSlotCount;

    public BoxViewerViewModel(SaveFile sav, ISpriteRenderer spriteRenderer)
    {
        _sav = sav;
        _spriteRenderer = spriteRenderer;

        LoadBox(0);
    }

    partial void OnSelectedIndexChanged(int value)
    {
        // Update IsSelected on all slots
        for (int i = 0; i < Slots.Count; i++)
            Slots[i].IsSelected = i == value;
    }

    private void LoadBox(int box)
    {
        if (box < 0 || box >= BoxCount)
            return;

        var previousIndex = SelectedIndex;
        CurrentBox = box;
        BoxName = _sav.GetBoxName(box);

        Slots.Clear();

        var boxData = _sav.GetBoxData(box);
        for (int slot = 0; slot < boxData.Length; slot++)
        {
            var pk = boxData[slot];
            Slots.Add(new SlotData
            {
                Slot = slot,
                Box = box,
                Species = pk.Species,
                Sprite = _spriteRenderer.GetSprite(pk),
                IsEmpty = pk.Species == 0,
                IsShiny = pk.IsShiny,
                Nickname = pk.Nickname,
                IsSelected = false
            });
        }

        // Restore selection position (clamped to valid range)
        SelectedIndex = Math.Clamp(previousIndex, 0, Slots.Count - 1);
    }

    [RelayCommand]
    private void PreviousBox()
    {
        var newBox = CurrentBox - 1;
        if (newBox < 0)
            newBox = BoxCount - 1;
        LoadBox(newBox);
    }

    [RelayCommand]
    private void NextBox()
    {
        var newBox = CurrentBox + 1;
        if (newBox >= BoxCount)
            newBox = 0;
        LoadBox(newBox);
    }

    [RelayCommand]
    private void SelectSlotByClick(SlotData? slot)
    {
        if (slot is null)
            return;

        SelectedIndex = slot.Slot;
    }

    [RelayCommand]
    private void MoveSelection(string direction)
    {
        if (Slots.Count == 0) return;

        int newIndex = direction switch
        {
            "Left" => SelectedIndex > 0 ? SelectedIndex - 1 : SelectedIndex,
            "Right" => SelectedIndex < Slots.Count - 1 ? SelectedIndex + 1 : SelectedIndex,
            "Up" => SelectedIndex >= Columns ? SelectedIndex - Columns : SelectedIndex,
            "Down" => SelectedIndex + Columns < Slots.Count ? SelectedIndex + Columns : SelectedIndex,
            _ => SelectedIndex
        };

        SelectedIndex = newIndex;
    }

    [RelayCommand]
    private void SelectFirstSlot()
    {
        if (Slots.Count > 0)
            SelectedIndex = 0;
    }

    [RelayCommand]
    private void SelectLastSlot()
    {
        if (Slots.Count > 0)
            SelectedIndex = Slots.Count - 1;
    }

    [RelayCommand]
    private void ActivateSlot()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Slots.Count)
            return;

        var slot = Slots[SelectedIndex];
        // TODO: Future - open PKM editor
        System.Diagnostics.Debug.WriteLine($"Activated slot {slot.Slot} in box {slot.Box} (Species: {slot.Species})");
    }
}
```

---

### Phase 5: Models (Step 15)

#### Step 15: SlotData.cs
```csharp
// PKHeX.Avalonia/Models/SlotData.cs
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PKHeX.Avalonia.Models;

public partial class SlotData : ObservableObject
{
    [ObservableProperty] private int _slot;
    [ObservableProperty] private int _box;
    [ObservableProperty] private ushort _species;
    [ObservableProperty] private Bitmap? _sprite;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _isShiny;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _nickname = string.Empty;
}
```

---

### Phase 6: Views (Steps 16-18)

#### Step 16: MainWindow.axaml
```xml
<!-- PKHeX.Avalonia/Views/MainWindow.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:PKHeX.Avalonia.ViewModels"
        xmlns:views="using:PKHeX.Avalonia.Views"
        x:Class="PKHeX.Avalonia.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="{Binding WindowTitle}"
        Width="800" Height="600"
        MinWidth="600" MinHeight="400">

    <Window.KeyBindings>
        <KeyBinding Gesture="Ctrl+O" Command="{Binding OpenFileCommand}" />
        <KeyBinding Gesture="Ctrl+S" Command="{Binding SaveFileCommand}" />
        <KeyBinding Gesture="Ctrl+Shift+S" Command="{Binding SaveFileAsCommand}" />
    </Window.KeyBindings>

    <DockPanel>
        <!-- Menu Bar -->
        <Menu DockPanel.Dock="Top">
            <MenuItem Header="_File">
                <MenuItem Header="_Open..." Command="{Binding OpenFileCommand}" InputGesture="Ctrl+O" />
                <MenuItem Header="_Save" Command="{Binding SaveFileCommand}" InputGesture="Ctrl+S" />
                <MenuItem Header="Save _As..." Command="{Binding SaveFileAsCommand}" InputGesture="Ctrl+Shift+S" />
                <Separator />
                <MenuItem Header="_Close" Command="{Binding CloseFileCommand}" />
            </MenuItem>
        </Menu>

        <!-- Status Bar -->
        <Border DockPanel.Dock="Bottom" Background="{DynamicResource SystemControlBackgroundChromeMediumLowBrush}" Padding="8,4">
            <TextBlock Text="{Binding CurrentSave.Version, StringFormat='Game: {0}', FallbackValue='No save loaded'}" />
        </Border>

        <!-- Main Content -->
        <Grid>
            <!-- No Save Loaded State -->
            <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center"
                        IsVisible="{Binding !HasSave}">
                <TextBlock Text="Welcome to PKHeX Avalonia" FontSize="24" FontWeight="Light" HorizontalAlignment="Center" />
                <TextBlock Text="Open a save file to get started" Opacity="0.6" HorizontalAlignment="Center" Margin="0,8,0,16" />
                <Button Content="Open Save File" Command="{Binding OpenFileCommand}" HorizontalAlignment="Center" />
            </StackPanel>

            <!-- Save Loaded State -->
            <views:BoxViewer DataContext="{Binding BoxViewer}" IsVisible="{Binding HasSave}" />
        </Grid>
    </DockPanel>
</Window>
```

#### Step 17: MainWindow.axaml.cs
```csharp
// PKHeX.Avalonia/Views/MainWindow.axaml.cs
using Avalonia.Controls;

namespace PKHeX.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

#### Step 18: BoxViewer.axaml
```xml
<!-- PKHeX.Avalonia/Views/BoxViewer.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:PKHeX.Avalonia.ViewModels"
             xmlns:models="using:PKHeX.Avalonia.Models"
             x:Class="PKHeX.Avalonia.Views.BoxViewer"
             x:DataType="vm:BoxViewerViewModel"
             Focusable="True">

    <!-- Keyboard Navigation -->
    <UserControl.KeyBindings>
        <KeyBinding Gesture="Left" Command="{Binding MoveSelectionCommand}" CommandParameter="Left" />
        <KeyBinding Gesture="Right" Command="{Binding MoveSelectionCommand}" CommandParameter="Right" />
        <KeyBinding Gesture="Up" Command="{Binding MoveSelectionCommand}" CommandParameter="Up" />
        <KeyBinding Gesture="Down" Command="{Binding MoveSelectionCommand}" CommandParameter="Down" />
        <KeyBinding Gesture="Enter" Command="{Binding ActivateSlotCommand}" />
        <KeyBinding Gesture="Space" Command="{Binding ActivateSlotCommand}" />
        <KeyBinding Gesture="Home" Command="{Binding SelectFirstSlotCommand}" />
        <KeyBinding Gesture="End" Command="{Binding SelectLastSlotCommand}" />
        <KeyBinding Gesture="PageUp" Command="{Binding PreviousBoxCommand}" />
        <KeyBinding Gesture="PageDown" Command="{Binding NextBoxCommand}" />
    </UserControl.KeyBindings>

    <Border Padding="16">
        <StackPanel Spacing="8">
            <!-- Box Navigation -->
            <DockPanel>
                <Button DockPanel.Dock="Left" Content="◀" Command="{Binding PreviousBoxCommand}" Width="40" ToolTip.Tip="Previous Box (PageUp)" />
                <Button DockPanel.Dock="Right" Content="▶" Command="{Binding NextBoxCommand}" Width="40" ToolTip.Tip="Next Box (PageDown)" />
                <TextBlock Text="{Binding BoxName}" HorizontalAlignment="Center" VerticalAlignment="Center" FontWeight="SemiBold" FontSize="16" />
            </DockPanel>

            <TextBlock Text="{Binding CurrentBox, StringFormat='Box {0}'}" Opacity="0.6" HorizontalAlignment="Center" />

            <!-- Box Grid (6 columns x 5 rows = 30 slots) -->
            <ItemsControl ItemsSource="{Binding Slots}" x:Name="SlotGrid">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <UniformGrid Columns="6" />
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate x:DataType="models:SlotData">
                        <Button Command="{Binding $parent[ItemsControl].((vm:BoxViewerViewModel)DataContext).SelectSlotByClickCommand}"
                                CommandParameter="{Binding}"
                                Background="Transparent"
                                Padding="2"
                                Margin="1"
                                BorderThickness="2"
                                Classes.selected="{Binding IsSelected}">
                            <Button.Styles>
                                <Style Selector="Button">
                                    <Setter Property="BorderBrush" Value="{DynamicResource SystemControlForegroundBaseMediumLowBrush}" />
                                </Style>
                                <Style Selector="Button.selected">
                                    <Setter Property="BorderBrush" Value="{DynamicResource SystemAccentColor}" />
                                    <Setter Property="Background" Value="{DynamicResource SystemAccentColorLight3}" />
                                </Style>
                            </Button.Styles>
                            <Panel Width="68" Height="56">
                                <!-- Empty slot background -->
                                <Border Background="{DynamicResource SystemControlBackgroundChromeMediumBrush}"
                                        CornerRadius="4"
                                        IsVisible="{Binding IsEmpty}" />

                                <!-- Sprite -->
                                <Image Source="{Binding Sprite}"
                                       Width="68" Height="56"
                                       IsVisible="{Binding !IsEmpty}" />

                                <!-- Shiny indicator -->
                                <Ellipse Width="8" Height="8"
                                         Fill="Gold"
                                         HorizontalAlignment="Left"
                                         VerticalAlignment="Top"
                                         Margin="2"
                                         IsVisible="{Binding IsShiny}" />
                            </Panel>
                        </Button>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>

            <!-- Keyboard hints -->
            <TextBlock Text="Arrow keys: Navigate | Enter: Select | PageUp/Down: Switch Box"
                       Opacity="0.5" FontSize="11" HorizontalAlignment="Center" Margin="0,8,0,0" />
        </StackPanel>
    </Border>
</UserControl>
```

**BoxViewer.axaml.cs:**
```csharp
// PKHeX.Avalonia/Views/BoxViewer.axaml.cs
using Avalonia.Controls;
using Avalonia.Input;

namespace PKHeX.Avalonia.Views;

public partial class BoxViewer : UserControl
{
    public BoxViewer()
    {
        InitializeComponent();

        // Focus the control when it becomes visible for keyboard navigation
        AttachedToVisualTree += (_, _) => Focus();
    }
}
```

---

### Phase 7: Build & Run (Steps 19-20)

#### Step 19: Build the Project
```bash
cd PKHeX.Avalonia
dotnet restore
dotnet build
```

#### Step 20: Run and Test
```bash
dotnet run
```

**Verification Checklist:**
- [ ] Main window opens with Fluent theme
- [ ] File > Open... shows file picker (Ctrl+O)
- [ ] Loading a valid save file displays Box Viewer
- [ ] Box navigation (◀/▶ buttons) works
- [ ] Box navigation (PageUp/PageDown) works
- [ ] 30 slots display in 6x5 grid
- [ ] Empty slots show placeholder background
- [ ] Filled slots show placeholder sprites with species number
- [ ] Arrow keys navigate between slots
- [ ] Selected slot has highlighted border
- [ ] Home/End jump to first/last slot
- [ ] Enter/Space activates selected slot (logs to debug)
- [ ] File > Save works (Ctrl+S, overwrites original)
- [ ] File > Save As... prompts for new location (Ctrl+Shift+S)

---

## 4. Critical Files to Reference (PKHeX.Core)

These files from the existing codebase are essential for understanding the Core API:

| File | Purpose |
|------|---------|
| [ISaveFileProvider.cs](PKHeX.Core/Editing/ISaveFileProvider.cs) | Interface your SaveFileService wraps |
| [ISpriteBuilder.cs](PKHeX.Core/Editing/ISpriteBuilder.cs) | Interface for sprite generation (reference for your renderer) |
| [ISlotViewer.cs](PKHeX.Core/Editing/Saves/Slots/ISlotViewer.cs) | Pattern for slot notification (future use) |
| [SaveUtil.cs](PKHeX.Core/Saves/Util/SaveUtil.cs) | `TryGetSaveFile()` - save detection |
| [SaveFile.cs](PKHeX.Core/Saves/SaveFile.cs) | Base class - `GetBoxData()`, `Write()` |
| [SlotUtil.cs](PKHeX.WinForms/Controls/Slots/SlotUtil.cs) | WinForms rendering reference |
| [SpriteBuilder.cs](PKHeX.Drawing.PokeSprite/Builder/SpriteBuilder.cs) | GDI+ sprite logic to port |

---

## 5. Future Work (Out of Scope for Vertical Slice)

The following are explicitly **NOT** part of this plan (YAGNI):

- PKM Editor (individual Pokemon editing)
- Party Editor
- Legality Checking UI
- Bag/Inventory editing
- Trainer Info editing
- Mystery Gift support
- Drag-and-drop between slots
- Plugin system
- Settings/Preferences UI
- Real sprite assets (placeholder only for now)
- Multi-language support

---

## 6. Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| .NET 10 preview incompatibility | Fall back to .NET 9 if needed; Avalonia 11.2 supports both |
| Sprite rendering performance | SkiaSharp is GPU-accelerated; profile if issues arise |
| PKHeX.Core internal changes | Core is stable; wrap all Core calls in services |
| File dialog platform differences | Avalonia's StorageProvider abstracts this |

---

## Summary

This plan creates a **minimal viable cross-platform PKHeX** with:
- Clean MVVM architecture using CommunityToolkit.Mvvm
- Proper DI via Microsoft.Extensions.DependencyInjection
- Abstracted rendering (ISpriteRenderer) ready for real sprites
- Familiar layout with modern Fluent styling
- Zero modifications to PKHeX.Core

**Total estimated files to create:** 15
**Key dependencies:** Avalonia 11.2.3, CommunityToolkit.Mvvm 8.4.0, SkiaSharp 3.x
