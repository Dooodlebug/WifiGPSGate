using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WifiGPSGate.App.Services;
using WifiGPSGate.App.ViewModels;

namespace WifiGPSGate.App;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private MainViewModel? _mainViewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ConfigureLogging();

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Load settings and apply to view model
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        _mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        _mainViewModel.LoadSettings(settingsService.Load());

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _mainViewModel;
        mainWindow.Show();
    }

    private static void ConfigureLogging()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WifiGPSGate", "Logs");

        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(logDirectory, "wifigpsgate-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        Log.Information("WifiGPSGate starting");
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ILogger>(Log.Logger);
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Save settings on exit
        if (_serviceProvider != null && _mainViewModel != null)
        {
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            settingsService.Save(_mainViewModel.GetCurrentSettings());
        }

        Log.Information("WifiGPSGate shutting down");
        Log.CloseAndFlush();

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnExit(e);
    }
}
