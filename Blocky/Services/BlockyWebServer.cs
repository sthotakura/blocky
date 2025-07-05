using System.Net.WebSockets;
using System.Text.Json;
using System.Timers;
using Blocky.Services.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace Blocky.Services;

public sealed class BlockyWebServer(
    ILogger<BlockyWebServer> logger,
    ISettingsService settingsService,
    IBlockyService blockyService)
    : IBlockyWebServer
{
    IHost? _webHost;
    readonly List<WebSocket> _webSocketClients = [];
    readonly SemaphoreSlim _webSocketClientsLock = new(1, 1);

    string[] _lastSent = [];
    readonly Lock _lastSentLock = new();

    Timer? _timer;

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
                webBuilder.UseUrls($"http://localhost:{settings.ProxyPort}");
                webBuilder.Configure(app =>
                {
                    app
                        .UseRouting()
                        .UseWebSockets()
                        .UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/blocked-domains", async context =>
                            {
                                var blockedDomains = await blockyService.GetBlockedDomainsAsync();
                                context.Response.ContentType = "application/json";
                                lock (_lastSentLock)
                                {
                                    _lastSent = blockedDomains;
                                }

                                await context.Response.WriteAsJsonAsync(blockedDomains);
                            });

                            endpoints.MapGet("/", async context =>
                            {
                                context.Response.ContentType = "text/html";
                                await context.Response.WriteAsync("Blocky Web Server is running.");
                            });

                            endpoints.Map("/ws", async context =>
                            {
                                if (context.WebSockets.IsWebSocketRequest)
                                {
                                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                                    await _webSocketClientsLock.WaitAsync();
                                    try
                                    {
                                        _webSocketClients.Add(webSocket);
                                    }
                                    finally
                                    {
                                        _webSocketClientsLock.Release();
                                    }

                                    logger.LogInformation("New WebSocket client connected. Total clients: {count}",
                                        _webSocketClients.Count);
                                    await UpdateLastSentAsync();
                                    var buffer = GetLastSentBuffer();
                                    await SendWebSocketMessageAsync(webSocket, buffer);
                                }
                                else
                                {
                                    context.Response.StatusCode = 400;
                                    await context.Response.WriteAsync("WebSocket request expected.");
                                }
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

        _timer = new Timer(30000); // 15 minutes
        _timer.Elapsed += OnTimerOnElapsed;
        _timer.Start();

        await _webHost.StartAsync();

        logger.LogInformation("Blocky web server started successfully.");
    }

    async void OnTimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            if (_webSocketClients.Count == 0)
            {
                return;
            }

            var changed = await UpdateLastSentAsync();

            if (!changed)
            {
                return;
            }

            await BroadCastLastSentAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during WebSocket message broadcast");
        }
        finally
        {
            _timer?.Start(); // Restart the timer for the next interval
        }
    }

    async Task BroadCastLastSentAsync()
    {
        var buffer = GetLastSentBuffer();

        logger.LogInformation("Broadcasting last sent data to {count} WebSocket clients", _webSocketClients.Count);

        await _webSocketClientsLock.WaitAsync();
        try
        {
            _webSocketClients.RemoveAll(client => client.State is WebSocketState.Closed or WebSocketState.Aborted);

            foreach (var client in _webSocketClients.Where(client => client.State == WebSocketState.Open))
            {
                await SendWebSocketMessageAsync(client, buffer);
            }
        }
        finally
        {
            _webSocketClientsLock.Release();
        }
    }

    static async Task SendWebSocketMessageAsync(WebSocket client, byte[] buffer)
    {
        await client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true,
            CancellationToken.None);
    }

    byte[] GetLastSentBuffer()
    {
        var message = JsonSerializer.Serialize(_lastSent);
        var buffer = System.Text.Encoding.UTF8.GetBytes(message);
        return buffer;
    }

    async Task<bool> UpdateLastSentAsync()
    {
        var blockedDomains = await blockyService.GetBlockedDomainsAsync();
        bool changed;

        lock (_lastSentLock)
        {
            changed = !_lastSent.SequenceEqual(blockedDomains);
            if (changed)
            {
                _lastSent = blockedDomains;
            }
        }

        return changed;
    }

    public async Task StopAsync()
    {
        if (_webHost is null)
        {
            logger.LogWarning("Blocky web server is not running.");
            return;
        }

        logger.LogInformation("Stopping Blocky web server...");

        if(_timer is not null)
        {
            _timer.Stop();
            _timer.Dispose();
        }
        
        await _webHost.StopAsync();
    }
}