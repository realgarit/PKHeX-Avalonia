using PKHeX.Application.Services;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using Moq;
using PKHeX.Application.Abstractions;

namespace PKHeX.Avalonia.Tests;

public class SvPokedexSessionTests(ITestOutputHelper output)
{
    private static SAV9SV LoadDlc() => Assert.IsType<SAV9SV>(Fixtures.SaveFileFixture.LoadSave(Path.Combine(Fixtures.SaveFileFixture.FindSaveFilesPath()!, "gen9_violet_indigo_public.main")));
    [Theory] [InlineData(1017)] [InlineData(1024)] [InlineData(1025)]
    public void DlcFormsRoundtripWithoutWritingTheRetiredPaldeaBlock(int species)
    {
        var save = LoadDlc(); var retired = save.Blocks.GetBlock(0x0DEAAEBD).Data.ToArray();
        using var vm = new PokedexGen9EditorViewModel(save); vm.SelectedSpecies = vm.SpeciesList.Single(choice => choice.Value == species);
        Assert.True(vm.UsesDlcFormat); Assert.NotEmpty(vm.FormStates);
        var form = vm.FormStates[0]; bool wanted = !form.Obtained; form.Obtained = wanted; form.Seen = true; form.Heard = true; form.Viewed = true;
        vm.SaveCurrentCommand.Execute(null); Assert.Empty(vm.Error);
        Assert.Equal(retired, save.Blocks.GetBlock(0x0DEAAEBD).Data.ToArray());
        var reopened = Assert.IsType<SAV9SV>(SaveUtil.GetSaveFile(save.Write())); var session = new SvPokedexDataSession(reopened);
        var stored = session.ReadForm((ushort)species, form.Form);
        Assert.Equal(wanted, stored.Obtained); Assert.True(stored.Seen); Assert.True(stored.Heard); Assert.True(stored.Viewed);
    }
    [Fact]
    public void RegionalDisplayEditsPreserveIndependentOpaqueSelections()
    {
        var save = LoadDlc(); var entry = save.Blocks.Zukan.DexKitakami.Get(25);
        entry.DisplayedPaldeaShiny = 7; entry.DisplayedKitakamiGender = 255; entry.DisplayedBlueberryShiny = 9;
        using var vm = new PokedexGen9EditorViewModel(save); vm.SelectedSpecies = vm.SpeciesList.Single(choice => choice.Value == 25);
        Assert.Equal(new[] { 1, 2 }, vm.RegionalDisplays.Select(display => display.Region));
        var paldea = vm.RegionalDisplays.Single(display => display.Region == 1); paldea.Gender = paldea.Gender == 0 ? 1 : 0;
        vm.SaveCurrentCommand.Execute(null); Assert.Empty(vm.Error);
        var actual = save.Blocks.Zukan.DexKitakami.Get(25);
        Assert.Equal(7, actual.DisplayedPaldeaShiny); Assert.Equal(255, actual.DisplayedKitakamiGender); Assert.Equal(9, actual.DisplayedBlueberryShiny);
        Assert.Equal(paldea.Gender, actual.DisplayedPaldeaGender);
    }
    [Fact]
    public void ConflictingDlcEditorRejectsEveryWriteAndRetainsItsDraft()
    {
        var save = LoadDlc(); using var first = new PokedexGen9EditorViewModel(save); using var second = new PokedexGen9EditorViewModel(save);
        first.SelectedSpecies = first.SpeciesList.Single(choice => choice.Value == 1017); second.SelectedSpecies = second.SpeciesList.Single(choice => choice.Value == 1024);
        first.FormStates[0].Obtained = !first.FormStates[0].Obtained; first.SaveCurrentCommand.Execute(null); Assert.Empty(first.Error);
        bool wanted = !second.FormStates[0].Seen; second.FormStates[0].Seen = wanted;
        var before = save.AllBlocks.ToDictionary(block => block.Key, block => block.Data.ToArray());
        second.SaveCurrentCommand.Execute(null); Assert.NotEmpty(second.Error); Assert.Equal(wanted, second.FormStates[0].Seen);
        foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
        second.ResetCommand.Execute(null); Assert.Empty(second.Error);
    }
    private static SAV9SV LoadBase() => Assert.IsType<SAV9SV>(Fixtures.SaveFileFixture.LoadSave(Path.Combine(Fixtures.SaveFileFixture.FindSaveFilesPath()!, "gen9_scarlet.main")));
    [Fact]
    public async Task SeenOnlyConfirmationPreservesCaughtAndStagesUntilApply()
    {
        var save = LoadBase(); save.Blocks.Zukan.DexPaldea.Get(906).SetCaught(false);
        var dialogs = new Mock<IDialogService>(); dialogs.Setup(dialog => dialog.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        using var vm = new PokedexGen9EditorViewModel(save, dialogs.Object); vm.SelectedSpecies = vm.SpeciesList.Single(choice => choice.Value == 906);
        var before = save.AllBlocks.ToDictionary(block => block.Key, block => block.Data.ToArray());
        await vm.SeenAllCommand.ExecuteAsync(null); Assert.False(vm.IsCaught); Assert.True(vm.CanUndo);
        foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
        vm.SaveCurrentCommand.Execute(null); Assert.Empty(vm.Error); Assert.False(save.Blocks.Zukan.GetCaught(906)); Assert.True(save.Blocks.Zukan.GetSeen(906));
    }
    [Fact]
    public async Task ConfirmedCaughtCanBeUndoneAndLateAnswersCannotRestoreAfterReset()
    {
        var save = LoadBase(); var answer = new TaskCompletionSource<bool>();
        var dialogs = new Mock<IDialogService>(); dialogs.Setup(dialog => dialog.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(answer.Task);
        using var vm = new PokedexGen9EditorViewModel(save, dialogs.Object);
        var pending = vm.CaughtAllCommand.ExecuteAsync(null); vm.ResetCommand.Execute(null); answer.SetResult(true); await pending; Assert.False(vm.CanUndo);
        var before = save.AllBlocks.ToDictionary(block => block.Key, block => block.Data.ToArray());
        dialogs.Setup(dialog => dialog.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        await vm.CaughtAllCommand.ExecuteAsync(null); vm.UndoCommand.Execute(null); vm.SaveCurrentCommand.Execute(null); Assert.Empty(vm.Error);
        foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
    }
    [Theory] [InlineData("gen9_scarlet.main")] [InlineData("gen9_violet.main")] [InlineData("gen9_violet_indigo_public.main")] [InlineData("gen9_scarlet_teal_public.main")]
    public void ViewModelDraftsDoNotWriteBeforeApplyAndResetDiscards(string filename)
    {
        var save = Assert.IsType<SAV9SV>(Fixtures.SaveFileFixture.LoadSave(Path.Combine(Fixtures.SaveFileFixture.FindSaveFilesPath()!, filename)));
        output.WriteLine($"{filename}: revision={save.SaveRevision}, mode={save.Blocks.Zukan.GetRevision()}, maxSpecies={save.MaxSpeciesID}");
        var before = save.AllBlocks.ToDictionary(block => block.Key, block => block.Data.ToArray());
        using var vm = new PokedexGen9EditorViewModel(save);
        Assert.Contains(vm.SpeciesList, choice => choice.Value == save.MaxSpeciesID);
        if (vm.UsesDlcFormat) vm.FormStates[0].Seen = !vm.FormStates[0].Seen;
        else vm.IsSeenMale = !vm.IsSeenMale;
        foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
        vm.ResetCommand.Execute(null); vm.SaveCurrentCommand.Execute(null); Assert.Empty(vm.Error);
        foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
    }
    [Theory] [InlineData("gen9_scarlet.main")] [InlineData("gen9_violet.main")] [InlineData("gen9_violet_indigo_public.main")] [InlineData("gen9_scarlet_teal_public.main")]
    public void CorpusUsesTheActiveFormatAndPreservesNoOpBytes(string filename)
    {
        var save = Assert.IsType<SAV9SV>(Fixtures.SaveFileFixture.LoadSave(Path.Combine(Fixtures.SaveFileFixture.FindSaveFilesPath()!, filename)));
        var before = save.AllBlocks.ToDictionary(block => block.Key, block => block.Data.ToArray());
        var session = new SvPokedexDataSession(save);
        output.WriteLine($"{filename}: revision={save.SaveRevision}, mode={save.Blocks.Zukan.GetRevision()}, maxSpecies={save.MaxSpeciesID}");
        Assert.Equal(save.Blocks.Zukan.GetRevision() != 0, session.UsesDlcFormat);
        Assert.Equal(save.MaxSpeciesID, session.MaxSpecies);
        foreach (ushort species in session.Species())
        {
            var form = session.ReadForm(species, 0); session.WriteForm(species, 0, form.Obtained, form.Seen, form.Heard, form.Viewed);
        }
        Assert.True(session.TryCommit());
        foreach (var block in save.AllBlocks) Assert.Equal(before[block.Key], block.Data.ToArray());
        Assert.IsType<SAV9SV>(SaveUtil.GetSaveFile(save.Write()));
    }
}
