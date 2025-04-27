using Blocky.Data;
using Microsoft.Extensions.Logging;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;
using Titanium.Web.Proxy.Network;

namespace Blocky.Services;

public sealed class BlockyService : IBlockyService, IAsyncDisposable
{
    readonly ILogger<BlockyService> _logger;
    readonly IBlockyRuleRepo _repo;
    readonly IBlockedPageProvider _blockedPageProvider;
    readonly IDateTimeService _dateTimeService;
    readonly ISettingsService _settingsService;
    readonly ProxyServer _proxyServer;
    readonly Task _initializeTask;

    ExplicitProxyEndPoint? _endPoint;

    public BlockyService(ILogger<BlockyService> logger, ICachedBlockyRuleRepo repo,
        IBlockedPageProvider blockedPageProvider, IDateTimeService dateTimeService,
        ISettingsService settingsService, ProxyServer proxyServer)
    {
        _logger = logger;
        _repo = repo;
        _blockedPageProvider = blockedPageProvider;
        _dateTimeService = dateTimeService;
        _settingsService = settingsService;
        _proxyServer = proxyServer;

        _proxyServer.BeforeRequest += OnBeforeRequest;
        _settingsService.SettingsUpdated += OnSettingsChanged;

        _initializeTask = InitializeAsync();
    }

    async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Blocky service from saved state . . .");

        _proxyServer.CertificateManager.CertificateEngine = CertificateEngine.DefaultWindows;
        _proxyServer.CertificateManager.EnsureRootCertificate();

        var settings = await _settingsService.GetSettingsAsync();
        if (settings.IsRunning)
        {
            await StartImplAsync();
        }
    }

    async void OnSettingsChanged(object? sender, BlockySettings e)
    {
        try
        {
            if (!IsRunning) return;

            _logger.LogInformation("Settings changed, restarting Blocky service . . .");

            await StopAsync();
            await Task.Delay(1000);
            await StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting Blocky service");
        }
    }

    public bool IsRunning { get; private set; }

    public async Task StartAsync()
    {
        await _initializeTask;

        await StartImplAsync();
    }

    async Task StartImplAsync()
    {
        if (IsRunning) return;

        _logger.LogInformation("Starting Blocky service");

        var settings = await _settingsService.GetSettingsAsync();

        StartProxyServer(settings);
        SetSystemProxy("127.0.0.1", settings.ProxyPort);

        IsRunning = true;
        await SetRunningStateAsync();
    }

    void StartProxyServer(BlockySettings settings)
    {
        if (_endPoint == null)
        {
            _endPoint = new ExplicitProxyEndPoint(System.Net.IPAddress.Any, settings.ProxyPort);
            _proxyServer.AddEndPoint(_endPoint);
        }

        _proxyServer.Start();
    }

    public async Task StopAsync()
    {
        await _initializeTask;

        await StopImplAsync();
    }

    async Task StopImplAsync()
    {
        if (!IsRunning) return;

        _logger.LogInformation("Stopping Blocky service");

        if (_endPoint != null)
        {
            _proxyServer.RemoveEndPoint(_endPoint);
            _endPoint = null;
        }

        _proxyServer.Stop();
        ClearSystemProxy();

        IsRunning = false;
        await SetRunningStateAsync();
    }

    async Task SetRunningStateAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        settings.IsRunning = IsRunning;
        await _settingsService.UpdateSettingsAsync(settings);
    }

    public Task AddRuleAsync(BlockyRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        _logger.LogInformation("Adding rule {rule}", rule);

        return _repo.AddAsync(rule);
    }
    
    public Task UpdateRuleAsync(BlockyRule updatedRule)
    {
        ArgumentNullException.ThrowIfNull(updatedRule);

        _logger.LogInformation("Updating rule {rule}", updatedRule);

        return _repo.UpdateAsync(updatedRule);
    }

    public Task RemoveRuleAsync(Guid id)
    {
        if (id.Equals(Guid.Empty)) throw new ArgumentException("Invalid rule id");

        _logger.LogInformation("Removing rule with id {id}", id);

        return _repo.DeleteAsync(id);
    }

    public ValueTask<BlockyRule?> GetRuleAsync(Guid id)
    {
        if (id.Equals(Guid.Empty)) throw new ArgumentException("Invalid rule id");

        return _repo.GetByIdAsync(id);
    }

    public Task<List<BlockyRule>> GetAllRulesAsync() => _repo.GetAllRulesAsync();

    async Task OnBeforeRequest(object sender, SessionEventArgs e)
    {
        try
        {
            if (!IsRunning)
            {
                return;
            }

            var url = e.HttpClient.Request.RequestUri;
            var host = url.Host.ToLowerInvariant();

            // Only log at debug level to avoid excessive logging in production
            _logger.LogDebug("Checking Host: {Host} URL: {Url}", host, url);

            var activeRules = await _repo.GetActiveRulesAsync();
            
            foreach (var rule in activeRules)
            {
                var domain = rule.Domain.ToLowerInvariant();
                
                // More precise domain matching
                if (host == domain || host.EndsWith($".{domain}"))
                {
                    if (rule is { HasTimeRestriction: true, StartTime: not null, EndTime: not null })
                    {
                        var now = _dateTimeService.Now.TimeOfDay;
                        
                        var isWithinTimeRestriction = rule.StartTime <= rule.EndTime
                            ? now >= rule.StartTime && now <= rule.EndTime
                            : now >= rule.StartTime || now <= rule.EndTime;
                        
                        if (isWithinTimeRestriction)
                        {
                            _logger.LogInformation("Time-restricted block applied to {Domain} at {Time}", 
                                host, _dateTimeService.Now.TimeOfDay);
                            await BlockRequestAsync(e);
                            return;
                        }
                    }
                    else
                    {
                        // No time restriction, always block
                        _logger.LogInformation("Block applied to {Domain}", host);
                        await BlockRequestAsync(e);
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request to {Url}", e.HttpClient.Request.RequestUri);
        }
    }

    async Task BlockRequestAsync(SessionEventArgs e)
    {
        _logger.LogInformation("Blocking {Url}", e.HttpClient.Request.RequestUri.ToString());
        e.Ok(await _blockedPageProvider.GetAsync());
    }

    void SetSystemProxy(string host, int port)
    {
        var internetSettingsKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings",
            true);

        if (internetSettingsKey == null)
        {
            _logger.LogError("Could not find internet settings key");
            return;
        }

        _logger.LogInformation("Setting system proxy to {host}:{port}", host, port);

        internetSettingsKey.SetValue("ProxyServer", $"{host}:{port}");
        internetSettingsKey.SetValue("ProxyEnable", 1);
    }

    void ClearSystemProxy()
    {
        _logger.LogInformation("Clearing system proxy settings");

        Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings",
            true)?.SetValue("ProxyEnable", 0);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _proxyServer.Dispose();
    }
}
