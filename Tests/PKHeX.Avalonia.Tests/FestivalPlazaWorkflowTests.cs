using Avalonia.Headless.XUnit;
using System.Buffers.Binary;
using CommunityToolkit.Mvvm.Messaging;
using PKHeX.Presentation.Localization;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class FestivalPlazaWorkflowTests
{
    [Fact]
    public void TimestampCommit_IsAtomicAcrossComponents_AndPreservesUnusedWord()
    {
        var save = new SAV7SM(); save.Festa.FestaDate = new DateTime(2020, 2, 28, 1, 2, 3);
        using var vm = new FestivalPlazaEditorViewModel(save);
        vm.Timestamp = "2024-02-28 01:02:03";
        save.Festa.FestaDate = new DateTime(2020, 3, 1, 4, 5, 6);
        BinaryPrimitives.WriteUInt32LittleEndian(save.Festa.Data[0x2FC..], 0xAABBCCDD);
        vm.SaveCommand.Execute(null);
        Assert.Equal(new DateTime(2024, 2, 28, 1, 2, 3), save.Festa.FestaDate);
        Assert.Equal(0xAABBCCDDu, BinaryPrimitives.ReadUInt32LittleEndian(save.Festa.Data[0x2FC..]));
    }

    [AvaloniaFact]
    public void LiveLanguageChange_RefreshesDomainChoices_WithoutResettingStagedFields()
    {
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        try
        {
            LocalizedStrings.Instance.SetLanguage("en");
            using var vm = new FestivalPlazaEditorViewModel(new SAV7USUM());
            vm.CurrentFC = 123; vm.Facilities[0].Color = 1; vm.Phrases[0].Unlocked = true;
            var english = vm.Facilities[0].ColorChoices[0].Text;
            LocalizedStrings.Instance.SetLanguage("de");
            WeakReferenceMessenger.Default.Send(new LanguageChangedMessage("de"));
            Assert.NotEqual(english, vm.Facilities[0].ColorChoices[0].Text);
            Assert.Equal(123, vm.CurrentFC); Assert.Equal(1, vm.Facilities[0].Color); Assert.True(vm.Phrases[0].Unlocked);
            Assert.Equal(0, vm.Facilities[0].Type);
        }
        finally { LocalizedStrings.Instance.SetLanguage(previous); WeakReferenceMessenger.Default.Send(new LanguageChangedMessage(previous)); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EverySupportedField_RoundTripsOnSmAndUsum(bool ultra)
    {
        SAV7 save = ultra ? new SAV7USUM() : new SAV7SM();
        var before = save.Data.ToArray();
        var vm = new FestivalPlazaEditorViewModel(save);
        vm.PlazaName = "Festival"; vm.Rank = 100;
        vm.CurrentFC = 123; vm.UsedFC = 456;
        vm.Timestamp = "2026-10-01 12:34:56";
        for (var i = 0; i < 4; i++) vm.Messages[i].Value = 100 + i;
        for (var i = 0; i < 107; i++) vm.Phrases[i].Unlocked = true;
        for (var i = 0; i < 11; i++) vm.Rewards[i].Value = i % 3;
        foreach (var facility in vm.Facilities)
        {
            facility.Type = ultra ? 127 : 124; facility.Color = 3;
            facility.Npc = 11; facility.Gender = 1; facility.OwnerName = "Visitor"; facility.IsIntroduced = true;
            facility.FestivalId = "0123456789ABCDEF01234567";
            for (var i = 0; i < 4; i++) facility.Messages[i].Value = 200 + i;
            for (var i = 0; i < 5; i++) facility.Usage[i].Value = i is 2 or 3 ? uint.MaxValue - i : 3 + i;
        }
        Assert.Equal(before, save.Data.ToArray());
        Assert.True(vm.CanSave); vm.SaveCommand.Execute(null);
        SAV7 reloaded = ultra ? new SAV7USUM(save.Write()) : new SAV7SM(save.Write());
        Assert.Equal("Festival", reloaded.Festa.FestivalPlazaName);
        Assert.Equal(100, reloaded.Festa.FestaRank);
        Assert.Equal(123, reloaded.Festa.FestaCoins); Assert.Equal(456, reloaded.GetRecord(38));
        Assert.Equal(579, reloaded.Festa.TotalFestaCoins);
        Assert.Equal(new DateTime(2026, 10, 1, 12, 34, 56), reloaded.Festa.FestaDate);
        for (var i = 0; i < 4; i++) Assert.Equal(100 + i, reloaded.Festa.GetFestaMessage(i));
        for (var i = 0; i < 107; i++) Assert.True(reloaded.Festa.GetFestaPhraseUnlocked(i));
        for (var i = 0; i < 11; i++) Assert.Equal(i % 3, reloaded.Festa.GetFestPrizeReceived(i));
        for (var i = 0; i < 7; i++)
        {
            var f = reloaded.Festa.GetFestaFacility(i);
            Assert.Equal(ultra ? 127 : 124, f.Type); Assert.Equal(3, f.Color);
            Assert.Equal(11, f.NPC); Assert.Equal(1, f.Gender); Assert.Equal("Visitor", f.OriginalTrainerName); Assert.True(f.IsIntroduced);
            Assert.Equal("0123456789ABCDEF01234567", Convert.ToHexString(f.TrainerFesID));
            for (var j = 0; j < 4; j++) Assert.Equal(200 + j, f.GetMessage(j));
            Assert.Equal(3, f.UsedLuckyRank); Assert.Equal(4, f.UsedLuckyPlace);
            Assert.Equal(uint.MaxValue - 2, f.UsedFlags); Assert.Equal(uint.MaxValue - 3, f.UsedRandStat);
            Assert.Equal(7, f.ExchangeLeftCount);
        }
    }
    [Fact]
    public void InvalidHiddenFields_BlockSave_WithoutApplyingPartialEdits()
    {
        var save = new SAV7SM(); var before = save.Data.ToArray(); var vm = new FestivalPlazaEditorViewModel(save);
        vm.PlazaName = "Staged"; vm.Facilities[6].FestivalId = "INVALID";
        Assert.False(vm.CanSave); vm.SaveCommand.Execute(null); Assert.Equal(before, save.Data.ToArray());
        vm.Facilities[6].FestivalId = new string('0', 24);
        vm.Timestamp = "2026-02-30 12:34:56";
        Assert.False(vm.CanSave); Assert.NotEmpty(vm.TimestampError);
        vm.Timestamp = "2026-10-01 12:34:56";
        vm.Messages[3].Value = 65536; Assert.False(vm.CanSave);
        vm.Messages[3].Value = 65535; Assert.True(vm.CanSave);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnknownFacilityAndRewardValues_InvalidTimestamp_AndSemanticRevertsPreserveBytes(bool ultra)
    {
        SAV7 save = ultra ? new SAV7USUM() : new SAV7SM();
        save.Festa.Data[0x310] = 255; save.Festa.Data[0x311] = 255; save.Festa.Data[0x312] = 2;
        save.Festa.Data[0x313] = 255; save.Festa.Data[0x2A50] = 2;
        save.Festa.SetFestaPrizeReceived(0, 255);
        save.Festa.FestaDate = new DateTime(2026, 10, 1);
        BinaryPrimitives.WriteInt32LittleEndian(save.Festa.Data[0x300..], 99);
        save.Festa.FestivalPlazaName = "Plaza"; save.Festa.Data[0x538] = 0xA7;
        save.State.Edited = false; var before = save.Data.ToArray();
        var vm = new FestivalPlazaEditorViewModel(save);
        Assert.Empty(vm.Timestamp); Assert.True(vm.CanSave);
        Assert.Contains(vm.Facilities[0].TypeChoices, c => c.Value == 255);
        Assert.Contains(vm.Facilities[0].ColorChoices, c => c.Value == 255);
        Assert.Contains(vm.Rewards[0].Choices, c => c.Value == 255);
        vm.Facilities[0].IsIntroduced = false; vm.Facilities[0].IsIntroduced = true;
        vm.Phrases[0].Unlocked = false; vm.Phrases[0].Unlocked = true;
        vm.PlazaName = "Different"; vm.PlazaName = "Plaza";
        vm.SaveCommand.Execute(null);
        Assert.Equal(before, save.Data.ToArray()); Assert.False(save.State.Edited);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BulkAndDeletion_RequireConfirmation_AndOnlyStageDocumentedFields(bool confirm)
    {
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(confirm);
        var save = new SAV7USUM(); var f = save.Festa.GetFestaFacility(0);
        f.Type = 127; f.IsIntroduced = true; f.OriginalTrainerName = "Visitor"; f.Gender = 1;
        f.TrainerFesID.Fill(0xAA); f.SetMessage(0, 20); f.UsedFlags = 123; f.ExchangeLeftCount = 3;
        save.Festa.SetFestaPrizeReceived(1, 2); save.Festa.SetFestaPrizeReceived(2, 255);
        var before = save.Data.ToArray(); var vm = new FestivalPlazaEditorViewModel(save, dialogs.Object);
        await vm.UnlockAllPhrasesCommand.ExecuteAsync(null); await vm.UnlockAllRewardsCommand.ExecuteAsync(null);
        await vm.DeleteVisitorCommand.ExecuteAsync(null);
        Assert.Equal(before, save.Data.ToArray());
        Assert.Equal(confirm, vm.Phrases[0].Unlocked);
        Assert.Equal(confirm ? 1 : 0, vm.Rewards[0].Value);
        Assert.Equal(2, vm.Rewards[1].Value); Assert.Equal(255, vm.Rewards[2].Value);
        vm.SaveCommand.Execute(null); f = save.Festa.GetFestaFacility(0);
        Assert.Equal(confirm ? string.Empty : "Visitor", f.OriginalTrainerName);
        Assert.Equal(!confirm, f.IsIntroduced); Assert.Equal(127, f.Type); Assert.Equal(123u, f.UsedFlags); Assert.Equal(3, f.ExchangeLeftCount);
        Assert.Equal(confirm ? new byte[12] : Enumerable.Repeat((byte)0xAA, 12).ToArray(), f.TrainerFesID.ToArray());
        dialogs.Verify(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(3));
    }
    [Fact]
    public void WholeFieldCommit_AppliesDesiredValue_WhenSameFieldWasIndependentlyChanged()
    {
        var save = new SAV7SM(); save.Festa.SetFestaMessage(0, 0x1234);
        var vm = new FestivalPlazaEditorViewModel(save);
        vm.Messages[0].Value = 0x12AB; save.Festa.SetFestaMessage(0, 0xCD34);
        vm.SaveCommand.Execute(null);
        Assert.Equal(0x12AB, save.Festa.GetFestaMessage(0));
    }
}
