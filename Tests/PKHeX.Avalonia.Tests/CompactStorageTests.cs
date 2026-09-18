using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
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

/// <summary>
/// Compact storage acceptance checks. These tests instantiate the reusable PartyStrip independently
/// from MainWindow so the inline component stays a real interactive view rather than a decorative copy
/// of the party data.
/// </summary>
public sealed class CompactStorageTests
{
    [Fact]
    public void BoxViewer_UsesCompactStorageClasses_AndKeepsActualCapacity()
    {
        var xml = File.ReadAllText(Path.Combine(FindRepoRoot(), "PKHeX.Avalonia", "Views", "BoxViewer.axaml"));

        Assert.Contains("Classes=\"slot compact-slot\"", xml);
        Assert.Contains("UniformGrid Columns=\"6\"", xml);
        Assert.Contains("ItemsSource=\"{Binding Slots}\"", xml);
        Assert.Contains("Margin=\"{DynamicResource CompactSlotGap}\"", xml);

        var save = new SAV7b();
        var vm = new BoxViewerViewModel(save, Mock.Of<ISpriteRenderer>());
        Assert.Equal(save.BoxSlotCount, vm.Slots.Count);
    }

    [Fact]
    public void PartyStrip_IsACompiledReusablePartyViewerSurface()
    {
        var xml = File.ReadAllText(Path.Combine(FindRepoRoot(), "PKHeX.Avalonia", "Views", "PartyStrip.axaml"));

        Assert.Contains("x:DataType=\"vm:PartyViewerViewModel\"", xml);
        Assert.Contains("Classes=\"party-strip compact-party-strip\"", xml);
        Assert.Contains("ItemsSource=\"{Binding Slots}\"", xml);
        Assert.Contains("UniformGrid Columns=\"6\" Rows=\"1\"", xml);
        Assert.Contains("DragDrop.AllowDrop=\"True\"", xml);
        Assert.Contains("DragDrop.Drop=\"OnSlotDrop\"", xml);
        Assert.Contains("DragDrop.DragOver=\"OnSlotDragOver\"", xml);
        Assert.Contains("IsHitTestVisible=\"False\"", xml);
        Assert.Contains("Classes.selected=\"{Binding IsSelected}\"", xml);
    }

    [AvaloniaFact]
    public void PartyStrip_RealizesSixInteractiveSlots_WithZeroOneAndSixOccupied()
    {
        foreach (var occupied in new[] { 0, 1, 6 })
        {
            var save = new SAV6XY();
            for (var slot = 0; slot < occupied; slot++)
                save.SetPartySlotAtIndex(new PK6 { Species = (ushort)(slot + 1) }, slot);

            var vm = new PartyViewerViewModel(save, Mock.Of<ISpriteRenderer>());
            var view = new PartyStrip { DataContext = vm };
            var window = new Window { Content = view, Width = 640, Height = 92 };
            window.Show();
            Pump(window);

            var buttons = view.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.Tag is PartySlotData)
                .ToArray();

            Assert.Equal(6, buttons.Length);
            Assert.Equal(occupied, buttons.Count(button => button.Tag is PartySlotData slot && !slot.IsEmpty));
            Assert.All(buttons, button => Assert.True(button.Bounds.Width > 0 && button.Bounds.Height > 0));

            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void PartyStrip_TunneledModifierClicksKeepPartyCommandMeaning()
    {
        var save = new SAV6XY();
        save.SetPartySlotAtIndex(new PK6 { Species = 25 }, 2);
        var vm = new PartyViewerViewModel(save, Mock.Of<ISpriteRenderer>());
        var requests = new System.Collections.Generic.List<string>();
        vm.ViewSlotRequested += slot => requests.Add($"View:{slot}");
        vm.SetSlotRequested += slot => requests.Add($"Set:{slot}");
        vm.DeleteSlotRequested += slot => requests.Add($"Delete:{slot}");

        var view = new PartyStrip { DataContext = vm };
        var window = new Window { Content = view, Width = 640, Height = 92 };
        window.Show();
        Pump(window);

        var button = view.GetVisualDescendants()
            .OfType<Button>()
            .Single(candidate => candidate.Tag is PartySlotData slot && slot.Slot == 2);

        RaiseModifierClick(button, KeyModifiers.Control);
        RaiseModifierClick(button, KeyModifiers.Shift);
        RaiseModifierClick(button, KeyModifiers.Alt);

        Assert.Equal(["View:2", "Set:2", "Delete:2"], requests);
        Assert.Equal(0, vm.SelectedIndex);

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void PartyStrip_NormalClickSelectsWithoutActivatingTheSlot()
    {
        var save = new SAV6XY();
        var vm = new PartyViewerViewModel(save, Mock.Of<ISpriteRenderer>());
        var activated = -1;
        vm.SlotActivated += slot => activated = slot;

        var view = new PartyStrip { DataContext = vm };
        var window = new Window { Content = view, Width = 640, Height = 92 };
        window.Show();
        Pump(window);

        var button = view.GetVisualDescendants()
            .OfType<Button>()
            .Single(candidate => candidate.Tag is PartySlotData slot && slot.Slot == 4);

        ((IInvokeProvider)new ButtonAutomationPeer(button)).Invoke();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(4, vm.SelectedIndex);
        Assert.True(vm.Slots[4].IsSelected);

        window.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(-1, activated);
    }

    private static void RaiseModifierClick(Button button, KeyModifiers modifiers)
    {
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
        var args = new PointerPressedEventArgs(source, pointer, source, new Point(1, 1), 0, properties, modifiers, 1)
        {
            RoutedEvent = InputElement.PointerPressedEvent,
        };
        source.RaiseEvent(args);

        var releaseProperties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
        var releaseArgs = new PointerReleasedEventArgs(source, pointer, source, new Point(1, 1), 1, releaseProperties, modifiers, MouseButton.Left)
        {
            RoutedEvent = InputElement.PointerReleasedEvent,
        };
        source.RaiseEvent(releaseArgs);
    }

    private static void Pump(Window window)
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!Directory.Exists(Path.Combine(dir, "PKHeX.Avalonia")))
        {
            var parent = Directory.GetParent(dir);
            if (parent is null)
                throw new DirectoryNotFoundException("Could not find repository root");
            dir = parent.FullName;
        }

        return dir;
    }
}
