using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PKHeX.Application.Services;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public sealed class IssueSweepRegressionTests
{
    [AvaloniaFact]
    public void SecretBase6_ComposesWithProductionBooleanResource()
    {
        var viewModel = new SecretBase6EditorViewModel(new SAV6AO());
        var view = Assert.IsType<SecretBase6Editor>(PKHeX.Avalonia.ViewLocator.Build(viewModel));
        var window = Show(view, 900, 700);
        try
        {
            Assert.NotEmpty(view.GetVisualDescendants().OfType<Button>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Misc7_BattleTreeRowsHaveNonOverlappingMeasuredBounds()
    {
        var view = new Misc7Editor
        {
            DataContext = new Misc7EditorViewModel(new SAV7SM()),
        };
        var window = Show(view, 920, 700);
        try
        {
            var streaks = view.GetVisualDescendants()
                .OfType<NumericUpDown>()
                .Where(control => AutomationProperties.GetName(control)?.Contains("Streak", StringComparison.Ordinal) == true)
                .ToArray();

            Assert.Equal(12, streaks.Length);
            Assert.All(streaks, control => Assert.True(control.Bounds.Height >= 30, $"{AutomationProperties.GetName(control)} measured at {control.Bounds.Height}px."));

            foreach (var group in streaks.GroupBy(control => AutomationProperties.GetName(control)!.StartsWith("Super ", StringComparison.Ordinal)))
            {
                var yPositions = group
                    .Select(control => control.TranslatePoint(default, view)!.Value.Y)
                    .OrderBy(y => y)
                    .ToArray();

                Assert.Equal(6, yPositions.Length);
                var distinctRows = yPositions
                    .Select(y => Math.Round(y, 1))
                    .Distinct()
                    .OrderBy(y => y)
                    .ToArray();
                Assert.Equal(3, distinctRows.Length);
                for (var i = 1; i < distinctRows.Length; i++)
                    Assert.True(distinctRows[i] - distinctRows[i - 1] >= 20, $"Battle Tree rows overlap at Y={distinctRows[i - 1]} and Y={distinctRows[i]}.");
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PokeBlockAndPoffinCaseGridsReserveReadableHeaderWidths()
    {
        var pokeBlockView = new PokeBlock3CaseEditorView
        {
            DataContext = new PokeBlock3CaseEditorViewModel(new SAV3E()),
        };
        var pokeBlockWindow = Show(pokeBlockView, 800, 500);
        try
        {
            var pokeBlockGrid = Assert.Single(pokeBlockView.GetVisualDescendants().OfType<DataGrid>());
            Assert.Equal(9, pokeBlockGrid.Columns.Count);
            Assert.All(pokeBlockGrid.Columns, column => Assert.True(column.Width.IsSizeToHeader || column.Width.Value >= 72, $"Pokéblock column does not reserve a safe header width: {column.Width}."));
        }
        finally
        {
            pokeBlockWindow.Close();
        }

        var poffinView = new PoffinCaseEditorView
        {
            DataContext = new PoffinCaseEditorViewModel(new SAV4DP()),
        };
        var poffinWindow = Show(poffinView, 800, 500);
        try
        {
            var poffinGrid = Assert.Single(poffinView.GetVisualDescendants().OfType<DataGrid>());
            Assert.Equal(9, poffinGrid.Columns.Count);
            Assert.All(poffinGrid.Columns, column => Assert.True(column.Width.IsSizeToHeader || column.Width.Value >= 72, $"Poffin column does not reserve a safe header width: {column.Width}."));
        }
        finally
        {
            poffinWindow.Close();
        }
    }

    [Fact]
    public void BoxViewer_UsesAndPersistsStoredActiveBoxAcrossSaveFamilies()
    {
        var early = new SAV3E { CurrentBox = 2 };
        var earlyViewer = new BoxViewerViewModel(early, new NullSpriteRenderer());
        Assert.Equal(2, earlyViewer.CurrentBox);
        earlyViewer.NextBoxCommand.Execute(null);
        Assert.Equal(3, early.CurrentBox);

        var scBlock = new SAV8SWSH();
        var currentBoxBlock = scBlock.Blocks.GetBlock(SaveBlockAccessor8SWSH.KCurrentBox);
        currentBoxBlock.ChangeStoredType(SCTypeCode.Byte);
        currentBoxBlock.SetValue((byte)5);
        scBlock.CurrentBox = 5;
        var scBlockViewer = new BoxViewerViewModel(scBlock, new NullSpriteRenderer());
        Assert.Equal(5, scBlockViewer.CurrentBox);
        scBlockViewer.NextBoxCommand.Execute(null);
        Assert.Equal(6, scBlock.CurrentBox);

        scBlock.CurrentBox = scBlock.BoxCount + 10;
        var clampedViewer = new BoxViewerViewModel(scBlock, new NullSpriteRenderer());
        Assert.Equal(0, clampedViewer.CurrentBox);
        Assert.Equal(0, scBlock.CurrentBox);
    }

    [Fact]
    public void TrainerEditor_UsesCountSemanticsForSwordShieldBadges()
    {
        var save = new SAV8SWSH { Badges = 8 };
        var viewModel = new TrainerEditorViewModel(save);
        var unchanged = save.Data.ToArray();

        Assert.True(viewModel.HasBadges);
        Assert.True(viewModel.IsBadgeCount);
        Assert.Empty(viewModel.Badges);
        Assert.Equal(8, viewModel.BadgeCount);

        viewModel.SaveCommand.Execute(null);
        Assert.Equal(unchanged, save.Data.ToArray());

        for (var count = 0; count <= viewModel.MaxBadgeCount; count++)
        {
            viewModel.BadgeCount = count;
            viewModel.SaveCommand.Execute(null);
            Assert.Equal(count, save.Badges);
        }
    }

    [Fact]
    public void TrainerEditor_UsesGenerationAwareDisplayIds()
    {
        var modern = new SAV8SWSH { ID32 = 2_287_321_660 };
        var modernViewModel = new TrainerEditorViewModel(modern);

        Assert.Equal(321_660u, modernViewModel.DisplayTid);
        Assert.Equal(2_287u, modernViewModel.DisplaySid);
        Assert.Equal(999_999u, modernViewModel.MaxDisplayTid);
        Assert.Equal(4_294u, modernViewModel.MaxDisplaySid);

        modernViewModel.DisplayTid = 654_321;
        modernViewModel.DisplaySid = 1_234;
        modernViewModel.SaveCommand.Execute(null);
        Assert.Equal(654_321u, modern.DisplayTID);
        Assert.Equal(1_234u, modern.DisplaySID);

        var legacy = new SAV3E { TID16 = 12_345, SID16 = 54_321 };
        var legacyViewModel = new TrainerEditorViewModel(legacy);
        Assert.Equal(12_345u, legacyViewModel.DisplayTid);
        Assert.Equal(54_321u, legacyViewModel.DisplaySid);
        Assert.Equal(65_535u, legacyViewModel.MaxDisplayTid);
        Assert.Equal(65_535u, legacyViewModel.MaxDisplaySid);

        foreach (var additional in new SaveFile[] { new SAV7SM(), new SAV8LA(), new SAV9SV(), new SAV9ZA() })
        {
            additional.ID32 = 2_287_321_660;
            var additionalViewModel = new TrainerEditorViewModel(additional);
            Assert.Equal(321_660u, additionalViewModel.DisplayTid);
            Assert.Equal(2_287u, additionalViewModel.DisplaySid);
        }
    }

    [Fact]
    public void TrainerEditor_RejectsAnUnrepresentableModernIdPair()
    {
        var save = new SAV8SWSH { ID32 = 0 };
        var viewModel = new TrainerEditorViewModel(save)
        {
            DisplayTid = 999_999,
            DisplaySid = 4_294,
        };

        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.Equal(0u, save.ID32);
    }

    [Fact]
    public void AppSettings_InitializeCoreAppliesPersistedCoreGroups()
    {
        var settings = new AppSettings
        {
            SlotWrite = { SetUpdateDex = false, SetUpdatePKM = false, SetUpdateRecords = false },
            Import = { ApplyMarkings = false, ApplyStatAlignment = true },
            Legality = new LegalitySettings(),
        };
        settings.SaveLanguage.OverrideGen1.Version = GameVersion.YW;
        settings.SaveLanguage.OverrideGen1.Language = LanguageID.German;

        try
        {
            settings.InitializeCore();

            var slotSettings = SaveFile.SetUpdateSettings;
            Assert.Equal(EntityImportOption.Disable, slotSettings.UpdateToSaveFile);
            Assert.Equal(EntityImportOption.Disable, slotSettings.UpdatePokeDex);
            Assert.Equal(EntityImportOption.Disable, slotSettings.UpdateRecord);
            Assert.False(CommonEdits.ShowdownSetIVMarkings);
            Assert.True(CommonEdits.ShowdownSetBehaviorNature);
            Assert.Same(settings.Legality, ParseSettings.Settings);
            Assert.Equal(LanguageID.German, SaveLanguage.OverrideLanguageGen1);
            Assert.Equal(GameVersion.YW, SaveLanguage.OverrideVersionGen1);
        }
        finally
        {
            new AppSettings().InitializeCore();
        }
    }

    [Fact]
    public void HallOfFameAndMailStatesAreExplicit()
    {
        var oldLanguage = LocalizedStrings.Instance.CurrentLanguage;
        LocalizedStrings.Instance.SetLanguage("en");
        try
        {
            Assert.Equal("01 (Empty)", new HallOfFameTeam1ViewModel(0, 0).DisplayText);
            Assert.Equal("01 (3/6)", new HallOfFameTeam1ViewModel(0, 3).DisplayText);
            Assert.Equal("01 (Complete)", new HallOfFameTeam1ViewModel(0, 6).DisplayText);

            var emptyMail = new MailEntryViewModel(0, new Mail2(new SAV2(), 0), isParty: true);
            Assert.Contains("(empty)", emptyMail.DisplayText, StringComparison.Ordinal);

            var occupiedDetail = new Mail2(new SAV2(), 0)
            {
                MailType = 0x9E,
                AuthorName = "Ash",
            };
            var occupiedMail = new MailEntryViewModel(1, occupiedDetail, isParty: false);
            Assert.Contains("From Ash", occupiedMail.DisplayText, StringComparison.Ordinal);
        }
        finally
        {
            LocalizedStrings.Instance.SetLanguage(oldLanguage);
        }
    }

    [AvaloniaFact]
    public void HallOfFame7_CancelLeavesSaveUntouched_AndSaveCommitsClone()
    {
        var save = new SAV7SM();
        var before = save.Data.ToArray();
        var cancelled = false;
        var viewModel = new HallOfFame7EditorViewModel(save) { CloseRequested = () => cancelled = true };

        Assert.True(viewModel.IsSupported);
        viewModel.FirstEntries[0].SelectedSpecies = 25;
        viewModel.CancelCommand.Execute(null);

        Assert.True(cancelled);
        Assert.Equal(before, save.Data.ToArray());

        var committed = new HallOfFame7EditorViewModel(save);
        committed.CurrentEntries[0].SelectedSpecies = 25;
        committed.SaveCommand.Execute(null);

        Assert.NotEqual(before, save.Data.ToArray());
    }

    [AvaloniaFact]
    public void HallOfFame7_USUM_CancelPreservesStarterAndActionsExposeKeyboardSemantics()
    {
        var save = new SAV7USUM();
        var before = save.Data.ToArray();
        var viewModel = new HallOfFame7EditorViewModel(save);
        viewModel.FirstEntries[0].SelectedSpecies = 25;
        viewModel.CurrentEntries[0].SelectedSpecies = 6;
        viewModel.StarterEc = "DEADBEEF";
        viewModel.CancelCommand.Execute(null);

        Assert.Equal(before, save.Data.ToArray());

        var view = new HallOfFame7Editor { DataContext = new HallOfFame7EditorViewModel(save) };
        var window = Show(view, 900, 700);
        try
        {
            var actions = view.GetVisualDescendants().OfType<Button>().ToArray();
            Assert.Contains(actions, button => button.IsCancel);
            Assert.Contains(actions, button => button.IsDefault);
        }
        finally
        {
            window.Close();
        }

        var committedSave = new SAV7USUM();
        var committedViewModel = new HallOfFame7EditorViewModel(committedSave);
        var committedBefore = committedSave.Data.ToArray();
        committedViewModel.FirstEntries[0].SelectedSpecies = 25;
        committedViewModel.CurrentEntries[0].SelectedSpecies = 6;
        committedViewModel.StarterEc = "DEADBEEF";
        committedViewModel.SaveCommand.Execute(null);
        Assert.NotEqual(committedBefore, committedSave.Data.ToArray());
    }

    [Fact]
    public void EmptyRecordCommandsAreDisabled()
    {
        var hallOfFame = new HallOfFame1EditorViewModel(new SAV1());
        hallOfFame.ClearAllCommand.Execute(null);
        hallOfFame.SelectedTeam = hallOfFame.Teams[1];
        Assert.False(hallOfFame.DeleteTeamCommand.CanExecute(null));

        var mail = new MailBoxEditorViewModel(new SAV2());
        mail.SelectedMail = mail.PartyMail[0];
        Assert.False(mail.DeleteMailCommand.CanExecute(null));

        var detail = new Mail2(new SAV2(), 0) { MailType = 0x9E, AuthorName = "Ash" };
        var occupied = new MailEntryViewModel(0, detail, isParty: true);
        mail.SelectedMail = null;
        mail.PartyMail[0] = occupied;
        mail.SelectedMail = occupied;
        Assert.True(mail.DeleteMailCommand.CanExecute(null));
    }

    [Fact]
    public void Gen3HallOfFameExposesAnExplicitEmptyState()
    {
        var viewModel = new HallOfFame3EditorViewModel(new SAV3E());
        Assert.False(viewModel.HasEntries);
    }

    [AvaloniaFact]
    public void Gen4MenuDoesNotAdvertiseUnsupportedRoamerEditor()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV4DP());

        var gen4 = app.ViewModel.ToolMenuGroups.Single(group => group.Title == LocalizedStrings.Instance["Menu_Gen4"]);
        Assert.DoesNotContain(gen4.Items, item => item.Title == LocalizedStrings.Instance["Menu_Roamer"]);
    }

    [AvaloniaFact]
    public void CaptureIssueSweepViews_WhenEnabled_WritesPng()
    {
        if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") != "1")
            return;

        var directory = Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR")
            ?? Path.Combine(Path.GetTempPath(), "pkhex-headless-frames");
        Directory.CreateDirectory(directory);

        Capture(new PokeBlock3CaseEditorView { DataContext = new PokeBlock3CaseEditorViewModel(new SAV3E()) }, "issue-pokeblock-case.png", 1000, 500, directory);
        Capture(new PoffinCaseEditorView { DataContext = new PoffinCaseEditorViewModel(new SAV4DP()) }, "issue-poffin-case.png", 1000, 500, directory);
        Capture(new SecretBase6Editor { DataContext = new SecretBase6EditorViewModel(new SAV6AO()) }, "issue-secret-base6.png", 900, 700, directory);
        Capture(new HallOfFame3EditorView { DataContext = new HallOfFame3EditorViewModel(new SAV3E()) }, "issue-hall-of-fame3-empty.png", 600, 500, directory);
        Capture(new HallOfFame7Editor { DataContext = new HallOfFame7EditorViewModel(new SAV7SM()) }, "issue-hall-of-fame7.png", 900, 700, directory);
        Capture(new TrainerEditor { DataContext = new TrainerEditorViewModel(new SAV8SWSH()) }, "issue-trainer-swsh.png", 900, 700, directory);
    }

    private static Window Show(Control content, double width, double height)
    {
        var window = new Window { Content = content, Width = width, Height = height };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Capture(Control content, string fileName, double width, double height, string directory)
    {
        var window = new Window { Content = content, Width = width, Height = height };
        window.Show();
        try
        {
            for (var i = 0; i < 10; i++)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }

            var frame = window.GetLastRenderedFrame();
            if (frame is null)
                return;

            var path = Path.Combine(directory, fileName);
            using var stream = File.Create(path);
            frame.Save(stream);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class NullSpriteRenderer : PKHeX.Application.Abstractions.ISpriteRenderer
    {
        public byte[]? GetSprite(PKM pk, bool isEgg = false) => null;
        public byte[]? GetSprite(ushort species, byte form, byte gender, uint formarg, bool shiny, EntityContext context) => null;
        public byte[]? GetItemSprite(int itemId) => null;
        public byte[]? GetEmptySlot() => null;
        public void Initialize(SaveFile sav) { }
    }
}
