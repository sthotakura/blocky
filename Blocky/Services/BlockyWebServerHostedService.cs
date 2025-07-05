using Blocky.Services.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Blocky.Services;

public sealed class BlockyWebServerHostedService(ILogger<BlockyWebServerHostedService> logger, IBlockyWebServer webServer) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting Blocky web server hosted service...");
        await webServer.StartAsync();
        logger.LogInformation("Blocky web server hosted service started.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping Blocky web server hosted service...");
        await webServer.StopAsync();
        logger.LogInformation("Blocky web server hosted service stopped.");
    }
}