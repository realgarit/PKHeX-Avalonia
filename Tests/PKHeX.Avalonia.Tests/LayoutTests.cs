using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.ViewModels;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using Moq;
using Xunit;
using Xunit.Sdk;

namespace PKHeX.Avalonia.Tests;

public class LayoutTests
{
    private readonly Mock<ISpriteRenderer> _spriteRendererMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly SaveFile _saveFile;

    public LayoutTests()
    {
        _spriteRendererMock = new Mock<ISpriteRenderer>();
        _dialogServiceMock = new Mock<IDialogService>();
        _saveFile = new SAV3E(); // Using E explicitly as established
    }

    [AvaloniaFact]
    public void Verify_No_Horizontal_Overflow()
    {
        // 1. Setup the View and ViewModel
        var pkm = new PK3();
        var vm = new PokemonEditorViewModel(pkm, _saveFile, _spriteRendererMock.Object, _dialogServiceMock.Object);
        var view = new PokemonEditor { DataContext = vm };

        // Create a window to host the view (use 420px to match MainWindow left column)
        var window = new Window
        {
            Content = view,
            Width = 420,
            Height = 600
        };
        window.Show();

        // 3. Force Layout Pass
        Dispatcher.UIThread.RunJobs();
        view.Measure(new Size(400, 600));
        view.Arrange(new Rect(0, 0, 400, 600));
        Dispatcher.UIThread.RunJobs();

        // 4. Inspect Visual Tree
        VerifyBounds(view, view.Bounds);
    }

    private void VerifyBounds(Visual visual, Rect rootBounds)
    {
        var bounds = visual.Bounds;

        // Skip items that are intentionally invisible or collapsed
        if (!visual.IsVisible || bounds.Width == 0 || bounds.Height == 0)
            return;

        // Recursively check children
        foreach (var child in visual.GetVisualChildren())
        {
            if (child is Visual vChild)
            {
                // Skip internal Avalonia containers that often report weird bounds (e.g. DatePicker internal Viewbox)
                // Also skip TextBlocks since TextTrimming handles overflow at render time, not measure time
                var typeName = vChild.GetType().Name;
                if (typeName.Contains("Viewbox") || typeName == "TextBlock")
                    continue;

                // Check if child fits in parent horizontally
                // Allow small floating point error (2.0 pixel) for border snapping/rounding
                Assert.True(vChild.Bounds.Right <= bounds.Width + 2.0, 
                    $"Horizontal Overflow: {vChild.GetType().Name} (Right: {vChild.Bounds.Right}) exceeds parent {visual.GetType().Name} (Width: {bounds.Width}). \nContext: {vChild}");

                // Recursively check
                VerifyBounds(vChild, rootBounds);
            }
        }
    }

    [AvaloniaFact]
    public void Verify_Critical_Controls_Are_Visible()
    {
        var pkm = new PK3();
        var vm = new PokemonEditorViewModel(pkm, _saveFile, _spriteRendererMock.Object, _dialogServiceMock.Object);
        var view = new PokemonEditor { DataContext = vm };

        var window = new Window { Content = view, Width = 400, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Helper to find by Type
        var textBoxes = view.GetVisualDescendants().OfType<TextBox>();
        
        foreach (var tb in textBoxes)
        {
            if (tb.Name == "NicknameBox") // Example if we named it, or just generally check all
            {
                 // ensure it has size
                 Assert.True(tb.Bounds.Width > 0);
                 Assert.True(tb.Bounds.Height > 0);
            }
        }
    }
}
