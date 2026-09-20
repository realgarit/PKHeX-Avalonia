using PKHeX.Avalonia.Services;
using Xunit;

namespace PKHeX.Avalonia.Tests;

public sealed class WindowServiceTests
{
    [Fact]
    public void ClampInitialBounds_BoundsLargeTabDrivenContentToLaptopWorkspace()
    {
        var bounds = WindowService.ClampInitialBounds(1380, 804, 978, 760);

        Assert.Equal(978, bounds.Width);
        Assert.Equal(760, bounds.Height);
    }

    [Fact]
    public void ClampInitialBoundsProvidesStableMinimumForCompactTabs()
    {
        var bounds = WindowService.ClampInitialBounds(352, 288, 978, 760);

        Assert.Equal(420, bounds.Width);
        Assert.Equal(300, bounds.Height);
    }
}
