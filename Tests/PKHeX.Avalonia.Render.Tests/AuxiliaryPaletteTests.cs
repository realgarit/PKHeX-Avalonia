using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

public class AuxiliaryPaletteTests
{
    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task AuxiliaryWindowsAndDisclosuresShareTheAppPalette(bool dark)
    {
        var previous = global::Avalonia.Application.Current!.RequestedThemeVariant;
        var settings = new AppSettings(); settings.Theme.Selected = dark ? AppTheme.Dark : AppTheme.Light;
        var store = Mock.Of<ISettingsStore>(); var theme = new Mock<IThemeService>(); theme.SetupGet(t => t.CurrentTheme).Returns(settings.Theme.Selected);
        var updates = new UpdateCheckCoordinator(Mock.Of<IUpdateCheckService>(), Mock.Of<IWindowService>(), Mock.Of<IUpdateInstaller>(), Mock.Of<IAppLifetime>(), settings, store);
        var save = new SAV9ZA();
        using var donuts = new DonutEditorViewModel(save);
        using var events = new ZaEventEditorViewModel(save, Mock.Of<IDialogService>());
        var views = new (string Name, Control View)[]
        {
            ("donut", new DonutEditor { DataContext = donuts }),
            ("events", new ZaEventEditor { DataContext = events }),
            ("encounter", new EncounterDatabaseView { DataContext = new EncounterDatabaseViewModel(save, Mock.Of<ISpriteRenderer>(), Mock.Of<IDialogService>(), _ => { }) }),
            ("settings", new SettingsView { DataContext = new SettingsViewModel(settings, store, theme.Object, Mock.Of<IUiDensityService>(), new LanguageService(), updates) }),
            ("legality", new LegalityView { DataContext = new LegalityViewModel("Legal!") }),
            ("database", new PKMDatabaseView { DataContext = new PKMDatabaseViewModel(save, Mock.Of<ISpriteRenderer>(), Mock.Of<IDialogService>()) }),
        };
        try
        {
            global::Avalonia.Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            foreach (var (name, view) in views)
            {
                var window = new Window { Content = view, Width = name == "settings" ? 390 : 900, Height = 650 }; window.Show();
                try
                {
                    foreach (var expander in view.GetVisualDescendants().OfType<Expander>()) expander.IsExpanded = true;
                    await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                    var baseBrush = Assert.IsAssignableFrom<ISolidColorBrush>(window.FindResource(window.ActualThemeVariant, "ThemeBackgroundBaseBrush"));
                    var cardBrush = Assert.IsAssignableFrom<ISolidColorBrush>(window.FindResource(window.ActualThemeVariant, "ThemeBackgroundCardBrush"));
                    Assert.Equal(baseBrush.Color, Assert.IsAssignableFrom<ISolidColorBrush>(window.Background).Color);
                    foreach (var expander in view.GetVisualDescendants().OfType<Expander>())
                    {
                        Assert.Equal(cardBrush.Color, Assert.IsAssignableFrom<ISolidColorBrush>(expander.Background).Color);
                        var header = expander.GetVisualDescendants().OfType<ToggleButton>().Single(t => t.Name == "ExpanderHeader");
                        Assert.Equal(cardBrush.Color, Assert.IsAssignableFrom<ISolidColorBrush>(header.Background).Color);
                    }
                    if (view is SettingsView settingsView) Assert.InRange(settingsView.FindControl<Border>("SettingsHeader")!.Bounds.Height, 30, 52);
                    using var frame = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height)); frame.Render(window);
                    if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1" && Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
                    { Directory.CreateDirectory(path); frame.Save(Path.Combine(path, $"palette-{name}-{dark}.png"), PngBitmapEncoderOptions.Default); }
                }
                finally { window.Close(); }
            }
        }
        finally { global::Avalonia.Application.Current.RequestedThemeVariant = previous; }
    }
}
