using Avalonia.Controls;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Core;
using PKHeX.Presentation.Models;
using PKHeX.Presentation.ViewModels;
using PKHeX.Avalonia.Views;

namespace PKHeX.Avalonia.Tests.Harness;

public sealed class CompactUiCaptureTests
{
    [AvaloniaFact]
    public async Task ReadmeHero_UsesLegalSaveFixture()
    {
        var directory = Fixtures.SaveFileFixture.FindSaveFilesPath()!;
        var save = Assert.IsType<SAV9ZA>(Fixtures.SaveFileFixture.LoadSave(Path.Combine(directory, "gen9a_legendsza.main")));
        save.CurrentBox = 0;
        using var app = new HeadlessAppFixture();
        app.Window.Width = 900;
        app.Window.Height = 600;
        app.Services.GetRequiredService<AppSettings>().Sprite.SpritePreference = SpritePreference.ForceArtwork;
        for (int slot = 0; slot < save.BoxSlotCount; slot++)
            Assert.True(new LegalityAnalysis(save.GetBoxSlotAtIndex(0, slot)).Valid, $"Source box slot {slot + 1} is not legal.");
        for (int slot = 0; slot < save.PartyCount; slot++)
            Assert.True(new LegalityAnalysis(save.GetPartySlotAtIndex(slot)).Valid, $"Source party slot {slot + 1} is not legal.");
        app.LoadSaveInstance(save, "gen9a_legendsza.main");
        Assert.All(app.BoxViewer!.Slots, slot => Assert.True(slot.IsLegal, $"Box 1 slot {slot.Slot + 1} is not legal."));
        Assert.All(app.ViewModel.PartyViewer!.Slots, slot => Assert.True(slot.IsLegal, $"Party slot {slot.Slot + 1} is not legal."));
        app.ViewModel.CurrentPokemonEditor!.LoadPKM(save.GetBoxSlotAtIndex(0, 5));
        Assert.True(app.ViewModel.CurrentPokemonEditor.IsLegal, app.ViewModel.CurrentPokemonEditor.LegalityReport);
        app.BoxViewer.SelectedIndex = 5;
        var theme = app.Services.GetRequiredService<IThemeService>();
        foreach (var variant in new[] { AppTheme.Light, AppTheme.Dark })
        {
            theme.ApplyTheme(variant);
            app.ViewModel.RefreshThemeSelection();
            app.Pump();
            await Task.Delay(250);
            app.Pump();
            if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") != "1") continue;
            var captureDirectory = Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR")!;
            Directory.CreateDirectory(captureDirectory);
            Assert.NotNull(app.CaptureFrame(Path.Combine(captureDirectory, $"readme-legal-{variant.ToString().ToLowerInvariant()}.png")));
        }
    }

    [AvaloniaTheory]
    [InlineData("de")]
    [InlineData("ja")]
    public async Task CompactNavigationRemainsInsideTheEditorWhenLocalized(string language)
    {
        using var app = new HeadlessAppFixture();
        app.Services.GetRequiredService<IThemeService>().ApplyTheme(AppTheme.Dark);
        app.Window.Width = 900;
        app.Window.Height = 600;
        app.LoadSaveInstance(new SAV6XY());
        app.ViewModel.LanguageService.SetLanguage(language);
        try
        {
            app.Pump();
            await Task.Delay(250);
            app.Pump();
            var editor = app.Window.GetVisualDescendants().OfType<PokemonEditor>().Single();
            var navigation = editor.GetVisualDescendants().OfType<Button>().Where(x => x.Classes.Contains("editor-nav") || x.Name == "MoreSectionsButton").ToArray();
            Assert.Equal(6, navigation.Length);
            double previousRight = 0;
            foreach (var button in navigation)
            {
                var origin = button.TranslatePoint(default, editor)!.Value;
                Assert.True(origin.X >= previousRight - 0.1);
                previousRight = origin.X + button.Bounds.Width;
                Assert.True(previousRight <= editor.Bounds.Width);
            }
            if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1")
            {
                var directory = Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") ?? Path.Combine(Path.GetTempPath(), "pkhex-compact-captures");
                Assert.NotNull(app.CaptureFrame(Path.Combine(directory, $"compact-localized-{language}.png")));
            }
        }
        finally { app.ViewModel.LanguageService.SetLanguage("en"); }
    }

    [AvaloniaFact]
    public async Task LegalFixtureDisplaysTheActualLegalPill()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PKHeX.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var path = Path.Combine(root.FullName, "Tests", "PKHeX.Core.Tests", "Legality", "Legal", "Generation 9", "FormArg", "0983 - Kingambit - 6BE42E9A9EF4 0 asKing.pk9");
        var pk = new PK9(File.ReadAllBytes(path));
        Assert.True(new LegalityAnalysis(pk).Valid);
        using var app = new HeadlessAppFixture();
        app.Window.Width = 900;
        app.Window.Height = 600;
        var save = new SAV9SV { Version = pk.Version, OT = pk.OriginalTrainerName, ID32 = pk.ID32, Gender = pk.OriginalTrainerGender, Language = pk.Language };
        save.SetBoxSlotAtIndex(pk.Clone(), 0);
        app.LoadSaveInstance(save, "main");
        app.ViewModel.CurrentPokemonEditor!.LoadPKM(pk);
        Assert.True(app.ViewModel.CurrentPokemonEditor.IsLegal, app.ViewModel.CurrentPokemonEditor.LegalityReport);
        var theme = app.Services.GetRequiredService<IThemeService>();
        foreach (var variant in new[] { AppTheme.Light, AppTheme.Dark })
        {
            theme.ApplyTheme(variant);
            app.ViewModel.RefreshThemeSelection();
            app.Pump();
            await Task.Delay(250);
            app.Pump();
            var pill = app.Window.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "LegalityPill");
            Assert.True(pill.IsEffectivelyVisible);
            if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") != "1") continue;
            var directory = Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") ?? Path.Combine(Path.GetTempPath(), "pkhex-compact-captures");
            Assert.NotNull(app.CaptureFrame(Path.Combine(directory, $"compact-legal-{variant.ToString().ToLowerInvariant()}.png")));
        }
    }

    [AvaloniaFact]
    public async Task CompactShell_RealCompositionFitsAndCapturesBothThemes()
    {
        using var app = new HeadlessAppFixture();
        app.Window.Width = 900;
        app.Window.Height = 600;
        app.Services.GetRequiredService<AppSettings>().Sprite.SpritePreference = SpritePreference.ForceArtwork;
        var save = new SAV9SV { OT = "Patrik", Version = GameVersion.SL };
        ushort[] species = [282, 6, 658, 448, 700, 149, 25, 196, 197, 470, 471, 133, 94, 778, 38, 59, 350, 359, 373, 445, 376, 248, 468, 407];
        for (var i = 0; i < species.Length; i++)
        {
            var pk = new PK9 { Species = species[i], PID = 0x12345678, CurrentLevel = 72, Language = 2, OriginalTrainerName = "Patrik", Nickname = GameInfo.Strings.specieslist[species[i]], HeldItem = 234, Nature = Nature.Modest };
            pk.RefreshAbility(0);
            save.SetBoxSlotAtIndex(pk, i);
            if (i < 6) save.SetPartySlotAtIndex(pk, i);
        }
        app.LoadSaveInstance(save, "main");
        app.ViewModel.CurrentPokemonEditor!.LoadPKM(save.GetBoxSlotAtIndex(0));
        app.BoxViewer!.SelectedIndex = 1;
        app.BoxViewer.SelectedIndex = 0;
        var theme = app.Services.GetRequiredService<IThemeService>();
        foreach (var variant in new[] { AppTheme.Light, AppTheme.Dark })
        {
            theme.ApplyTheme(variant);
            app.ViewModel.RefreshThemeSelection();
            app.Pump();
            await Task.Delay(250);
            app.Pump();
            var slots = app.Window.GetVisualDescendants().OfType<Button>().Where(x => x.Tag is SlotData).ToArray();
            Assert.Equal(30, slots.Length);
            Assert.All(slots, x =>
            {
                Assert.True(x.Bounds.Width >= 70 && x.Bounds.Height >= 48);
                var origin = x.TranslatePoint(default, app.Window)!.Value;
                Assert.InRange(origin.X, 300, 900 - x.Bounds.Width);
                Assert.InRange(origin.Y, 80, 600 - x.Bounds.Height);
            });
            for (var row = 0; row < 5; row++)
            {
                var y = slots[row * 6].TranslatePoint(default, app.Window)!.Value.Y;
                Assert.All(slots.Skip(row * 6).Take(6), slot => Assert.InRange(Math.Abs(slot.TranslatePoint(default, app.Window)!.Value.Y - y), 0, 0.1));
            }
            if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") != "1") continue;
            var directory = Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") ?? Path.Combine(Path.GetTempPath(), "pkhex-compact-captures");
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, $"compact-production-{variant.ToString().ToLowerInvariant()}.png");
            Assert.NotNull(app.CaptureFrame(file));
            Assert.True(new FileInfo(file).Length > 0);

            var editorVm = app.ViewModel.CurrentPokemonEditor!;
            for (var section = 1; section <= 4; section++)
            {
                editorVm.SelectedEditorSection = section;
                app.Pump();
                await Task.Delay(200);
                app.Pump();
                Assert.NotNull(app.CaptureFrame(Path.Combine(directory, $"compact-editor-{section}-{variant.ToString().ToLowerInvariant()}.png")));
            }
            editorVm.SelectedEditorSection = 0;
            app.Pump();
            var nature = app.Window.GetVisualDescendants().OfType<ComboBox>().First(combo => ReferenceEquals(combo.ItemsSource, editorVm.NatureList));
            nature.IsDropDownOpen = true;
            await Task.Delay(200);
            app.Pump();
            Assert.NotNull(app.CaptureFrame(Path.Combine(directory, $"compact-dropdown-{variant.ToString().ToLowerInvariant()}.png")));
            nature.IsDropDownOpen = false;

            var settings = app.Services.GetRequiredService<AppSettings>();
            var settingsVm = new SettingsViewModel(settings, app.Services.GetRequiredService<ISettingsStore>(), theme,
                app.Services.GetRequiredService<IUiDensityService>(), app.Services.GetRequiredService<LanguageService>(), UpdateTestDoubles.Coordinator());
            var preferences = new Window { Width = 390, Height = 492, Content = new SettingsView { DataContext = settingsVm } };
            preferences.Show();
            app.Pump();
            await Task.Delay(250);
            app.Pump();
            using var frame = preferences.GetLastRenderedFrame();
            Assert.NotNull(frame);
            using (var stream = File.Create(Path.Combine(directory, $"compact-settings-{variant.ToString().ToLowerInvariant()}.png"))) frame.Save(stream);
            preferences.Close();
        }
    }
}
