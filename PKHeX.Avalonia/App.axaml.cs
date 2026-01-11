using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PKHeX.Avalonia.Services;
using PKHeX.Avalonia.ViewModels;
using PKHeX.Avalonia.Views;

namespace PKHeX.Avalonia;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Initialize PKHeX Core
        // GameInfo is initialized via AppSettings in ConfigureServices


        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Initialize Config
        var config = AppSettings.Load();
        config.InitializeCore();
        services.AddSingleton(config);

        // Services (Singleton - shared state)
        services.AddSingleton<ISaveFileService, SaveFileService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ISpriteRenderer, AvaloniaSpriteRenderer>();
        services.AddSingleton<ISlotService, SlotService>();
        services.AddSingleton<IClipboardService, ClipboardService>();

        // ViewModels (Transient - created fresh each time)
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<BoxViewerViewModel>();
    }
}
