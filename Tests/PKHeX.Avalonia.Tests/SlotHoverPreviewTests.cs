using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Models;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

/// <summary>Rich box/party slot hover card (issue #244).</summary>
public class SlotHoverPreviewTests
{
    private static string LegalKingambitPath()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PKHeX.sln"))) root = root.Parent;
        Assert.NotNull(root);
        return Path.Combine(root.FullName, "Tests", "PKHeX.Core.Tests", "Legality", "Legal", "Generation 9", "FormArg", "0983 - Kingambit - 6BE42E9A9EF4 0 asKing.pk9");
    }

    private static (SAV9SV Save, PK9 Pokemon) LegalSave()
    {
        var pk = new PK9(File.ReadAllBytes(LegalKingambitPath()));
        var save = new SAV9SV { Version = pk.Version, OT = pk.OriginalTrainerName, ID32 = pk.ID32, Gender = pk.OriginalTrainerGender, Language = pk.Language };
        save.SetBoxSlotAtIndex(pk.Clone(), 0, 0, EntityImportSettings.None);
        return (save, pk);
    }

    private static (SAV6XY Save, PK6 Pokemon) IllegalSave()
    {
        var save = new SAV6XY();
        // Budew cannot learn Hydro Pump, and this PID/encounter combination is not legal.
        var pk = new PK6 { Species = (ushort)Species.Budew, Nickname = "Budew", PID = 0x12345678, CurrentLevel = 5, Ball = (byte)Ball.Poke, Move1 = (ushort)Move.Absorb, Move2 = (ushort)Move.HydroPump };
        save.SetBoxSlotAtIndex(pk, 0, 0, EntityImportSettings.None);
        save.SetPartySlotAtIndex(pk, 0, EntityImportSettings.None);
        return (save, pk);
    }

    private static AvaloniaSpriteRenderer Renderer(SaveFile save)
    {
        var renderer = new AvaloniaSpriteRenderer(new AppSettings());
        renderer.Initialize(save);
        return renderer;
    }

    [Fact]
    public void Gen3HeldItem_UsesGen3ItemTable_InPreviewAndSlotLabels()
    {
        // Issue #554: Gen 3 stores its own item indexes. 139 is Oran Berry in Gen 3
        // but a different item in the modern table.
        const ushort oranBerryGen3 = 139;
        var save = new SAV3E();
        var pk = new PK3 { Species = (ushort)Species.Zigzagoon, PID = 0x12345678, CurrentLevel = 5, Ball = (byte)Ball.Poke, Move1 = (ushort)Move.Tackle, HeldItem = oranBerryGen3, Version = GameVersion.E };
        save.SetBoxSlotAtIndex(pk, 0, 0, EntityImportSettings.None);

        var expected = GameInfo.Strings.GetItemStrings(EntityContext.Gen3, GameVersion.E)[oranBerryGen3];
        Assert.NotEqual(GameInfo.Strings.Item[oranBerryGen3], expected);

        var vm = new BoxViewerViewModel(save, Renderer(save));
        var preview = vm.GetSlotPreview(vm.Slots[0].Location);
        Assert.NotNull(preview);
        Assert.Equal(expected, preview.HeldItemText);
        Assert.Equal(expected, vm.Slots[0].HeldItemName);
        Assert.Equal(expected, StringResourceLookup.HeldItem(save.GetBoxSlotAtIndex(0, 0)));
    }

    [Fact]
    public void LegalSlot_PreviewShowsBallMovesWithTypeIconsAndEncounter()
    {
        var (save, pk) = LegalSave();
        var vm = new BoxViewerViewModel(save, Renderer(save));
        var preview = vm.GetSlotPreview(vm.Slots[0].Location);

        Assert.NotNull(preview);
        Assert.True(preview.IsLegal, preview.LegalityHint);
        Assert.Equal(LocalizedStringsValue("SlotPreview_Legal"), preview.LegalityText);
        Assert.Equal(pk.Nickname, preview.Title);
        Assert.NotNull(preview.BallSprite);
        Assert.NotEmpty(preview.BallName);
        Assert.NotNull(preview.Sprite);
        Assert.Equal(pk.Moves.Count(m => m != 0), preview.Moves.Count);
        Assert.All(preview.Moves, m => { Assert.NotNull(m.TypeIcon); Assert.NotEmpty(m.TypeName); Assert.False(m.IsIllegal); });
        Assert.True(preview.HasEncounter);
        Assert.Contains(preview.Title, preview.AccessibleText);
        Assert.Contains(preview.Moves[0].Name, preview.AccessibleText);
        Assert.Contains(preview.EncounterText, preview.AccessibleText);
    }

    [Fact]
    public void IllegalSlot_PreviewMarksIllegalMoveAndLegalityHint()
    {
        var (save, _) = IllegalSave();
        var box = new BoxViewerViewModel(save, Renderer(save));
        var party = new PartyViewerViewModel(save, Renderer(save));

        foreach (var preview in new[] { box.GetSlotPreview(box.Slots[0].Location), party.GetSlotPreview(party.Slots[0].Location) })
        {
            Assert.NotNull(preview);
            Assert.True(preview.IsIllegal);
            Assert.Equal(LocalizedStringsValue("SlotPreview_Illegal"), preview.LegalityText);
            Assert.True(preview.HasLegalityHint);
            var hydroPump = Assert.Single(preview.Moves, m => m.Name == GameInfo.Strings.movelist[(int)Move.HydroPump]);
            Assert.True(hydroPump.IsIllegal);
            Assert.Contains(LocalizedStringsValue("SlotPreview_Illegal"), preview.AccessibleText);
            Assert.Contains(preview.LegalityHint.Split(Environment.NewLine)[0], preview.AccessibleText);
        }
    }

    [Fact]
    public void EmptySlot_HasNoPreview()
    {
        var save = new SAV6XY();
        var box = new BoxViewerViewModel(save, Renderer(save));
        var party = new PartyViewerViewModel(save, Renderer(save));
        Assert.Null(box.GetSlotPreview(box.Slots[3].Location));
        Assert.Null(party.GetSlotPreview(party.Slots[0].Location));
    }

    [Fact]
    public void HaXMode_PreviewMakesNoLegalityClaim()
    {
        var (save, _) = IllegalSave();
        var box = new BoxViewerViewModel(save, Renderer(save), haXMode: true);
        var preview = box.GetSlotPreview(box.Slots[0].Location);
        Assert.NotNull(preview);
        Assert.True(preview.IsNotChecked);
        Assert.False(preview.HasLegalityHint);
        Assert.All(preview.Moves, m => Assert.False(m.IsIllegal));
        Assert.False(preview.HasEncounter);
    }

    [Fact]
    public void EncounterDetails_FollowTheSetting()
    {
        var (save, _) = LegalSave();
        var box = new BoxViewerViewModel(save, Renderer(save)) { HoverSettings = new AppSettings.SlotHoverSettings { ShowEncounter = false } };
        var preview = box.GetSlotPreview(box.Slots[0].Location);
        Assert.NotNull(preview);
        Assert.False(preview.HasEncounter);
        Assert.False(preview.HasEncounterLocation);
    }

    [Fact]
    public void HoverSettings_PersistThroughTheSettingsStore()
    {
        var settings = new AppSettings();
        settings.SlotHover.Enabled = false;
        settings.SlotHover.DelayMs = 700;
        settings.SlotHover.ShowEncounter = false;
        var json = System.Text.Json.JsonSerializer.Serialize(settings);
        var copy = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json)!;
        Assert.False(copy.SlotHover.Enabled);
        Assert.Equal(700, copy.SlotHover.DelayMs);
        Assert.False(copy.SlotHover.ShowEncounter);

        // A settings file from before this feature loads with the card on.
        var old = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{}")!;
        Assert.True(old.SlotHover.Enabled);
        Assert.Equal(AppSettings.SlotHoverSettings.DefaultDelayMs, old.SlotHover.DelayMs);
    }

    [AvaloniaFact]
    public void Hover_OpensRichCardWithoutTakingFocus()
    {
        var (save, _) = LegalSave();
        var vm = new BoxViewerViewModel(save, Renderer(save));
        var (window, view) = Show(new BoxViewer { DataContext = vm }, vm);
        try
        {
            var slot = SlotButton(view, 0);
            var empty = SlotButton(view, 5);
            view.Focus();
            var focused = window.FocusManager!.GetFocusedElement();
            Assert.Equal(vm.HoverSettings.DelayMs, ToolTip.GetShowDelay(slot));

            ToolTip.SetIsOpen(slot, true);
            Dispatcher.UIThread.RunJobs();
            Assert.True(ToolTip.GetIsOpen(slot));
            var tip = Assert.IsType<ToolTip>(ToolTip.GetTip(slot));
            var card = Assert.IsType<SlotPreviewCard>(tip.Content);
            var preview = Assert.IsType<SlotPreviewViewModel>(card.DataContext);
            Assert.True(preview.IsLegal);
            Assert.True(card.IsAttachedToVisualTree());
            Assert.False(card.Focusable);
            Assert.Same(focused, window.FocusManager.GetFocusedElement());
            ToolTip.SetIsOpen(slot, false);

            // Empty slot: no card, only the plain text tip.
            ToolTip.SetIsOpen(empty, true);
            Dispatcher.UIThread.RunJobs();
            Assert.IsType<string>(ToolTip.GetTip(empty));
            ToolTip.SetIsOpen(empty, false);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SettingOff_KeepsThePlainTextTooltip()
    {
        var (save, _) = LegalSave();
        var settings = new AppSettings.SlotHoverSettings { Enabled = false, DelayMs = 1000 };
        var vm = new BoxViewerViewModel(save, Renderer(save)) { HoverSettings = settings };
        var (window, view) = Show(new BoxViewer { DataContext = vm }, vm);
        try
        {
            var slot = SlotButton(view, 0);
            Assert.Equal(1000, ToolTip.GetShowDelay(slot));
            ToolTip.SetIsOpen(slot, true);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(vm.Slots[0].ToolTipSummary, ToolTip.GetTip(slot));
            ToolTip.SetIsOpen(slot, false);

            // The live settings object is read on each open: turning it on applies at once.
            settings.Enabled = true;
            ToolTip.SetIsOpen(slot, true);
            Dispatcher.UIThread.RunJobs();
            Assert.IsType<SlotPreviewCard>(Assert.IsType<ToolTip>(ToolTip.GetTip(slot)).Content);
            ToolTip.SetIsOpen(slot, false);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void KeyboardFocus_OpensTheSameCardAndScreenReadersGetItsText()
    {
        var (save, _) = IllegalSave();
        var vm = new PartyViewerViewModel(save, Renderer(save));
        var (window, view) = Show(new PartyViewer { DataContext = vm }, vm);
        try
        {
            var slot = view.GetVisualDescendants().OfType<Button>().First(b => b.Tag is PartySlotData { Slot: 0 });
            Assert.Equal(vm.Slots[0].ToolTipSummary, AutomationProperties.GetHelpText(slot));

            slot.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(slot, window.FocusManager!.GetFocusedElement());
            Assert.True(ToolTip.GetIsOpen(slot));
            Assert.Equal(PlacementMode.Bottom, ToolTip.GetPlacement(slot));
            var card = Assert.IsType<SlotPreviewCard>(Assert.IsType<ToolTip>(ToolTip.GetTip(slot)).Content);
            var preview = Assert.IsType<SlotPreviewViewModel>(card.DataContext);
            Assert.True(preview.IsIllegal);
            Assert.Equal(preview.AccessibleText, AutomationProperties.GetHelpText(slot));
            // The popup must not take focus from the slot.
            Assert.Same(slot, window.FocusManager.GetFocusedElement());

            view.Focus();
            Dispatcher.UIThread.RunJobs();
            Assert.False(ToolTip.GetIsOpen(slot));
            Assert.Equal(PlacementMode.Pointer, ToolTip.GetPlacement(slot));
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(PlacementMode.Bottom)]
    [InlineData(PlacementMode.Pointer)]
    public void CardStaysOnScreenAtTheBottomRightEdge(PlacementMode placement)
    {
        var (save, _) = LegalSave();
        var last = save.BoxSlotCount - 1;
        save.SetBoxSlotAtIndex(save.GetBoxSlotAtIndex(0, 0), 0, last, EntityImportSettings.None);
        var vm = new BoxViewerViewModel(save, Renderer(save));
        var view = new BoxViewer { DataContext = vm };
        var window = new Window { Content = view, Width = 420, Height = 360 };
        try
        {
            window.Show();
            var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
            Assert.NotNull(screen);
            var area = screen.Bounds;
            window.Position = new PixelPoint(area.Right - 420, area.Bottom - 360);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var slot = SlotButton(view, last);
            ToolTip.SetPlacement(slot, placement);
            ToolTip.SetIsOpen(slot, true);
            Dispatcher.UIThread.RunJobs();
            var card = Assert.IsType<SlotPreviewCard>(Assert.IsType<ToolTip>(ToolTip.GetTip(slot)).Content);
            card.UpdateLayout();
            Assert.True(card.Bounds.Width > 0 && card.Bounds.Height > 0);
            var topLeft = card.PointToScreen(new Point(0, 0));
            var bottomRight = card.PointToScreen(new Point(card.Bounds.Width, card.Bounds.Height));
            var slotBottomRight = slot.PointToScreen(new Point(slot.Bounds.Width, slot.Bounds.Height));
            // Without the positioner's constraint the card would start below/right of the slot and run off-screen.
            Assert.True(slotBottomRight.Y + card.Bounds.Height > area.Bottom || slotBottomRight.X + card.Bounds.Width > area.Right);
            Assert.InRange(topLeft.X, area.X, area.Right);
            Assert.InRange(topLeft.Y, area.Y, area.Bottom);
            Assert.InRange(bottomRight.X, area.X, area.Right);
            Assert.InRange(bottomRight.Y, area.Y, area.Bottom);
            ToolTip.SetIsOpen(slot, false);
        }
        finally { window.Close(); }
    }

    [Fact]
    public void SettingsScreen_LoadsAndSavesTheHoverSettings()
    {
        var settings = new AppSettings();
        var store = new FakeSettingsStore();
        var vm = CreateSettings(settings, store);
        Assert.True(vm.SlotPreviewEnabled);
        Assert.Equal(AppSettings.SlotHoverSettings.DefaultDelayMs, vm.SlotPreviewDelayMs);
        Assert.True(vm.SlotPreviewShowEncounter);
        Assert.Equal(AppSettings.SlotHoverSettings.DelayChoices, vm.SlotPreviewDelays.Select(c => c.Value));

        vm.SlotPreviewEnabled = false;
        vm.SlotPreviewDelayMs = 1000;
        vm.SlotPreviewShowEncounter = false;
        vm.SaveCommand.Execute(null);

        Assert.Same(settings, store.Saved);
        Assert.False(settings.SlotHover.Enabled);
        Assert.Equal(1000, settings.SlotHover.DelayMs);
        Assert.False(settings.SlotHover.ShowEncounter);
    }

    [AvaloniaFact]
    public void SettingsScreen_ShowsTheHoverSection()
    {
        var vm = CreateSettings(new AppSettings(), new FakeSettingsStore());
        var view = new SettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 480, Height = 760 };
        using var lifetime = new HeadlessWindowLifetime(window);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var enabled = view.FindControl<ToggleSwitch>("SlotPreviewEnabledSwitch")!;
        var delay = view.FindControl<ComboBox>("SlotPreviewDelayComboBox")!;
        var encounter = view.FindControl<ToggleSwitch>("SlotPreviewEncounterSwitch")!;
        Assert.Equal(LocalizedStringsValue("Settings_SlotPreviewEnabled"), AutomationProperties.GetName(enabled));
        Assert.Equal(LocalizedStringsValue("Settings_SlotPreviewDelay"), AutomationProperties.GetName(delay));
        Assert.Equal(LocalizedStringsValue("Settings_SlotPreviewEncounter"), AutomationProperties.GetName(encounter));
        Assert.Equal(AppSettings.SlotHoverSettings.DefaultDelayMs, delay.SelectedValue);

        delay.SelectedValue = 200;
        Assert.Equal(200, vm.SlotPreviewDelayMs);
        enabled.IsChecked = false;
        Assert.False(delay.IsEffectivelyEnabled);
        Assert.False(encounter.IsEffectivelyEnabled);
    }

    private static SettingsViewModel CreateSettings(AppSettings settings, FakeSettingsStore store) => new(
        settings,
        store,
        new ThemeService(settings, new FakeSettingsStore()),
        new UiDensityService(settings, new FakeSettingsStore()),
        new LanguageService(),
        UpdateTestDoubles.Coordinator(),
        isLinux: false);

    private static string LocalizedStringsValue(string key) => PKHeX.Presentation.Localization.LocalizedStrings.Instance[key];

    private static (Window Window, T View) Show<T>(T view, object _) where T : Control
    {
        var window = new Window { Content = view, Width = 520, Height = 420 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (window, view);
    }

    private static Button SlotButton(Control view, int slot)
        => view.GetVisualDescendants().OfType<Button>().First(b => b.Tag is SlotData s && s.Slot == slot);
}
