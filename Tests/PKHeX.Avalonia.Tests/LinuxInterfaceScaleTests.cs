using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Issue #534: the Flatpak (X11 on GNOME) showed a very small interface on a 2560x1440 screen.
/// Settings offers an interface scale that the host exports as AVALONIA_GLOBAL_SCALE_FACTOR before
/// the X11 backend starts. A value the user exported already wins.
/// </summary>
public sealed class LinuxInterfaceScaleTests
{
    [Theory]
    [InlineData(100, "1")]
    [InlineData(125, "1.25")]
    [InlineData(150, "1.5")]
    [InlineData(175, "1.75")]
    [InlineData(200, "2")]
    [InlineData(300, "3")]
    public void Linux_ExportsTheSavedScaleWithAnInvariantDecimalPoint(int percent, string expected)
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            // A comma-decimal culture must not leak into the value Avalonia parses.
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal(expected, LinuxInterfaceScalePolicy.Resolve(isLinux: true, () => percent, environmentValue: null));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(0)]     // Automatic: follow the desktop.
    [InlineData(-25)]   // Hand-edited nonsense is ignored.
    [InlineData(1000)]
    public void Linux_LeavesTheEnvironmentAloneForAutomaticOrInvalidValues(int percent)
    {
        Assert.Null(LinuxInterfaceScalePolicy.Resolve(isLinux: true, () => percent, environmentValue: null));
    }

    [Fact]
    public void Linux_AnExportedValueWinsAndTheSettingIsNotRead()
    {
        var read = false;
        Assert.Null(LinuxInterfaceScalePolicy.Resolve(isLinux: true, () => { read = true; return 200; }, "1.25"));
        Assert.False(read);
    }

    [Fact]
    public void OtherPlatforms_NeverExportAScaleOrReadTheSetting()
    {
        var read = false;
        Assert.Null(LinuxInterfaceScalePolicy.Resolve(isLinux: false, () => { read = true; return 200; }, null));
        Assert.False(read);
    }

    [Fact]
    public void Setting_DefaultsToAutomatic()
    {
        Assert.Equal(0, new AppSettings().Linux.InterfaceScalePercent);
    }

    [Fact]
    public void Save_PersistsTheChosenScaleForTheNextStart()
    {
        var store = new FakeSettingsStore();
        var vm = Create(new AppSettings(), store, isLinux: true);
        Assert.Equal(0, vm.InterfaceScalePercent);
        Assert.Contains(vm.InterfaceScales, o => o.Value == 150);

        vm.InterfaceScalePercent = 150;
        vm.SaveCommand.Execute(null);

        Assert.Equal(150, store.Saved!.Linux.InterfaceScalePercent);
        Assert.Equal(150, Create(store.Saved, new FakeSettingsStore(), isLinux: true).InterfaceScalePercent);
    }

    [Fact]
    public void Save_KeepsAHandEditedValueThatTheUserDidNotChange()
    {
        var settings = new AppSettings();
        settings.Linux.InterfaceScalePercent = 140;
        var store = new FakeSettingsStore();
        var vm = Create(settings, store, isLinux: true);
        Assert.Equal(0, vm.InterfaceScalePercent);

        vm.SaveCommand.Execute(null);

        Assert.Equal(140, store.Saved!.Linux.InterfaceScalePercent);
    }

    [AvaloniaFact]
    public void Section_OffersTheScaleChoiceOnLinux()
    {
        var vm = Create(new AppSettings(), new FakeSettingsStore(), isLinux: true);
        var view = new SettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 480, Height = 760 };
        using var lifetime = new HeadlessWindowLifetime(window);
        window.Show();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var combo = view.FindControl<ComboBox>("InterfaceScaleComboBox")!;
        Assert.Equal(LocalizedStrings.Instance["Settings_InterfaceScale"], AutomationProperties.GetName(combo));
        Assert.True(combo.GetVisualAncestors().OfType<global::Avalonia.Visual>().All(a => a.IsVisible));
        Assert.Equal(0, combo.SelectedValue);

        combo.SelectedValue = 200;
        Assert.Equal(200, vm.InterfaceScalePercent);
    }

    private static SettingsViewModel Create(AppSettings settings, FakeSettingsStore store, bool isLinux) => new(
        settings,
        store,
        new ThemeService(settings, new FakeSettingsStore()),
        new UiDensityService(settings, new FakeSettingsStore()),
        new LanguageService(),
        UpdateTestDoubles.Coordinator(),
        isLinux: isLinux);
}
