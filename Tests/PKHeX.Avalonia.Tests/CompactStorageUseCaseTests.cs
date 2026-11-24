using PKHeX.Application.UseCases;
using PKHeX.Core;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Let's Go stores its boxes as one packed list. A Pokémon placed after the end of the list must be
/// moved up and counted before the save is written, the same as WinForms' pre-export cleanup.
/// </summary>
public class CompactStorageUseCaseTests
{
    private static PB7 CreatePikachu() => new() { Species = (ushort)Species.Pikachu, CurrentLevel = 5 };

    [Fact]
    public void Execute_LetsGoEntityAfterGap_MovesItToListStartAndCountsIt()
    {
        var sav = new SAV7b();
        sav.SetBoxSlotAtIndex(CreatePikachu(), 1, 0); // box 2, slot 1: 25 empty slots before it

        Assert.True(new CompactStorageUseCase().Execute(sav));

        Assert.Equal((ushort)Species.Pikachu, sav.GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal(0, sav.GetBoxSlotAtIndex(1, 0).Species);
        Assert.Equal(1, sav.Blocks.Storage.Count);
    }

    [Fact]
    public void Execute_LetsGoListAlreadyPacked_ReportsNoMove()
    {
        var sav = new SAV7b();
        sav.SetBoxSlotAtIndex(CreatePikachu(), 0, 0);

        Assert.False(new CompactStorageUseCase().Execute(sav));
        Assert.Equal((ushort)Species.Pikachu, sav.GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal(1, sav.Blocks.Storage.Count);
    }

    [Fact]
    public void Execute_FormatWithoutStorageCleanup_DoesNothing()
    {
        var sav = new SAV6XY();
        Assert.False(new CompactStorageUseCase().Execute(sav));
    }
}
