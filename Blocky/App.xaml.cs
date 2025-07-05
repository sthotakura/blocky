using System.IO;
using System.Windows;
using Blocky.Data;
using Blocky.Services;
using Blocky.Services.Contracts;
using Blocky.ViewModels;
using Blocky.Views;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Blocky;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    readonly IHost _host;

    public App()
    {
        InitializeComponent();

        _host = CreateHost();
    }

    static IHost CreateHost()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .CreateLogger();

        return Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                services
                    .AddLogging(builder =>
                    {
                        builder.ClearProviders();
                        builder.AddSerilog(dispose: true);
                    })
                    .AddDbContextFactory<AppDbContext>(options =>
                    {
                        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        var dbPath = Path.Combine(appData, "Blocky", "blocky.db");

                        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!); // ensure folder exists

                        options.UseSqlite($"Filename={dbPath}");
                    })
                    .AddSingleton<ITimerService, TimerService>()
                    .AddSingleton<IBlockyRuleRepo, BlockyRuleRepo>()
                    .AddSingleton<ICachedBlockyRuleRepo, CachedBlockyRuleRepo>()
                    .AddSingleton<IDateTimeService, DefaultDateTimeService>()
                    .AddSingleton<IBlockyWebServer, BlockyWebServer>()
                    .AddHostedService<BlockyWebServerHostedService>()
                    .AddSingleton<IBlockyService, BlockyService>()
                    .AddSingleton<ISettingsService, SettingService>()
                    .AddSingleton<IApplication, DefaultApplication>()
                    .AddSingleton<ILogConfig, LogConfig>()
                    .AddSingleton<IMessenger, WeakReferenceMessenger>()
                    .AddSingleton<SettingsViewModel>()
                    .AddSingleton<MainWindowViewModel>()
                    .AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _host.Start();

        var serviceProvider = _host.Services;
        ConfigureDatabase(serviceProvider);

        var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
        var mainWindowViewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();
        mainWindow.DataContext = mainWindowViewModel;
        mainWindow.Show();
        mainWindow.WindowState = WindowState.Minimized;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.StopAsync().GetAwaiter().GetResult();
        _host.Dispose();
        Log.CloseAndFlush();

        base.OnExit(e);
    }

    static void ConfigureDatabase(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        
        using var context = contextFactory.CreateDbContext();
        context.Database.EnsureCreated();
    }
}