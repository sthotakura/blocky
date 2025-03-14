using System.IO;
using Blocky.Data;
using Blocky.Services;
using Blocky.ViewModels;
using Blocky.Views;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Titanium.Web.Proxy;

namespace Blocky;

public class Bootstrapper
{
    readonly IServiceCollection _services = new ServiceCollection();
    
    public void Run()
    {
        ConfigureLogging();
        var serviceProvider = ConfigureServices();
        ConfigureDatabase(serviceProvider);
        
        var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
        var mainWindowViewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();
        mainWindow.DataContext = mainWindowViewModel;
        mainWindow.Show();
    }

    void ConfigureLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Blocky",
                    "logs", "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7
            )
            .CreateLogger();
    }
    
    ServiceProvider ConfigureServices()
    {
        _services
            .AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(dispose: true);
            })
            .AddSingleton<AppDbContext>()
            .AddSingleton<ITimerService, TimerService>()
            .AddSingleton<IBlockedPageProvider, BlockedPageProvider>()
            .AddSingleton<IBlockyRuleRepo, BlockyRuleRepo>()
            .AddSingleton<ICachedBlockyRuleRepo, CachedBlockyRuleRepo>()
            .AddSingleton<IDateTimeService, DefaultDateTimeService>()
            .AddSingleton<ProxyServer>()
            .AddSingleton<IBlockyService, BlockyService>()
            .AddSingleton<ISettingsService, SettingService>()
            .AddSingleton<IApplication, DefaultApplication>()
            .AddSingleton<IMessenger, WeakReferenceMessenger>()
            .AddSingleton<SettingsViewModel>()
            .AddSingleton<MainWindowViewModel>()
            .AddSingleton<MainWindow>();

        return _services.BuildServiceProvider();
    }

    static void ConfigureDatabase(ServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.EnsureCreated();
    }
}