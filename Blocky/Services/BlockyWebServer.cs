using Blocky.Services.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Blocky.Services;

public sealed class BlockyWebServer(
    ILogger<BlockyWebServer> logger,
    ISettingsService settingsService,
    IPacFileProvider pacFileProvider,
    IBlockedPageProvider blockedPageProvider)
    : IBlockyWebServer
{
    IHost? _webHost;

    public async Task StartAsync()
    {
        if (_webHost is not null)
        {
            logger.LogWarning("Blocky web server is already running.");
            return;
        }

        logger.LogInformation("Starting Blocky web server...");

        var settings = await settingsService.GetSettingsAsync();

        _webHost = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls($"http://127.0.0.1:{settings.ProxyPort}");
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/blocky.pac", async context =>
                        {
                            context.Response.ContentType = "application/x-ns-proxy-autoconfig";
                            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                            context.Response.Headers.Pragma = "no-cache";
                            context.Response.Headers["Expires"] = "0";
                            await context.Response.WriteAsync(await pacFileProvider.GetAsync());
                        });

                        endpoints.MapGet("/", async context =>
                        {
                            context.Response.ContentType = "text/html";
                            await context.Response.WriteAsync(await blockedPageProvider.GetAsync());
                        });

                        endpoints.MapFallback(async context =>
                        {
                            context.Response.StatusCode = 404;
                            await context.Response.WriteAsync("Not found");
                        });
                    });
                });
            })
            .Build();

        await _webHost.StartAsync();

        logger.LogInformation("Blocky web server started successfully.");
    }

    public async Task StopAsync()
    {
        if (_webHost is null)
        {
            logger.LogWarning("Blocky web server is not running.");
            return;
        }

        await _webHost.StopAsync();
    }
}