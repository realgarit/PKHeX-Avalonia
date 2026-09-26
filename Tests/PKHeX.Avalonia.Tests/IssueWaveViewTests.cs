using Avalonia;
using PKHeX.Application.Services;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using PKHeX.Application.Abstractions;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class IssueWaveViewTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZygardeCounters_HaveGameSpecificAccessibleNames(bool ultra)
    {
        using var app = new HeadlessAppFixture();
        var vm = new ZygardeCellEditorViewModel(ultra ? new SAV7USUM() : new SAV7SM());
        var view = ViewLocator.Build(vm);
        var window = new Window { Content = view, Width = 660, Height = 520 };
        try
        {
            window.Show();
            Pump(window);
            var counters = view.GetVisualDescendants().OfType<NumericUpDown>().Take(2).ToArray();
            Assert.Equal(2, counters.Length);
            Assert.Equal(vm.StoredCounterLabel, AutomationProperties.GetName(counters[0]));
            Assert.Equal(vm.CollectedCounterLabel, AutomationProperties.GetName(counters[1]));
            Assert.Contains(ultra ? "Stickers" : "cells", AutomationProperties.GetName(counters[0]), StringComparison.Ordinal);
            Assert.Contains(ultra ? "Stickers" : "cells", AutomationProperties.GetName(counters[1]), StringComparison.Ordinal);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("beans-sm", "gen7_sun.main", 440, 520)]
    [InlineData("beans-usum", "gen7_ultrasun.main", 440, 520)]
    [InlineData("cells-sm", "gen7_sun.main", 660, 520)]
    [InlineData("cells-usum", "gen7_ultrasun.main", 660, 520)]
    [InlineData("records-5", "gen5_white2.sav", 620, 520)]
    [InlineData("records-6", "gen6_x.main", 620, 520)]
    [InlineData("records-7", "gen7_sun.main", 620, 520)]
    [InlineData("records-8-synthetic", "", 620, 520)]
    [InlineData("records-bdsp", "gen8b_brilliantdiamond.bin", 620, 520)]
    [InlineData("poffins", "gen8b_brilliantdiamond.bin", 660, 530)]
    public void EditorBindingsAndClosePreserveSource(string scenario, string fixture, double width, double height)
    {
        using var app = new HeadlessAppFixture();
        var save = fixture.Length == 0 ? new SAV8SWSH() :
            SaveFileFixture.LoadSave(Path.Combine(SaveFileFixture.FindSaveFilesPath()!, fixture))!;
        Assert.NotNull(save);
        var before = save.Data.ToArray();
        object vm = scenario.Split('-')[0] switch
        {
            "beans" => new PokebeanEditorViewModel(save),
            "cells" => new ZygardeCellEditorViewModel(save),
            "records" => new RecordsEditorViewModel(save),
            _ => new Poffin8bEditorViewModel(save),
        };
        var view = ViewLocator.Build(vm);
        var window = new Window { Content = view, Width = width, Height = height };
        ((ICloseableDialog)vm).CloseRequested = window.Close;
        try
        {
            window.Show();
            Pump(window);
            if (vm is Poffin8bEditorViewModel poffins)
            {
                var combo = view.GetVisualDescendants().OfType<ComboBox>().Single();
                Assert.Equal(poffins.SelectedPoffin!.MstID, Assert.IsType<ComboItem>(combo.SelectedItem).Value);
                combo.SelectedValue = 0;
                Pump(window);
                Assert.Equal(0, poffins.SelectedPoffin.MstID);
            }
            foreach (var button in view.GetVisualDescendants().OfType<Button>().Where(b => b.Command is not null))
            {
                var point = button.TranslatePoint(default, window)!.Value;
                Assert.InRange(point.X, 0, width);
                Assert.InRange(point.Y + button.Bounds.Height, 0, height);
            }
            Capture(window, scenario);
        }
        finally { window.Close(); }
        Assert.Equal(before, save.Data);
    }

    [AvaloniaFact]
    public void MetadataLanguageRefresh_PreservesSelectionsAndBytes()
    {
        using var app = new HeadlessAppFixture();
        var save = new SAV9ZA();
        var pk = new PA9 { Species = 25, Version = GameVersion.ZA, BattleVersion = GameVersion.SW,
            HandlingTrainerLanguage = 7, Tracker = 0xFEDCBA9876543210, IsAlpha = true, Scale = 255,
            HeightScalar = 37, WeightScalar = 129, ObedienceLevel = 44 };
        app.LoadSaveInstance(save);
        var editor = app.ViewModel.CurrentPokemonEditor!;
        editor.LoadPKM(pk);
        editor.SelectedEditorSection = 2;
        app.Pump();
        var before = editor.PreparePKM().Data.ToArray();
        app.Services.GetRequiredService<LanguageService>().SetLanguage("de");
        app.Pump();
        Assert.Equal((int)GameVersion.SW, editor.BattleVersion);
        Assert.Equal(7, editor.HandlingTrainerLanguage);
        Assert.Equal(before, editor.PreparePKM().Data);
        editor.SelectedEditorSection = 4;
        app.Pump();
        app.Services.GetRequiredService<LanguageService>().SetLanguage("en");
        app.Pump();
        Assert.Equal(before, editor.PreparePKM().Data);
        if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1")
        {
            app.Window.Width = 900;
            app.Window.Height = 600;
            for (int section = 1; section <= 4; section++)
            {
                editor.SelectedEditorSection = section;
                app.Pump();
                Pump(app.Window);
                var editorView = app.Window.GetVisualDescendants().OfType<PKHeX.Avalonia.Views.PokemonEditor>().Single();
                foreach (var scroll in editorView.GetVisualDescendants().OfType<ScrollViewer>().Where(s => s.IsEffectivelyVisible))
                    scroll.Offset = new Vector(0, scroll.Extent.Height);
                app.Pump();
                Pump(app.Window);
                Capture(app.Window, $"metadata-{section}");
            }
            var plusWindow = new Window
            {
                Content = ViewLocator.Build(new PlusRecordEditorViewModel(editor.PreparePKM())),
                Width = 460, Height = 500,
            };
            try { plusWindow.Show(); Pump(plusWindow); Capture(plusWindow, "plus-records"); }
            finally { plusWindow.Close(); }
        }
    }

    private static void Pump(Window window)
    {
        for (int i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static void Capture(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") != "1") return;
        var directory = Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR")!;
        Directory.CreateDirectory(directory);
        using var frame = window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, name + ".png"));
    }
}
