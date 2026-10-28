using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using System.Text;

namespace PKHeX.Avalonia.Render.Tests;

public class StatExperienceRenderTests
{
    [AvaloniaTheory]
    [InlineData(GameVersion.RD, false, 300)]
    [InlineData(GameVersion.RD, false, 360)]
    [InlineData(GameVersion.C, true, 300)]
    [InlineData(GameVersion.C, true, 360)]
    public async Task FiveDigitStatExperienceFitsCompactStatsTable(GameVersion version, bool dark, int width)
    {
        var application = global::Avalonia.Application.Current!;
        var previousTheme = application.RequestedThemeVariant;
        var previousSources = GameInfo.FilteredSources;
        var save = BlankSaveFile.Get(version);
        var pokemon = Assert.IsAssignableFrom<GBPKM>(save.BlankPKM);
        pokemon.Species = 25;
        pokemon.CurrentLevel = 50;
        pokemon.MaxEVs();
        GameInfo.FilteredSources = new FilteredGameDataSource(save, GameInfo.Sources, false);
        var renderer = new AvaloniaSpriteRenderer(new AppSettings());
        renderer.Initialize(save);
        var vm = new PokemonEditorViewModel(pokemon, save, renderer, Mock.Of<IDialogService>(), Mock.Of<IWindowService>());
        vm.SelectEditorSectionCommand.Execute("1");
        var view = new PokemonEditor { DataContext = vm };
        var window = new Window { Content = view, Width = width, Height = 700 };
        var directory = Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "TestResults");
        Directory.CreateDirectory(directory);
        var capture = Path.Combine(directory, $"stat-experience-{version}-{width}-{(dark ? "dark" : "light")}");
        var diagnostics = new StringBuilder();
        try
        {
            application.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            // Retain the real frame even when a geometry assertion fails on a CI platform.
            using (var frame = new RenderTargetBitmap(new PixelSize(width, 700)))
            {
                frame.Render(window);
                frame.Save(capture + ".png", PngBitmapEncoderOptions.Default);
            }
            var names = new[] { vm.HpEvAutomationName, vm.AtkEvAutomationName, vm.DefEvAutomationName,
                vm.SpaEvAutomationName, vm.SpdEvAutomationName, vm.SpeEvAutomationName };
            foreach (var name in names)
            {
                var input = view.GetVisualDescendants().OfType<NumericUpDown>()
                    .Single(control => AutomationProperties.GetName(control) == name);
                Assert.Equal(65535m, input.Value);
                var presenter = input.GetVisualDescendants().OfType<TextPresenter>().Single();
                Assert.Equal("65535", presenter.Text);
                var line = Assert.Single(presenter.TextLayout.TextLines);
                // A centered presenter has a rounded natural width and does not clip its text.
                // Test the complete line, including ink overhang, against the actual clip viewports.
                var left = line.Start + Math.Min(0, line.OverhangLeading);
                var right = line.Start + line.WidthIncludingTrailingWhitespace - Math.Min(0, line.OverhangTrailing);
                var viewport = presenter.GetVisualAncestors().OfType<ScrollContentPresenter>().First();
                diagnostics.AppendLine($"{name}: input={input.Bounds}, font={input.FontFamily}/{input.FontSize}, presenter={presenter.Bounds}, line=[{left}, {right}], viewport={viewport.Viewport}, extent={viewport.Extent}, offset={viewport.Offset}");
                var clips = presenter.GetVisualAncestors().Prepend(presenter)
                    .Where(visual => visual.ClipToBounds).ToArray();
                Assert.Contains(viewport, clips);
                foreach (var clip in clips)
                {
                    Assert.Null(clip.Clip);
                    var projectedLeft = presenter.TranslatePoint(new Point(left, 0), clip)!.Value.X;
                    var projectedRight = presenter.TranslatePoint(new Point(right, 0), clip)!.Value.X;
                    diagnostics.AppendLine($"  {clip.GetType().Name}: bounds={clip.Bounds}, text=[{projectedLeft}, {projectedRight}], clip={clip.Clip}");
                    Assert.True(projectedLeft >= 0 && projectedRight <= clip.Bounds.Width,
                        $"Five-digit text [{projectedLeft}, {projectedRight}] crosses {clip.GetType().Name} viewport [0, {clip.Bounds.Width}].");
                }
                var position = input.TranslatePoint(default, window)!.Value;
                Assert.InRange(position.X + input.Bounds.Width, 0, window.ClientSize.Width);
            }
            Assert.Equal(327675, vm.EVTotal);
        }
        finally
        {
            File.WriteAllText(capture + ".txt", diagnostics.ToString());
            window.Close();
            application.RequestedThemeVariant = previousTheme;
            GameInfo.FilteredSources = previousSources;
        }
    }
}
