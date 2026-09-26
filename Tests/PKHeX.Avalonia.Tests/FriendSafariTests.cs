using Avalonia.Headless.XUnit;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Core;

namespace PKHeX.Avalonia.Tests;

public class FriendSafariTests
{
    [AvaloniaFact]
    public void CapabilityEntry_IsOnlyAvailableForXY()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV6XY());
        var entry = Assert.Single(app.ViewModel.ToolLauncherItems,
            item => ReferenceEquals(item.Command, app.ViewModel.UnlockFriendSafariCommand));
        Assert.True(entry.IsAvailable);
        app.LoadSaveInstance(new SAV6AO());
        Assert.False(entry.IsAvailable);
        app.LoadSaveInstance(new SAV7SM());
        Assert.False(entry.IsAvailable);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unlock_RequiresConfirmation_AndOnlyChangesPresentFriends(bool confirm)
    {
        using var app = new HeadlessAppFixture();
        var save = new SAV6XY();
        const int first = 0x1E7FF + 0x15;
        const int last = 0x1E7FF + 100 * 0x15;
        save.Data[first] = 1;
        save.Data[last] = 1;
        var before = save.Data.ToArray();
        app.LoadSaveInstance(save);
        app.Dialogs.ConfirmResult = confirm;
        await app.ViewModel.UnlockFriendSafariCommand.ExecuteAsync(null);
        Assert.Single(app.Dialogs.Confirmations);
        if (!confirm) Assert.Equal(before, save.Data);
        else
        {
            before[first] = before[last] = 0x3D;
            Assert.Equal(before, save.Data);
            Assert.True(save.State.Edited);
        }
    }
}
