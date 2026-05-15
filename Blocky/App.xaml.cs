using System.IO;
using System.Net.Sockets;
using System.Windows;
using Blocky.Data;
using Blocky.Services;
using Blocky.Services.Contracts;
using Blocky.ViewModels;
using Blocky.Views;
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
                    .AddSingleton<IApplication, DefaultApplication>()
                    .AddSingleton<ILogConfig, LogConfig>()
                    .AddSingleton<MainWindowViewModel>()
                    .AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _host.Start();
        }
        catch (Exception ex) when (IsPortInUse(ex))
        {
            MessageBox.Show(
                $"Blocky could not start because port {BlockyWebServer.Port} is already in use by another application.\n\nPlease close the conflicting application and restart Blocky.",
                "Port Conflict",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        var serviceProvider = _host.Services;
        ConfigureDatabase(serviceProvider);

        var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
        var mainWindowViewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();
        mainWindow.DataContext = mainWindowViewModel;
        mainWindow.Show();
        mainWindow.WindowState = WindowState.Minimized;
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while stopping the application");
            await Log.CloseAndFlushAsync();
        }
        finally
        {
            base.OnExit(e);
        }
    }

    static void ConfigureDatabase(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        using var context = contextFactory.CreateDbContext();
        context.Database.EnsureCreated();
    }

    static bool IsPortInUse(Exception ex)
    {
        var e = ex;
        while (e != null)
        {
            if (e is SocketException se && se.SocketErrorCode == SocketError.AddressAlreadyInUse)
                return true;
            e = e.InnerException;
        }
        return false;
    }
}
