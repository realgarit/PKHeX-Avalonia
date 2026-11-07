using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Battle Box (X/Y, OR/AS) and Battle Team (S/M, US/UM, Sword/Shield) unlock in the trainer
/// workspace: the unlock is staged, Apply writes it and refreshes slots, Undo/Reset discard it,
/// and a no-op Apply keeps every byte. The public fixtures have no locks, so the lock state is
/// synthesized through the public Core APIs before the workspace opens.
/// </summary>
public class BattleTeamUnlockTests
{
    public static TheoryData<string> Fixtures => new()
    {
        "gen6_x.main", "gen6_or.main", "gen7_sun.main", "gen7_ultrasun.main", "gen8_sword_ct_public.main",
    };

    private static SaveFile Load(string file) =>
        PKHeX.Avalonia.Tests.Fixtures.SaveFileFixture.LoadSave(Path.Combine(PKHeX.Avalonia.Tests.Fixtures.SaveFileFixture.FindSaveFilesPath()!, file))!;

    private static void Lock(SaveFile save)
    {
        switch (save)
        {
            case ISaveBlock6Main gen6: gen6.BattleBox.LockedWiFiTournament = true; gen6.BattleBox.LockedLiveTournament = true; break;
            case SAV7 gen7: gen7.BoxLayout.SetIsTeamLocked(0, true); gen7.BoxLayout.SetIsTeamLocked(3, true); break;
            case SAV8SWSH swsh: swsh.TeamIndexes.SetIsTeamLocked(1, true); swsh.TeamIndexes.SetIsTeamLocked(5, true); break;
            default: throw new InvalidOperationException(save.GetType().Name);
        }
    }

    private static bool IsLocked(SaveFile save) => save switch
    {
        ISaveBlock6Main gen6 => gen6.BattleBox.Locked,
        SAV7 gen7 => gen7.BoxLayout.IsAnyTeamLocked(),
        SAV8SWSH swsh => swsh.TeamIndexes.IsAnyTeamLocked(),
        _ => throw new InvalidOperationException(save.GetType().Name),
    };

    private static bool VmLocked(TrainerEditorViewModel vm) =>
        vm.Gen7Editor?.IsBattleTeamLocked ?? vm.SwshEditor?.IsBattleTeamLocked ?? vm.IsBattleBoxLocked;

    private static void Unlock(TrainerEditorViewModel vm)
    {
        if (vm.Gen7Editor is { } gen7) { Assert.True(gen7.UnlockBattleTeamsCommand.CanExecute(null)); gen7.UnlockBattleTeamsCommand.Execute(null); }
        else if (vm.SwshEditor is { } swsh) { Assert.True(swsh.UnlockBattleTeamsCommand.CanExecute(null)); swsh.UnlockBattleTeamsCommand.Execute(null); }
        else { Assert.True(vm.HasBattleBox); Assert.True(vm.UnlockBattleBoxCommand.CanExecute(null)); vm.UnlockBattleBoxCommand.Execute(null); }
    }

    private static void Undo(TrainerEditorViewModel vm)
    {
        if (vm.Gen7Editor is { } gen7) gen7.UndoFashionCommand.Execute(null);
        else if (vm.SwshEditor is { } swsh) swsh.UndoCommand.Execute(null);
        else vm.UndoBattleBoxCommand.Execute(null);
    }

    private static bool CanUnlock(TrainerEditorViewModel vm) =>
        vm.Gen7Editor?.UnlockBattleTeamsCommand.CanExecute(null)
        ?? vm.SwshEditor?.UnlockBattleTeamsCommand.CanExecute(null)
        ?? vm.UnlockBattleBoxCommand.CanExecute(null);

    private static byte[] Bytes(SaveFile save) => save is SAV8SWSH swsh
        ? swsh.AllBlocks.SelectMany(block => block.Data.ToArray()).ToArray()
        : save.Data.ToArray();

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void UnlockStagesUndoesAndApplyWritesAndRefreshesSlots(string file)
    {
        var save = Load(file);
        Lock(save);
        var locked = Bytes(save);
        using var vm = new TrainerEditorViewModel(save);
        int refreshes = 0; vm.SaveSlotsChanged += (_, _) => refreshes++;
        Assert.True(VmLocked(vm));

        Unlock(vm);
        Assert.False(VmLocked(vm));
        Assert.False(CanUnlock(vm));
        Assert.True(IsLocked(save));
        Assert.Equal(locked, Bytes(save));

        Undo(vm);
        Assert.True(VmLocked(vm));
        Assert.Equal(locked, Bytes(save));

        Unlock(vm);
        vm.SaveCommand.Execute(null);
        Assert.False(IsLocked(save));
        Assert.Equal(1, refreshes);
        Assert.False(VmLocked(vm));
        Assert.False(CanUnlock(vm));

        var reopened = SaveUtil.GetSaveFile(save.Write());
        Assert.NotNull(reopened);
        Assert.False(IsLocked(reopened));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void ResetDiscardsStagedUnlock(string file)
    {
        var save = Load(file);
        Lock(save);
        var locked = Bytes(save);
        using var vm = new TrainerEditorViewModel(save);
        int refreshes = 0; vm.SaveSlotsChanged += (_, _) => refreshes++;

        Unlock(vm);
        vm.ResetCommand.Execute(null);
        Assert.True(VmLocked(vm));
        vm.SaveCommand.Execute(null);

        Assert.True(IsLocked(save));
        Assert.Equal(locked, Bytes(save));
        Assert.Equal(0, refreshes);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void NoOpApplyKeepsEveryByteAndDoesNotRefreshSlots(string file)
    {
        var save = Load(file);
        Lock(save);
        var before = Bytes(save);
        using var vm = new TrainerEditorViewModel(save);
        int refreshes = 0; vm.SaveSlotsChanged += (_, _) => refreshes++;

        vm.SaveCommand.Execute(null);

        Assert.Equal(before, Bytes(save));
        Assert.True(IsLocked(save));
        Assert.Equal(0, refreshes);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void UnlockIsUnavailableWhenNothingIsLocked(string file)
    {
        var save = Load(file);
        Assert.False(IsLocked(save));
        using var vm = new TrainerEditorViewModel(save);
        Assert.False(VmLocked(vm));
        Assert.False(CanUnlock(vm));
    }

    [Fact]
    public void LiveConflictRejectsTheGen6UnlockAndWritesNothing()
    {
        var save = Load("gen6_x.main");
        Lock(save);
        using var vm = new TrainerEditorViewModel(save);
        Unlock(vm);
        // Another editor changes the Battle Box block after the workspace opened.
        var box = ((ISaveBlock6Main)save).BattleBox;
        box[0].Span[0] ^= 0xFF;
        var live = save.Data.ToArray();

        vm.SaveCommand.Execute(null);

        Assert.True(vm.HasBattleBoxError);
        Assert.Equal(live, save.Data.ToArray());
    }

    [Fact]
    public void OrasDemoHasNoBattleBox()
    {
        using var vm = new TrainerEditorViewModel(new SAV6AODemo());
        Assert.False(vm.HasBattleBox);
        Assert.False(vm.UnlockBattleBoxCommand.CanExecute(null));
    }
}
