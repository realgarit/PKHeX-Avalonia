using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Models;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class SlotModifierClickRoutingTests
{
    [AvaloniaFact]
    public void BoxViewer_ButtonPointerPress_StillRoutesModifierAction()
    {
        var sav = new SAV6XY();
        sav.SetBoxSlotAtIndex(new PK6 { Species = 25 }, 0, 3);
        var vm = new BoxViewerViewModel(sav, new Mock<ISpriteRenderer>().Object);
        var requests = new List<string>();
        vm.ViewSlotRequested += (_, slot) => requests.Add($"View:{slot}");
        vm.SetSlotRequested += (_, slot) => requests.Add($"Set:{slot}");
        vm.DeleteSlotRequested += (_, slot) => requests.Add($"Delete:{slot}");

        var view = new BoxViewer { DataContext = vm };
        var window = new Window { Content = view, Width = 720, Height = 640 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var button = view.GetVisualDescendants()
            .OfType<Button>()
            .Single(b => b.Tag is SlotData slot && slot.Slot == 3);

        RaiseModifierClick(button, KeyModifiers.Control);
        RaiseModifierClick(button, KeyModifiers.Shift);
        RaiseModifierClick(button, KeyModifiers.Alt);

        Assert.Equal(["View:3", "Set:3", "Delete:3"], requests);
        Assert.Equal(0, vm.SelectedIndex);
    }

    [AvaloniaFact]
    public void PartyViewer_ButtonPointerPress_StillRoutesModifierAction()
    {
        var sav = new SAV6XY();
        sav.SetPartySlotAtIndex(new PK6 { Species = 25 }, 2);
        var vm = new PartyViewerViewModel(sav, new Mock<ISpriteRenderer>().Object);
        var requests = new List<string>();
        vm.ViewSlotRequested += slot => requests.Add($"View:{slot}");
        vm.SetSlotRequested += slot => requests.Add($"Set:{slot}");
        vm.DeleteSlotRequested += slot => requests.Add($"Delete:{slot}");

        var view = new PartyViewer { DataContext = vm };
        var window = new Window { Content = view, Width = 520, Height = 640 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var button = view.GetVisualDescendants()
            .OfType<Button>()
            .Single(b => b.Tag is PartySlotData slot && slot.Slot == 2);

        RaiseModifierClick(button, KeyModifiers.Control);
        RaiseModifierClick(button, KeyModifiers.Shift);
        RaiseModifierClick(button, KeyModifiers.Alt);

        Assert.Equal(["View:2", "Set:2", "Delete:2"], requests);
        Assert.Equal(0, vm.SelectedIndex);
    }

    private static void RaiseModifierClick(Button button, KeyModifiers modifiers)
    {
        // Pointer input can originate on the Button template's Border/ContentPresenter rather than
        // the Button itself. Start the routed event on a real descendant so the test covers the
        // ancestor lookup used by the viewer handler.
        var source = button.GetVisualDescendants().OfType<Control>().FirstOrDefault() ?? button;
        var rawModifiers = RawInputModifiers.LeftMouseButton;
        if (modifiers.HasFlag(KeyModifiers.Control))
            rawModifiers |= RawInputModifiers.Control;
        if (modifiers.HasFlag(KeyModifiers.Shift))
            rawModifiers |= RawInputModifiers.Shift;
        if (modifiers.HasFlag(KeyModifiers.Alt))
            rawModifiers |= RawInputModifiers.Alt;

        var pointer = new Pointer(1, PointerType.Mouse, true);
        var properties = new PointerPointProperties(rawModifiers, PointerUpdateKind.LeftButtonPressed);
        var args = new PointerPressedEventArgs(
            source,
            pointer,
            source,
            new Point(1, 1),
            0,
            properties,
            modifiers,
            1)
        {
            RoutedEvent = InputElement.PointerPressedEvent,
        };

        source.RaiseEvent(args);

        var releaseProperties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
        var releaseArgs = new PointerReleasedEventArgs(
            source,
            pointer,
            source,
            new Point(1, 1),
            1,
            releaseProperties,
            modifiers,
            MouseButton.Left)
        {
            RoutedEvent = InputElement.PointerReleasedEvent,
        };

        source.RaiseEvent(releaseArgs);
    }
}
