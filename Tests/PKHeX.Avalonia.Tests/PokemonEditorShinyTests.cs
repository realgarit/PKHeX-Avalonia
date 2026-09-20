using PKHeX.Core;
using Xunit;

namespace PKHeX.Avalonia.Tests;

public sealed class PokemonEditorShinyTests
{
    [Fact]
    public void Gen3OriginTransfer_ToggleShinyKeepsPidAndEcSynchronized()
    {
        var save = new SAV6XY();
        var pkm = new PK6
        {
            Species = (ushort)Species.Mew,
            Version = GameVersion.E,
            CurrentLevel = 30,
            TID16 = 0x1111,
            SID16 = 0x2222,
            PID = 0x12345678,
            EncryptionConstant = 0x87654321,
        };

        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, save);
        vm.IsShiny = true;

        var prepared = vm.PreparePKM();

        Assert.True(prepared.IsShiny);
        Assert.Equal(prepared.PID, prepared.EncryptionConstant);
        Assert.Equal(prepared.PID.ToString("X8"), vm.Pid);
        Assert.Equal(prepared.EncryptionConstant.ToString("X8"), vm.EncryptionConstant);
    }
}
