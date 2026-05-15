using System.Net.WebSockets;
using System.Text.Json;
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
    IBlockyService blockyService)
    : IBlockyWebServer, IDisposable
{
    IHost? _webHost;
    readonly List<WebSocket> _webSocketClients = [];
    readonly SemaphoreSlim _webSocketClientsLock = new(1, 1);

    string[] _lastSent = [];
    readonly Lock _lastSentLock = new();

    PeriodicTimer? _timer;
    CancellationTokenSource? _timerCts;
    Task? _timerTask;

    public async Task StartAsync()
    {
        if (_webHost is not null)
        {
            logger.LogWarning("Blocky web server is already running.");
            return;
        }

        blockyService.RulesChanged += OnRulesChanged;
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
                                    // Reject connections from web-page origins (http:// or https://).
                                    // Chrome extensions send "chrome-extension://" as their Origin,
                                    // and native/test clients typically send no Origin at all.
                                    var origin = context.Request.Headers.Origin.ToString();
                                    if (!string.IsNullOrEmpty(origin) &&
                                        (origin.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                         origin.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                                    {
                                        logger.LogWarning("WebSocket connection rejected: disallowed Origin '{origin}'", origin);
                                        context.Response.StatusCode = 403;
                                        await context.Response.WriteAsync("Forbidden");
                                        return;
                                    }

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

                                    try
                                    {
                                        // Send the latest data immediately upon connection
                                        await UpdateLastSentAsync();
                                        var initialBuffer = GetLastSentBuffer();
                                        await SendWebSocketMessageAsync(webSocket, initialBuffer);

                                        // Keep the socket alive by reading messages until the client closes
                                        var recvBuffer = new byte[4096];
                                        using var ms = new System.IO.MemoryStream();
                                        while (webSocket.State == WebSocketState.Open)
                                        {
                                            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(recvBuffer), CancellationToken.None);

                                            if (result.MessageType == WebSocketMessageType.Close)
                                            {
                                                break;
                                            }

                                            if (result.MessageType == WebSocketMessageType.Text)
                                            {
                                                ms.Write(recvBuffer, 0, result.Count);
                                                if (result.EndOfMessage)
                                                {
                                                    _ = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                                                    ms.SetLength(0);
                                                    // Optional: respond to simple heartbeat messages
                                                    // If the client sends {"type":"ping"}, we can ignore or reply with pong
                                                    // We choose to ignore to reduce chatter; broadcasts are timer-driven
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.LogError(ex, "WebSocket receive loop error");
                                    }
                                    finally
                                    {
                                        await _webSocketClientsLock.WaitAsync();
                                        try
                                        {
                                            _webSocketClients.Remove(webSocket);
                                        }
                                        finally
                                        {
                                            _webSocketClientsLock.Release();
                                        }

                                        try
                                        {
                                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                                        }
                                        catch
                                        {
                                            // ignore
                                        }

                                        logger.LogInformation("WebSocket client disconnected. Total clients: {count}", _webSocketClients.Count);
                                    }
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

        _timerCts = new CancellationTokenSource();
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        _timerTask = RunBroadcastLoopAsync(_timerCts.Token);

        await _webHost.StartAsync();

        logger.LogInformation("Blocky web server started successfully.");
    }

    async Task RunBroadcastLoopAsync(CancellationToken ct)
    {
        try
        {
            while (await _timer!.WaitForNextTickAsync(ct))
            {
                try
                {
                    logger.LogInformation("WebSocket message broadcast timer elapsed");

                    if (_webSocketClients.Count == 0)
                    {
                        logger.LogInformation("No WebSocket clients connected, skipping broadcast");
                        continue;
                    }

                    var changed = await UpdateLastSentAsync();

                    if (!changed)
                    {
                        logger.LogInformation("No changes in blocked domains, skipping broadcast");
                        continue;
                    }

                    await BroadCastLastSentAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during WebSocket message broadcast");
                    // Continue loop even if one iteration fails
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Broadcast timer cancelled");
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

    async Task SendWebSocketMessageAsync(WebSocket client, byte[] buffer)
    {
        try
        {
            await client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true,
                CancellationToken.None);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error sending WebSocket message");
        }
    }

    byte[] GetLastSentBuffer()
    {
        lock (_lastSentLock)
        {
            var message = JsonSerializer.Serialize(_lastSent);
            var buffer = System.Text.Encoding.UTF8.GetBytes(message);
            return buffer;
        }
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

    void OnRulesChanged() => _ = OnRulesChangedAsync();

    async Task OnRulesChangedAsync()
    {
        try
        {
            logger.LogInformation("Rules changed — broadcasting immediately to WebSocket clients");
            await UpdateLastSentAsync();
            await BroadCastLastSentAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error broadcasting immediate rule change");
        }
    }

    public async Task StopAsync()
    {
        if (_webHost is null)
        {
            logger.LogWarning("Blocky web server is not running.");
            return;
        }

        blockyService.RulesChanged -= OnRulesChanged;
        logger.LogInformation("Stopping Blocky web server...");

        // Cancel and wait for timer task
        await _timerCts?.CancelAsync()!;
        _timer?.Dispose();

        if (_timerTask != null)
        {
            try
            {
                await _timerTask;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error stopping broadcast timer");
            }
        }

        await _webHost.StopAsync();
    }

    public void Dispose()
    {
        blockyService.RulesChanged -= OnRulesChanged;

        _webSocketClients.ForEach(client => client.Dispose());
        _webSocketClients.Clear();

        _timerCts?.Cancel();
        _timerCts?.Dispose();
        _timer?.Dispose();

        _webHost?.Dispose();
        _webSocketClientsLock.Dispose();
    }
}