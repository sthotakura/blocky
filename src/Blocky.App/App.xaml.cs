using System.IO;
using System.Windows;
using Blocky.Core.Data;
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
                        Directory.CreateDirectory(DbPaths.DataDirectory);
                        options.UseSqlite($"Filename={DbPaths.DatabasePath}");
                    })
                    .AddSingleton(TimeProvider.System)
                    .AddSingleton<IBlockyRuleRepo, BlockyRuleRepo>()
                    .AddSingleton<IBlockyService, BlockyService>()
                    .AddSingleton<IApplication, DefaultApplication>()
                    .AddSingleton<IDialogService, DialogService>()
                    .AddSingleton<IShellService, ShellService>()
                    .AddSingleton<ILogConfig, LogConfig>()
                    .AddSingleton<MainWindowViewModel>()
                    .AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // The database must exist before anything reads it — including the native
        // messaging host, which treats a missing file as "nothing configured".
        ConfigureDatabase(_host.Services);

        _host.Start();

        var serviceProvider = _host.Services;
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
        DbInitializer.Initialize(context);
    }
}
