using PKHeX.Core;
using Xunit;

namespace PKHeX.Avalonia.Tests;

public sealed class PokemonEditorShinyTests
{
    [Fact]
    public void Gen3OriginTransfer_ToggleShinyKeepsPidAndEcSynchronized()
    {
        var save = new SAV6XY();
        const uint encryptionConstant = 0x87654321;
        var pkm = new PK6
        {
            Species = (ushort)Species.Mew,
            Version = GameVersion.E,
            CurrentLevel = 30,
            TID16 = 0x1111,
            SID16 = 0x2222,
            EncryptionConstant = encryptionConstant,
        };
        pkm.PID = PK5.GetTransferPID(pkm.EncryptionConstant, pkm.ID32, out _);

        // Start from an actually valid Gen 3-origin transfer relation. A random PID/EC
        // pair would only test the editor's repair behavior, not the preserved legality path.
        Assert.Equal(pkm.PID, PK5.GetTransferPID(pkm.EncryptionConstant, pkm.ID32, out _));

        var (vm, _, _) = TestHelpers.CreateTestViewModel(pkm, save);
        vm.IsShiny = true;

        var prepared = vm.PreparePKM();

        Assert.True(prepared.IsShiny);
        Assert.Equal(prepared.PID, prepared.EncryptionConstant);
        Assert.Equal(prepared.PID.ToString("X8"), vm.Pid);
        Assert.Equal(prepared.EncryptionConstant.ToString("X8"), vm.EncryptionConstant);
    }
}
