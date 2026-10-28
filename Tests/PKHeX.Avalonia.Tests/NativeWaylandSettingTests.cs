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
/// Native Wayland is opt-in: X11 stays the default on Linux, the Settings screen offers the switch
/// only on Linux, and an environment variable overrides the saved choice in both directions.
/// </summary>
public sealed class NativeWaylandSettingTests
{
    [Theory]
    [InlineData(false, null, false)]
    [InlineData(true, null, true)]
    [InlineData(false, "", false)]
    [InlineData(true, "yes", true)]
    [InlineData(false, "1", true)]
    [InlineData(false, " 1 ", true)]
    [InlineData(true, "0", false)]
    public void Linux_UsesTheOverrideOrElseTheSetting(bool setting, string? environment, bool expected)
    {
        Assert.Equal(expected, NativeWaylandPolicy.ShouldUseWayland(isLinux: true, () => setting, environment));
    }

    [Theory]
    [InlineData(null)] [InlineData("1")] [InlineData("0")]
    public void OtherPlatforms_NeverUseWaylandOrReadTheSetting(string? environment)
    {
        var read = false;
        Assert.False(NativeWaylandPolicy.ShouldUseWayland(isLinux: false, () => read = true, environment));
        Assert.False(read);
    }

    [Theory]
    [InlineData("1")] [InlineData("0")]
    public void Override_DoesNotReadTheSetting(string environment)
    {
        var read = false;
        NativeWaylandPolicy.ShouldUseWayland(isLinux: true, () => { read = true; return true; }, environment);
        Assert.False(read);
    }

    [Fact]
    public void Setting_DefaultsToX11()
    {
        Assert.False(new AppSettings().Linux.UseNativeWayland);
    }

    [Fact]
    public void Save_PersistsTheChoiceForTheNextStart()
    {
        var store = new FakeSettingsStore();
        var vm = Create(new AppSettings(), store, isLinux: true);
        Assert.False(vm.UseNativeWayland);

        vm.UseNativeWayland = true;
        vm.SaveCommand.Execute(null);

        Assert.True(store.Saved!.Linux.UseNativeWayland);
        Assert.True(Create(store.Saved, new FakeSettingsStore(), isLinux: true).UseNativeWayland);
    }

    [AvaloniaTheory]
    [InlineData(true)] [InlineData(false)]
    public void Section_IsShownOnlyOnLinux(bool isLinux)
    {
        var vm = Create(new AppSettings(), new FakeSettingsStore(), isLinux);
        var view = new SettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 480, Height = 760 };
        using var lifetime = new HeadlessWindowLifetime(window);
        window.Show();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var section = view.FindControl<Border>("LinuxDisplaySection")!;
        var toggle = view.FindControl<ToggleSwitch>("UseNativeWaylandSwitch")!;
        Assert.Equal(isLinux, section.IsVisible);
        Assert.Equal(LocalizedStrings.Instance["Settings_UseNativeWayland"], AutomationProperties.GetName(toggle));
        if (!isLinux)
            return;

        Assert.True(toggle.GetVisualAncestors().OfType<global::Avalonia.Visual>().All(a => a.IsVisible));
        Assert.False(toggle.IsChecked);
        toggle.IsChecked = true;
        Assert.True(vm.UseNativeWayland);
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
