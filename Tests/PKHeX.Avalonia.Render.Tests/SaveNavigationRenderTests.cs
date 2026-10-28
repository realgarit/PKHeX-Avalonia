using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Core;
using PKHeX.Presentation.Models;
using PKHeX.Presentation.ViewModels;
using PKHeX.Avalonia.Views;

namespace PKHeX.Avalonia.Render.Tests;

public class SaveNavigationRenderTests
{
    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task PopulatedSaveUsesOneNavigationRowAndConsistentSelectedStates(bool dark)
    {
        var previous = global::Avalonia.Application.Current!.RequestedThemeVariant;
        var root = Path.Combine(Path.GetTempPath(), "pkhex-render-" + Guid.NewGuid().ToString("N"));
        var paths = new Mock<IAppPaths>();
        paths.SetupGet(x => x.ConfigDirectory).Returns(Path.Combine(root, "config"));
        paths.SetupGet(x => x.DataDirectory).Returns(Path.Combine(root, "data"));
        paths.SetupGet(x => x.ConfigFilePath).Returns(Path.Combine(root, "config.json"));
        paths.SetupGet(x => x.LegacyConfigFilePath).Returns(Path.Combine(root, "legacy.json"));
        using var services = (ServiceProvider)App.BuildServiceProvider(paths.Object, Mock.Of<ISettingsStore>(), new AppSettings(), s =>
        {
            s.AddSingleton(Mock.Of<IDialogService>());
            s.AddSingleton(Mock.Of<IWindowService>());
        });
        var vm = services.GetRequiredService<MainWindowViewModel>();
        Window? window = null;
        try
        {
            global::Avalonia.Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tests/savefiles/gen9a_legendsza.main"))) directory = directory.Parent;
            Assert.NotNull(directory);
            var save = Assert.IsType<SAV9ZA>(SaveUtil.GetSaveFile(File.ReadAllBytes(Path.Combine(directory.FullName, "Tests/savefiles/gen9a_legendsza.main"))));
            services.GetRequiredService<ISaveFileGateway>().OpenLoadedSave(save, "fixture.main");
            Assert.True(vm.HasSave);
            window = new MainWindow { DataContext = vm, Width = 900, Height = 600 }; window.Show();
            foreach (var index in new[] { 2, 3, 4, 6 })
            {
                vm.SelectWorkspaceTabCommand.Execute(index);
                if (index == MainWindowViewModel.InventoryWorkspaceTabIndex)
                    vm.InventoryEditor!.SelectedPouch = vm.InventoryEditor.Pouches.Single(p => p.ItemList.Any(item => item.Value == 632));
                await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                var tabs = window.GetVisualDescendants().OfType<TabControl>().Single(x => x.Name == "WorkspacePane");
                Assert.Equal(index, tabs.SelectedIndex);
                if (index >= 4)
                    Assert.Single(window.GetVisualDescendants().OfType<Button>(), x => x.IsEffectivelyVisible && x.Classes.Contains("workspace-top-tab") && x.Classes.Contains("active") && x.Command == vm.SelectWorkspaceTabCommand);
                Assert.DoesNotContain(window.GetVisualDescendants().OfType<MenuItem>(), x => x.Header?.ToString() == PKHeX.Presentation.Localization.LocalizedStrings.Instance["Menu_Save"]);
                if (index == 2)
                {
                    var trainer = window.GetVisualDescendants().OfType<TrainerZaWorkspace>().Single();
                    Assert.All(trainer.GetVisualDescendants().OfType<TabItem>(), x => Assert.True(x.FontSize <= 13));
                }
                using var frame = new RenderTargetBitmap(new PixelSize(900, 600)); frame.Render(window);
                if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1" && Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } path)
                { Directory.CreateDirectory(path); frame.Save(Path.Combine(path, $"save-navigation-{index}-{(dark ? "dark" : "light")}.png"), PngBitmapEncoderOptions.Default); }
                if (index == MainWindowViewModel.InventoryWorkspaceTabIndex)
                {
                    var grid = window.GetVisualDescendants().OfType<DataGrid>().Single(g => g.IsEffectivelyVisible);
                    grid.ScrollIntoView(vm.InventoryEditor!.SelectedPouch!.Items.First(item => item.ItemId == 2592), null);
                    await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                    using var cards = new RenderTargetBitmap(new PixelSize(900, 600)); cards.Render(window);
                    if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE") == "1" && Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } cardPath)
                        cards.Save(Path.Combine(cardPath, $"za-key-cards-{(dark ? "dark" : "light")}.png"), PngBitmapEncoderOptions.Default);
                }
            }
        }
        finally
        {
            services.GetRequiredService<ISaveFileGateway>().CloseSave();
            window?.Close();
            global::Avalonia.Application.Current.RequestedThemeVariant = previous;
        }
    }
}
