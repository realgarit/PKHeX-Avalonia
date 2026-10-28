using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

/// <summary>
/// Number fields in save editors show their largest value in full. Spinner buttons and padding take about
/// 90 px of a field, so fields sized for the value alone cut it off ("1(" for 100) or showed no digit at all.
/// </summary>
public sealed class NumericFieldWidthRenderTests
{
    private static readonly string SaveDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../savefiles"));

    private static SaveFile Load(string name)
    {
        Assert.True(SaveUtil.TryGetSaveFile(Path.Combine(SaveDirectory, name), out var save), name);
        return save!;
    }

    // Editor, fields expected on screen. Mail Box already fitted and guards the shared measurement.
    public static IEnumerable<object[]> Cases =>
        new (string Editor, int Fields)[] { ("apricorn", 7), ("daycare-hgss", 2), ("daycare-e", 2), ("hof1", 3), ("link6", 8), ("mail-e", 11), ("opower", 35) }
            .SelectMany(e => new[] { AppDensity.Compact, AppDensity.Comfortable }.Select(d => new object[] { e.Editor, e.Fields, d }));

    private static (Control View, object ViewModel) Build(string editor) => editor switch
    {
        "apricorn" => (new ApricornEditor(), new ApricornEditorViewModel(Load("gen4_heartgold.sav"))),
        "daycare-hgss" => (new DaycareEditorView(), new DaycareEditorViewModel(Load("gen4_heartgold.sav"), Mock.Of<ISpriteRenderer>())),
        "daycare-e" => (new DaycareEditorView(), new DaycareEditorViewModel(Load("gen3_emerald.sav"), Mock.Of<ISpriteRenderer>())),
        "hof1" => (new HallOfFame1Editor(), new HallOfFame1EditorViewModel((SAV1)Load("gen1_red.sav"))),
        "link6" => (new Link6Editor(), new Link6EditorViewModel(Load("gen6_x.main"), Mock.Of<IDialogService>())),
        "mail-e" => (new MailBoxEditor(), new MailBoxEditorViewModel(Load("gen3_emerald.sav"))),
        "opower" => (new OPowerEditor(), new OPowerEditorViewModel(Load("gen6_x.main"))),
        _ => throw new ArgumentOutOfRangeException(nameof(editor)),
    };

    [AvaloniaTheory]
    [MemberData(nameof(Cases))]
    public async Task EveryNumberFieldShowsItsMaximum(string editor, int fields, AppDensity density)
    {
        var densityService = new UiDensityService(new AppSettings(), Mock.Of<ISettingsStore>());
        var directory = Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "TestResults");
        Directory.CreateDirectory(directory);
        var capture = Path.Combine(directory, $"numeric-fields-{editor}-{density}");
        var diagnostics = new StringBuilder();
        Window? window = null;
        try
        {
            densityService.ApplyDensity(density);
            var (view, viewModel) = Build(editor);
            view.DataContext = viewModel;
            // The view's own minimum is the narrowest supported layout; views without one use a small tool size.
            var width = view.MinWidth > 0 ? view.MinWidth : 600;
            var height = view.MinHeight > 0 ? view.MinHeight : 500;
            window = new Window { Content = view, Width = width, Height = height };
            window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            var inputs = view.GetVisualDescendants().OfType<NumericUpDown>().Where(input => input.IsEffectivelyVisible).ToList();
            foreach (var input in inputs)
                input.Value = input.Maximum;
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            // Retain the real frame even when a geometry assertion fails on a CI platform.
            using (var frame = new RenderTargetBitmap(new PixelSize((int)width, (int)height)))
            {
                frame.Render(window);
                frame.Save(capture + ".png", PngBitmapEncoderOptions.Default);
            }

            Assert.Equal(fields, inputs.Count);
            for (var i = 0; i < inputs.Count; i++)
            {
                var input = inputs[i];
                var name = $"{AutomationProperties.GetName(input)} #{i}".Trim();
                NumericTextFit.AssertFits(input, input.Maximum.ToString("0", CultureInfo.CurrentCulture), name, diagnostics);
                var right = input.TranslatePoint(new Point(input.Bounds.Width, 0), view)!.Value.X;
                Assert.True(right <= width + 0.5, $"{name} ends at {right}, outside the {width} px view.");
            }
        }
        finally
        {
            File.WriteAllText(capture + ".txt", diagnostics.ToString());
            window?.Close();
            densityService.ApplyDensity(AppDensity.Compact);
        }
    }
}
