using PKHeX.Application.Abstractions;
using PKHeX.Infrastructure.Desktop;
using System.Diagnostics;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// The user-local desktop integration must install the AppImage under the canonical stable name,
/// install icon + desktop entry into the XDG user directories, and clean all three up again.
/// File layout is exercised for real against temp directories; the environment (APPIMAGE/APPDIR,
/// Linux check, refresh helpers) is injected so the tests run identically on every CI platform.
/// </summary>
public class LinuxDesktopIntegrationServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pkhex-desktop-integration-{Guid.NewGuid():N}");
    private readonly List<(string File, IReadOnlyList<string> Args)> _commands = [];

    private string DataHome => Path.Combine(_root, "share");
    private string OptDir => Path.Combine(_root, "opt", "PKHeX-Avalonia");
    private string SourceAppImage => Path.Combine(_root, "Downloads", "PKHeX-Avalonia-1.87.3-x86_64.AppImage");
    private string AppDir => Path.Combine(_root, "AppDir");
    private string IconSource => Path.Combine(AppDir, "usr", "share", "icons", "hicolor", "64x64", "apps", "io.pkhex.avalonia.png");

    private string InstalledApp => Path.Combine(OptDir, "PKHeX-Avalonia.AppImage");
    private string DesktopEntry => Path.Combine(DataHome, "applications", "io.pkhex.avalonia.desktop");
    private string InstalledIcon => Path.Combine(DataHome, "icons", "hicolor", "64x64", "apps", "io.pkhex.avalonia.png");

    public LinuxDesktopIntegrationServiceTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SourceAppImage)!);
        File.WriteAllBytes(SourceAppImage, [0x7f, 0x45, 0x4c, 0x46, 1, 2, 3, 4, 5, 6, 7, 8]);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    private LinuxDesktopIntegrationService CreateService(
        string? source = null, string? appDir = null, bool isLinux = true) =>
        new(source ?? SourceAppImage, appDir ?? AppDir, DataHome, OptDir, isLinux,
            (file, args) => { _commands.Add((file, args)); return 0; });

    [Fact]
    public async Task Register_InstallsStableCopyIconAndDesktopEntry()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(IconSource)!);
        await File.WriteAllBytesAsync(IconSource, [1, 2, 3, 4], TestContext.Current.CancellationToken);

        var service = CreateService();
        Assert.True(service.IsSupported);

        var result = await service.RegisterAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DesktopIntegrationOutcome.Success, result.Outcome);
        Assert.Equal(InstalledApp, result.InstalledPath);
        Assert.True(File.Exists(InstalledApp));
        Assert.True(File.Exists(InstalledIcon));
        Assert.True(File.Exists(DesktopEntry));
        Assert.True(service.IsRegistered);
        Assert.Equal(InstalledApp, service.InstalledAppImagePath);

        // The installed copy is byte-identical to the running AppImage.
        Assert.Equal(await File.ReadAllBytesAsync(SourceAppImage, TestContext.Current.CancellationToken), await File.ReadAllBytesAsync(InstalledApp, TestContext.Current.CancellationToken));

        var entry = await File.ReadAllTextAsync(DesktopEntry, TestContext.Current.CancellationToken);
        Assert.Contains("Type=Application", entry);
        Assert.Contains("Name=PKHeX-Avalonia", entry);
        Assert.Contains($"Exec=/usr/bin/env -- {LinuxDesktopEntry.QuoteExec(InstalledApp)}", entry);
        Assert.Contains($"TryExec={LinuxDesktopEntry.EscapeValue(InstalledApp)}", entry);
        Assert.Contains("Icon=io.pkhex.avalonia", entry);
        Assert.Contains("Terminal=false", entry);

        // Desktop databases are refreshed best-effort.
        Assert.Contains(_commands, c => c.File == "update-desktop-database");
    }

    [Fact]
    public async Task Register_WithoutBundledIcon_SucceedsWithoutIconKey()
    {
        var service = CreateService();

        var result = await service.RegisterAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DesktopIntegrationOutcome.Success, result.Outcome);
        Assert.False(File.Exists(InstalledIcon));
        var entry = await File.ReadAllTextAsync(DesktopEntry, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("Icon=", entry);
    }

    [Fact]
    public async Task Register_OverwritesStaleInstalledCopy()
    {
        Directory.CreateDirectory(OptDir);
        await File.WriteAllTextAsync(InstalledApp, "an older installed version", TestContext.Current.CancellationToken);

        var service = CreateService();
        var result = await service.RegisterAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DesktopIntegrationOutcome.Success, result.Outcome);
        Assert.Equal(await File.ReadAllBytesAsync(SourceAppImage, TestContext.Current.CancellationToken), await File.ReadAllBytesAsync(InstalledApp, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Register_LeavesNoStagingFileBehind()
    {
        var service = CreateService();
        await service.RegisterAsync(TestContext.Current.CancellationToken);

        Assert.Empty(Directory.GetFiles(OptDir, "*.tmp"));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(DesktopEntry)!, "*.tmp"));
    }

    [Fact]
    public async Task Register_NotLinux_ReturnsNotSupported()
    {
        var service = CreateService(isLinux: false);

        var result = await service.RegisterAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DesktopIntegrationOutcome.NotSupported, result.Outcome);
        Assert.False(service.IsSupported);
        Assert.False(File.Exists(InstalledApp));
    }

    [Fact]
    public async Task Register_SourceAppImageMissing_ReturnsNotSupported()
    {
        var service = CreateService(source: Path.Combine(_root, "does-not-exist.AppImage"));

        Assert.False(service.IsSupported);
        var result = await service.RegisterAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DesktopIntegrationOutcome.NotSupported, result.Outcome);
    }

    [Fact]
    public async Task Unregister_RemovesOnlyEntryAndIcon()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(IconSource)!);
        await File.WriteAllBytesAsync(IconSource, [1, 2, 3, 4], TestContext.Current.CancellationToken);
        var service = CreateService();
        await service.RegisterAsync(TestContext.Current.CancellationToken);

        var result = await service.UnregisterAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DesktopIntegrationOutcome.Success, result.Outcome);
        Assert.False(File.Exists(DesktopEntry));
        Assert.True(File.Exists(InstalledApp));
        Assert.False(File.Exists(InstalledIcon));
        Assert.True(Directory.Exists(OptDir));
        Assert.False(service.IsRegistered);
        Assert.Equal(InstalledApp, service.InstalledAppImagePath);

        // The user's downloaded source AppImage is never touched.
        Assert.True(File.Exists(SourceAppImage));
    }

    [Fact]
    public async Task Unregister_WhenNothingInstalled_Succeeds()
    {
        var service = CreateService();

        var result = await service.UnregisterAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DesktopIntegrationOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task InstalledSource_CanRemoveAndReAddWithoutLosingTheImage()
    {
        await CreateService().RegisterAsync(TestContext.Current.CancellationToken);
        File.Delete(SourceAppImage);
        var service = CreateService(source: InstalledApp);
        var before = await File.ReadAllBytesAsync(InstalledApp, TestContext.Current.CancellationToken);
        Assert.Equal(DesktopIntegrationOutcome.Success, (await service.UnregisterAsync(TestContext.Current.CancellationToken)).Outcome);
        Assert.True(service.IsSupported);
        Assert.Equal(before, await File.ReadAllBytesAsync(InstalledApp, TestContext.Current.CancellationToken));
        Assert.Equal(DesktopIntegrationOutcome.Success, (await service.RegisterAsync(TestContext.Current.CancellationToken)).Outcome);
        Assert.True(service.IsRegistered);
        Assert.Equal(before, await File.ReadAllBytesAsync(InstalledApp, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Unregister_ReportsRequiredDeletionFailure()
    {
        await CreateService().RegisterAsync(TestContext.Current.CancellationToken);
        File.Delete(DesktopEntry);
        Directory.CreateDirectory(DesktopEntry);
        Assert.Equal(DesktopIntegrationOutcome.Failed, (await CreateService().UnregisterAsync(TestContext.Current.CancellationToken)).Outcome);
        Assert.True(File.Exists(InstalledApp));
    }

    [Fact]
    public async Task Flatpak_RejectsEvenAnExistingInheritedAppImage()
    {
        var service = new LinuxDesktopIntegrationService(SourceAppImage, AppDir, DataHome, OptDir, true, null, isFlatpak: true);
        Assert.False(service.IsSupported);
        Assert.Equal(DesktopIntegrationOutcome.NotSupported, (await service.RegisterAsync(TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(DesktopIntegrationOutcome.NotSupported, (await service.UnregisterAsync(TestContext.Current.CancellationToken)).Outcome);
        Assert.False(Directory.Exists(OptDir));
    }

    [Theory]
    [InlineData("/home/a%b/app", "\"/home/a%%b/app\"")]
    [InlineData("/home/a$b/app", "\"/home/a\\\\$b/app\"")]
    [InlineData("/home/a`b/app", "\"/home/a\\\\`b/app\"")]
    [InlineData("/home/a\\b/app", "\"/home/a\\\\\\\\b/app\"")]
    [InlineData("/home/a\"b/app", "\"/home/a\\\\\"b/app\"")]
    [InlineData("/home/a b/app", "\"/home/a b/app\"")]
    public void ExecAppliesBothEscapingLayers(string path, string expected) =>
        Assert.Equal(expected, LinuxDesktopEntry.QuoteExec(path));

    [LinuxTheory]
    [InlineData("space name")]
    [InlineData("percent%name")]
    [InlineData("dollar$name")]
    [InlineData("back\\slash")]
    [InlineData("quote\"name")]
    [InlineData("tick`name")]
    [InlineData("apostrophe'name")]
    public async Task DesktopEntryLaunchesTheExactInstalledPath(string name)
    {
        File.WriteAllText(SourceAppImage, "#!/bin/sh\nprintf started > \"$TEST_LAUNCH\"\n");
        var opt = Path.Combine(_root, name);
        var service = new LinuxDesktopIntegrationService(SourceAppImage, AppDir, DataHome, opt, true, (_, _) => 0);
        Assert.Equal(DesktopIntegrationOutcome.Success, (await service.RegisterAsync()).Outcome);
        var marker = Path.Combine(_root, "launched");
        var psi = new ProcessStartInfo("/usr/bin/python3") { RedirectStandardError = true, RedirectStandardOutput = true };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("import sys; from gi.repository import Gio; assert Gio.DesktopAppInfo.new_from_filename(sys.argv[1]).launch([], None)");
        psi.ArgumentList.Add(DesktopEntry);
        psi.Environment["TEST_LAUNCH"] = marker;
        using var process = Process.Start(psi)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await process.WaitForExitAsync(timeout.Token);
        Assert.True(process.ExitCode == 0, await process.StandardError.ReadToEndAsync(timeout.Token));
        for (var i = 0; i < 100 && (!File.Exists(marker) || new FileInfo(marker).Length == 0); i++)
            await Task.Delay(20, timeout.Token);
        Assert.Equal("started", File.ReadAllText(marker));
    }
}
