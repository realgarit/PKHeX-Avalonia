using System.Diagnostics;
using System.Runtime.CompilerServices;
using PKHeX.Infrastructure.Updating;

namespace PKHeX.Avalonia.Tests;

public class LinuxUpdateStrategyTests
{
    [Theory]
    [InlineData("/home/user/Downloads/old.AppImage", "/home/user/Downloads/PKHeX-Avalonia.AppImage")]
    [InlineData("/home/user/Downloads/PKHeX-Avalonia.AppImage", "/home/user/Downloads/PKHeX-Avalonia.AppImage")]
    [InlineData("/old.AppImage", "/PKHeX-Avalonia.AppImage")]
    public void LinuxPathsAreIndependentOfTheTestHost(string current, string target)
    {
        var script = LinuxUpdateStrategy.BuildAppImageSwapScript("/tmp/new.AppImage", current, "/tmp/update.log");
        Assert.Contains($"CURRENT='{current}'", script);
        Assert.Contains($"TARGET='{target}'", script);
    }

    [LinuxTheory]
    [InlineData("versioned")]
    [InlineData("canonical")]
    [InlineData("missing-download")]
    [InlineData("existing-file")]
    [InlineData("existing-directory")]
    [InlineData("existing-symlink")]
    [InlineData("failed-copy")]
    [InlineData("failed-chmod")]
    [InlineData("failed-rename")]
    [InlineData("special-path")]
    public async Task ExecutedScriptPreservesTheOriginalOnFailure(string scenario)
    {
        var root = Path.Combine(Path.GetTempPath(), "pkhex-swap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var directory = Path.Combine(root, scenario == "special-path" ? "space ' $HOME `uname` \\ %path" : "install");
            Directory.CreateDirectory(directory);
            var canonical = scenario is "canonical" or "failed-rename";
            var current = Path.Combine(directory, canonical ? "PKHeX-Avalonia.AppImage" : "old.AppImage");
            var target = Path.Combine(directory, "PKHeX-Avalonia.AppImage");
            var download = Path.Combine(root, "download.AppImage");
            var oldBytes = "#!/bin/sh\nprintf old > \"$TEST_LAUNCH\"\n";
            var newBytes = "#!/bin/sh\nprintf new > \"$TEST_LAUNCH\"\n";
            WriteExecutable(current, oldBytes);
            if (scenario != "missing-download") WriteExecutable(download, newBytes);
            if (scenario == "existing-file") File.WriteAllText(target, "unrelated file");
            if (scenario == "existing-directory") Directory.CreateDirectory(target);
            if (scenario == "existing-symlink") File.CreateSymbolicLink(target, Path.Combine(root, "missing"));
            var script = Path.Combine(root, "swap.sh");
            File.WriteAllText(script, string.Join('\n', LinuxUpdateStrategy.BuildAppImageSwapScript(download, current, Path.Combine(root, "update.log"))));
            var psi = new ProcessStartInfo("/bin/sh") { RedirectStandardOutput = true, RedirectStandardError = true };
            psi.ArgumentList.Add(script); psi.ArgumentList.Add("99999999");
            var launched = Path.Combine(root, "launched"); psi.Environment["TEST_LAUNCH"] = launched;
            if (scenario.StartsWith("failed-", StringComparison.Ordinal))
            {
                var bin = Path.Combine(root, "bin"); Directory.CreateDirectory(bin);
                var command = scenario switch { "failed-copy" => "cp", "failed-chmod" => "chmod", _ => "mv" };
                WriteExecutable(Path.Combine(bin, command), "#!/bin/sh\nexit 1\n");
                psi.Environment["PATH"] = bin + ":" + Environment.GetEnvironmentVariable("PATH");
            }
            using var process = Process.Start(psi)!;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await process.WaitForExitAsync(timeout.Token);
            var success = scenario is "versioned" or "canonical" or "special-path";
            Assert.Equal(success ? 0 : 1, process.ExitCode);
            // Relaunch is asynchronous, but its output is a real executable-side observation.
            for (var i = 0; i < 100 && (!File.Exists(launched) || new FileInfo(launched).Length == 0); i++)
                await Task.Delay(20, timeout.Token);
            Assert.Equal(success ? "new" : "old", File.ReadAllText(launched));
            if (success)
            {
                Assert.Equal(newBytes, File.ReadAllText(target));
                if (!canonical) Assert.False(File.Exists(current));
            }
            else Assert.Equal(oldBytes, File.ReadAllText(current));
            if (scenario == "existing-file") Assert.Equal("unrelated file", File.ReadAllText(target));
            if (scenario == "existing-directory") Assert.Empty(Directory.EnumerateFileSystemEntries(target));
            if (scenario == "existing-symlink") Assert.NotNull(new FileInfo(target).LinkTarget);
            Assert.Empty(Directory.GetDirectories(directory, ".pkhex-update.*"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static void WriteExecutable(string path, string text)
    {
        File.WriteAllText(path, text);
        if (OperatingSystem.IsLinux())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}

public sealed class LinuxTheoryAttribute : TheoryAttribute
{
    public LinuxTheoryAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!OperatingSystem.IsLinux()) Skip = "Executes Linux shell commands; covered by Linux CI.";
    }
}
