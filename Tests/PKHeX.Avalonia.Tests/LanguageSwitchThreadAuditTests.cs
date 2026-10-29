using System.Text.RegularExpressions;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Guardrail: a test that changes the UI language must run on the Avalonia UI thread.
///
/// <c>LocalizedStrings.Instance</c> is process-wide. Views from earlier tests in the same process
/// can still be bound to it until they are collected, so a language change notifies their bindings.
/// A plain <c>[Fact]</c>/<c>[Theory]</c> runs on a thread-pool thread, and a bound control such as a
/// <c>DataGrid</c> then throws "The calling thread cannot access this object". That failure depends
/// on test order, so it showed up only now and then (macOS CI, <c>LanguageChange_RefreshesActionTitles</c>).
///
/// The scan is static and regex-based, like <see cref="AccessibilityAuditTests"/>: each test method
/// body that calls <c>SetLanguage(</c> must carry <c>[AvaloniaFact]</c> or <c>[AvaloniaTheory]</c>.
/// It sees direct calls in the test body only, not calls made through helper methods.
/// </summary>
public class LanguageSwitchThreadAuditTests
{
    // Split so this file does not match its own scan.
    private const string LanguageSwitchCall = "SetLanguage" + "(";

    private static readonly Regex TestMethod = new(
        @"\[(Fact|Theory|AvaloniaFact|AvaloniaTheory)\b[^\]]*\]\s*(?:\[[^\]]*\]\s*)*public[^\(]*?(\w+)\(",
        RegexOptions.Compiled);

    private static readonly Regex NextTestAttribute = new(
        @"\n    \[(Fact|Theory|AvaloniaFact|AvaloniaTheory)\b",
        RegexOptions.Compiled);

    [Fact]
    public void TestsThatChangeTheLanguage_RunOnTheUiThread()
    {
        var repoRoot = FindRepoRoot();
        var testsRoot = Path.Combine(repoRoot, "Tests");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
            if (relative.Contains("/obj/") || relative.Contains("/bin/"))
                continue;

            var source = File.ReadAllText(file);
            if (!source.Contains(LanguageSwitchCall))
                continue;

            foreach (Match method in TestMethod.Matches(source))
            {
                var bodyStart = method.Index + method.Length;
                var next = NextTestAttribute.Match(source, bodyStart);
                var body = source[bodyStart..(next.Success ? next.Index : source.Length)];
                var attribute = method.Groups[1].Value;
                if (attribute is "Fact" or "Theory" && body.Contains(LanguageSwitchCall))
                    offenders.Add($"{relative}: {method.Groups[2].Value} uses [{attribute}]");
            }
        }

        Assert.True(offenders.Count == 0,
            "These tests change the UI language off the UI thread. Use [AvaloniaFact] or [AvaloniaTheory]:\n"
            + string.Join('\n', offenders));
    }

    private static string FindRepoRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!Directory.Exists(Path.Combine(dir, "PKHeX.Avalonia")))
        {
            var parent = Directory.GetParent(dir);
            if (parent == null) throw new DirectoryNotFoundException("Could not find repository root");
            dir = parent.FullName;
        }
        return dir;
    }
}
