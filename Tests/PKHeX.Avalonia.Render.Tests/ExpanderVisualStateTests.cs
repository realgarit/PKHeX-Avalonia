using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

public class ExpanderVisualStateTests
{
    [AvaloniaFact]
    public async Task GeneratorChevronSettlesToCollapsedAndExpandedAngles()
    {
        using var vm = new DonutEditorViewModel(new SAV9ZA());
        var view = new DonutEditor { DataContext = vm };
        var window = new Window { Content = view, Width = 1000, Height = 760 };
        window.Show();
        try
        {
            var expander = view.FindControl<Expander>("DonutGenerator")!;
            foreach (bool expanded in new[] { false, true, false })
            {
                expander.IsExpanded = expanded;
                await PKHeX.Testing.HeadlessRenderSettling.Settle(window, () =>
                {
                    var chevron = expander.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Path>().Single(element => element.Name == "ExpandCollapseChevron");
                    return chevron.RenderTransform is RotateTransform transform && Math.Abs(transform.Angle - (expanded ? 180 : 0)) < 0.0001;
                });
                // Render the actual laid-out View synchronously. The headless compositor's
                // latest window buffer can still be null on a runner despite correct layout.
                using var frame = new RenderTargetBitmap(new PixelSize((int)view.Bounds.Width, (int)view.Bounds.Height));
                frame.Render(view);
                using var png = new MemoryStream();
                frame.Save(png, PngBitmapEncoderOptions.Default);
                using var rendered = SkiaSharp.SKBitmap.Decode(png.ToArray());
                Assert.NotNull(rendered);
                Assert.True(rendered.Pixels.Any(pixel => pixel != rendered.GetPixel(0, 0)), "The editor frame must contain drawn content, not a uniform surface.");
                var header = expander.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.ToggleButton>().Single(element => element.Name == "ExpanderHeader");
                Assert.Equal(expanded, header.IsChecked);
                Assert.Equal(expanded ? "expanded" : "collapsed", header.Tag);
                var path = expander.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Path>().Single(element => element.Name == "ExpandCollapseChevron");
                var rotation = Assert.IsType<RotateTransform>(path.RenderTransform);
                Assert.Equal(expanded ? 180 : 0, rotation.Angle, precision: 3);
            }
        }
        finally { window.Close(); }
    }

}
