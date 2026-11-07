using Avalonia;
using Avalonia.Automation;
using CommunityToolkit.Mvvm.Messaging;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class SafariZone4EditorTests
{
    private static SAV4HGSS LoadHeartGold() => Assert.IsType<SAV4HGSS>(SaveFileFixture.LoadSave(
        Path.Combine(SaveFileFixture.FindSaveFilesPath()!, "gen4_heartgold.sav")));

    private static SafariZoneCellViewModel FirstFreeLand(SafariZone4EditorViewModel vm)
    {
        var map = (SafariZoneType4)vm.SelectedArea!.Map;
        return vm.Cells.First(c => c.Occupant is null && SafariZoneMap4.IsLand(map, c.X, c.Z));
    }

    [Fact]
    public void NoOpSaveKeepsEverySaveByteAndEditedState()
    {
        var save = LoadHeartGold();
        save.State.Edited = false;
        var data = save.Data.ToArray();
        var general = save.General.ToArray();
        var vm = new SafariZone4EditorViewModel(save);

        // Visit every set and area so all row view models are realized, then save without edits.
        foreach (var set in vm.Sets)
        {
            vm.SelectedSet = set;
            foreach (var area in set.Areas)
                vm.SelectedArea = area;
        }
        bool closed = false;
        vm.CloseRequested = () => closed = true;
        vm.SaveCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal(general, save.General.ToArray());
        Assert.Equal(data, save.Data.ToArray());
        Assert.False(save.State.Edited);
        Assert.Equal(0, new SafariZone4EditorViewModel(save).Commit());
    }

    [Fact]
    public void NoOpPreservesUnknownRawValues()
    {
        var save = LoadHeartGold();
        var safari = save.Safari;
        var self = safari.Self;
        self.Configurations[2].Map = (SafariZoneType4)0xEE; // outside the enum
        self.Configurations[2].Objects[4] = new SafariZoneObject4 { Type = (SafariZoneObjectType4)0x77, X = 200, Z = 201, Y = 202 };
        self.Configurations[2].Count = 31;
        self.FlushConfiguration(2);
        safari.AreaSwitcherState = 1;
        save.State.Edited = false;
        var before = save.General.ToArray();

        var vm = new SafariZone4EditorViewModel(save);
        vm.SelectedArea = vm.Sets[0].Areas[2];
        Assert.Contains(vm.SelectedArea.MapChoices, c => c.Value == 0xEE);
        Assert.Contains(vm.SelectedArea.Objects[4].TypeChoices, c => c.Value == 0x77);
        vm.SaveCommand.Execute(null);

        Assert.Equal(before, save.General.ToArray());
        Assert.False(save.State.Edited);
    }

    [Fact]
    public void FieldEditsRoundTripThroughExportAndReloadAndOnlyTouchTheSafariBlock()
    {
        var save = LoadHeartGold();
        var general = save.General.ToArray();
        var data = save.Data.ToArray();
        var vm = new SafariZone4EditorViewModel(save);

        vm.SelectedSet = vm.Sets[0];
        vm.SelectedArea = vm.Sets[0].Areas[1];
        vm.ClearObjectsCommand.Execute(null);
        vm.SelectedArea.Map = (int)SafariZoneType4.Plains;
        var tile = vm.Cells.Single(c => c.X == 12 && c.Z == 2);
        vm.SelectTileCommand.Execute(tile);
        var placed = Assert.IsType<SafariZoneObjectRow>(vm.SelectedObject);
        Assert.Equal(0, placed.Index);
        placed.Type = (int)SafariZoneObjectType4.RedFlower;
        Assert.Equal(1, vm.SelectedArea.Count);
        Assert.Equal(16, placed.Y); // terrain height of the clicked tile

        vm.Sets[1].Ages[(int)SafariZoneType4.Desert].Value = 99;
        vm.DayDelay = 3;
        vm.ElapsedPlayMinutes = 1234;
        vm.ObjectUnlockLevel = 4;
        vm.IsAreaSwitcherUnlocked = true;
        vm.IsLinked = true;
        vm.LinkTrainerName = "Lyra";
        vm.LinkTrainerId = 0x12345678;

        // Nothing is written before Save.
        Assert.Equal(general, save.General.ToArray());
        vm.SaveCommand.Execute(null);
        Assert.True(save.State.Edited);

        // Every changed byte of the whole save sits inside one Safari-sized window.
        var changed = Enumerable.Range(0, data.Length).Where(i => data[i] != save.Data[i]).ToArray();
        Assert.NotEmpty(changed);
        Assert.True(changed[^1] - changed[0] < SafariZone4.SIZE);

        // Export and re-detect the save from its bytes.
        var exported = save.Write().ToArray();
        Assert.True(SaveUtil.TryGetSaveFile(exported, out var reopened));
        var reloaded = Assert.IsType<SAV4HGSS>(reopened).Safari;
        var area = reloaded.Self.Configurations[1];
        Assert.Equal(SafariZoneType4.Plains, area.Map);
        Assert.Equal(1, area.Count);
        Assert.Equal(SafariZoneObjectType4.RedFlower, area.Objects[0].Type);
        Assert.Equal((12, 2, 16), (area.Objects[0].X, area.Objects[0].Z, area.Objects[0].Y));
        Assert.True(area.Objects.Skip(1).All(o => o.IsEmpty));
        Assert.Equal(99, reloaded.Link.GetMapAge(SafariZoneType4.Desert));
        Assert.Equal(3, reloaded.DayDelay);
        Assert.Equal(1234, reloaded.ElapsedPlayMinutes);
        Assert.Equal(4, reloaded.ObjectUnlockLevel);
        Assert.Equal(1, reloaded.AreaSwitcherState);
        Assert.True(reloaded.LinkLeader.IsLinked);
        Assert.Equal("Lyra", reloaded.LinkLeader.OriginalTrainerName);
        Assert.Equal(0x12345678u, reloaded.LinkLeader.ID32);

        // The editor shows the committed values when opened again.
        var again = new SafariZone4EditorViewModel(Assert.IsType<SAV4HGSS>(reopened));
        Assert.Equal((int)SafariZoneType4.Plains, again.Sets[0].Areas[1].Map);
        Assert.Equal((int)SafariZoneObjectType4.RedFlower, again.Sets[0].Areas[1].Objects[0].Type);
        Assert.Equal("Lyra", again.LinkTrainerName);
    }

    [Fact]
    public void CancelAndResetDiscardStagedChanges()
    {
        var save = LoadHeartGold();
        var before = save.General.ToArray();
        var vm = new SafariZone4EditorViewModel(save);
        var area = vm.SelectedArea!;
        int map = area.Map;
        area.Map = (map + 1) % SafariZone4EditorViewModel.MapCount;
        vm.DayDelay = (vm.DayDelay + 1) % 256;
        vm.MaxAgesCommand.Execute(null);

        vm.ResetCommand.Execute(null);
        Assert.Equal(map, vm.SelectedArea!.Map);
        Assert.Equal(0, vm.Commit());
        Assert.Equal(before, save.General.ToArray());

        vm.ClearObjectsCommand.Execute(null);
        vm.ClearAgesCommand.Execute(null);
        bool closed = false;
        vm.CloseRequested = () => closed = true;
        vm.CancelCommand.Execute(null);
        Assert.True(closed);
        Assert.Equal(before, save.General.ToArray());
    }

    [Fact]
    public void MapClicksSelectPlaceAndReportFullAreas()
    {
        var vm = new SafariZone4EditorViewModel(LoadHeartGold());
        vm.ClearObjectsCommand.Execute(null);
        vm.SelectedArea!.Map = (int)SafariZoneType4.Plains;

        var tile = FirstFreeLand(vm);
        vm.SelectTileCommand.Execute(tile);
        var row = vm.SelectedObject!;
        row.Type = (int)SafariZoneObjectType4.Tree;
        Assert.Same(row, tile.Occupant);
        Assert.True(tile.IsSelected);
        Assert.Equal(row.Number.ToString(), tile.Label);
        // A tree covers 2 x 2 tiles; selecting any of them selects the same row.
        var corner = vm.Cells.Single(c => c.X == tile.X + 1 && c.Z == tile.Z + 1);
        vm.SelectedObject = null;
        vm.SelectTileCommand.Execute(corner);
        Assert.Same(row, vm.SelectedObject);

        vm.RemoveObjectCommand.Execute(null);
        Assert.True(row.IsEmpty);
        Assert.Null(tile.Occupant);

        foreach (var o in vm.SelectedArea.Objects)
            o.SetPosition(31, 31, 0);
        var free = FirstFreeLand(vm);
        vm.SelectTileCommand.Execute(free);
        Assert.Null(free.Occupant);
        Assert.Equal(LocalizedStrings.Instance["Safari4_SlotsFull"], vm.StatusText);
    }

    [Fact]
    public void PlacementProblemsAreFlaggedOnRowsAndTiles()
    {
        var vm = new SafariZone4EditorViewModel(LoadHeartGold());
        vm.ClearObjectsCommand.Execute(null);
        vm.SelectedArea!.Map = (int)SafariZoneType4.Plains;
        var rows = vm.SelectedArea.Objects;
        rows[0].Type = (int)SafariZoneObjectType4.Tree;
        rows[0].SetPosition(2, 12, 16);
        rows[1].SetPosition(3, 13, 16); // shrubbery inside the tree
        rows[2].SetPosition(1, 2, 0);   // shrubbery on a tile without terrain

        Assert.Equal(SafariZonePlacement4.Overlap, rows[0].Placement);
        Assert.Equal(SafariZonePlacement4.Overlap, rows[1].Placement);
        Assert.Equal(SafariZonePlacement4.Unplaceable, rows[2].Placement);
        Assert.NotEmpty(vm.ProblemText);
        var bad = vm.Cells.Single(c => c.X == 1 && c.Z == 2);
        Assert.True(bad.HasProblem);
        Assert.Contains(LocalizedStrings.Instance["Safari4_Unplaceable"], bad.AccessibleName);
        Assert.All(vm.Cells, c => Assert.False(string.IsNullOrWhiteSpace(c.AccessibleName)));
    }

    [AvaloniaFact]
    public void MenuAndLocatorExposeTheEditorOnlyForHeartGoldSoulSilver()
    {
        using var app = new HeadlessAppFixture();
        var action = Assert.Single(app.ViewModel.ToolMenuGroups.SelectMany(g => g.Items),
            e => ReferenceEquals(e.Command, app.ViewModel.OpenSafariZone4Command));
        Assert.False(action.IsAvailable);
        app.LoadSaveInstance(new SAV4HGSS());
        Assert.True(action.IsAvailable);
        Assert.IsType<SafariZone4Editor>(ViewLocator.Build(new SafariZone4EditorViewModel(new SAV4HGSS())));
        app.LoadSaveInstance(new SAV4Pt());
        Assert.False(action.IsAvailable);
        app.LoadSaveInstance(new SAV4DP());
        Assert.False(action.IsAvailable);
        app.LoadSaveInstance(new SAV5B2W2());
        Assert.False(action.IsAvailable);
    }

    [AvaloniaTheory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("ja")]
    public void CompactViewKeepsMapActionsAndNamedTilesReachable(string language)
    {
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        try
        {
            using var app = new HeadlessAppFixture();
            app.ViewModel.LanguageService.SetLanguage(language);
            var vm = new SafariZone4EditorViewModel(LoadHeartGold());
            app.Window.MinWidth = 0;
            app.Window.MinHeight = 0;
            app.Window.Width = 900;
            app.Window.Height = 600;
            var view = new SafariZone4Editor { DataContext = vm };
            app.Window.Content = view;
            app.Pump();

            var cells = view.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("safari-cell")).ToArray();
            Assert.Equal(SafariZoneMap4.WorldSize * SafariZoneMap4.WorldSize, cells.Length);
            Assert.All(cells, c => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(c))));

            var map = view.GetVisualDescendants().OfType<ItemsControl>().Single(i => i.Name == "MapGrid");
            var topLeft = map.TranslatePoint(new global::Avalonia.Point(0, 0), app.Window)!.Value;
            var bottomRight = map.TranslatePoint(new global::Avalonia.Point(map.Bounds.Width, map.Bounds.Height), app.Window)!.Value;
            Assert.True(bottomRight.X - topLeft.X > 200, $"Map rendered only {bottomRight.X - topLeft.X}px wide.");
            Assert.InRange(bottomRight.Y, 1, 600);

            foreach (var button in view.GetVisualDescendants().OfType<Button>().Where(b =>
                ReferenceEquals(b.Command, vm.SaveCommand) || ReferenceEquals(b.Command, vm.CancelCommand)
                || ReferenceEquals(b.Command, vm.ResetCommand) || ReferenceEquals(b.Command, vm.RemoveObjectCommand)))
            {
                Assert.True(button.IsEffectivelyVisible);
                var point = button.TranslatePoint(new global::Avalonia.Point(button.Bounds.Width, button.Bounds.Height), app.Window);
                Assert.NotNull(point);
                Assert.InRange(point.Value.Y, 1, 600);
                Assert.InRange(point.Value.X, 1, 900);
            }

            var list = view.GetVisualDescendants().OfType<ListBox>().Single();
            Assert.True(list.Bounds.Height > 60, $"Object list was only {list.Bounds.Height}px tall.");

            // Clicking a map tile runs the same command as the view model.
            var target = cells[(12 * SafariZoneMap4.WorldSize) + 12];
            target.Command!.Execute(target.CommandParameter);
            app.Pump();
            Assert.NotNull(vm.SelectedObject);

            var capture = Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1";
            var directory = Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") ?? string.Empty;
            if (capture)
            {
                Directory.CreateDirectory(directory);
                Assert.NotNull(app.CaptureFrame(Path.Combine(directory, $"gen4-safari-{language}.png")));
            }

            // The progress and linked-trainer tabs realize their fields and keep the footer reachable.
            var tabs = view.GetVisualDescendants().OfType<TabControl>().Single();
            for (int i = 1; i < tabs.ItemCount; i++)
            {
                tabs.SelectedIndex = i;
                app.Pump();
                var numbers = view.GetVisualDescendants().OfType<NumericUpDown>().Where(n => n.IsEffectivelyVisible).ToArray();
                Assert.NotEmpty(numbers);
                Assert.All(numbers, n => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(n))));
                if (capture)
                    Assert.NotNull(app.CaptureFrame(Path.Combine(directory, $"gen4-safari-{language}-tab{i}.png")));
            }
        }
        finally
        {
            new PKHeX.Application.Services.LanguageService().SetLanguage(previous);
            LocalizedStrings.Instance.SetLanguage(previous);
        }
    }

    [AvaloniaFact]
    public void LanguageChangeRefreshesNamesWithoutChangingStagedValues()
    {
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        try
        {
            using var app = new HeadlessAppFixture();
            app.ViewModel.LanguageService.SetLanguage("en");
            var vm = new SafariZone4EditorViewModel(LoadHeartGold());
            var area = vm.SelectedArea!;
            area.Map = (int)SafariZoneType4.Desert;
            var english = vm.Cells[0].AccessibleName;
            app.ViewModel.LanguageService.SetLanguage("de");
            WeakReferenceMessenger.Default.Send(new LanguageChangedMessage("de"));
            Assert.Equal((int)SafariZoneType4.Desert, area.Map);
            Assert.Contains("Wüste", area.Name);
            Assert.NotEqual(english, vm.Cells[0].AccessibleName);
        }
        finally
        {
            new PKHeX.Application.Services.LanguageService().SetLanguage(previous);
            LocalizedStrings.Instance.SetLanguage(previous);
        }
    }
}
