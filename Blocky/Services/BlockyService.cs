using Blocky.Data;
using Blocky.Services.Contracts;
using Microsoft.Extensions.Logging;

namespace Blocky.Services;

public sealed class BlockyService : IBlockyService, IAsyncDisposable
{
    readonly ILogger<BlockyService> _logger;
    readonly IBlockyRuleRepo _repo;
    readonly ISettingsService _settingsService;
    readonly Task _initializeTask;

    public BlockyService(ILogger<BlockyService> logger, ICachedBlockyRuleRepo repo,
        ISettingsService settingsService)
    {
        _logger = logger;
        _repo = repo;
        _settingsService = settingsService;
        _settingsService.SettingsUpdated += OnSettingsChanged;

        _initializeTask = InitializeAsync();
    }

    async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Blocky service from saved state . . .");

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

        SetSystemProxy("127.0.0.1", settings.ProxyPort);

        IsRunning = true;
        await SetRunningStateAsync();
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

        var pacUrl = $"http://{host}:{port}/blocky.pac?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        _logger.LogInformation("Setting system proxy to {pacUrl}", pacUrl);

        internetSettingsKey.SetValue("AutoConfigURL", pacUrl);
        internetSettingsKey.SetValue("ProxyEnable", 0);
    }

    void ClearSystemProxy()
    {
        _logger.LogInformation("Clearing system proxy settings");

        var internetSettingsKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings",
            true);

        if (internetSettingsKey == null)
        {
            _logger.LogError("Could not find internet settings key");
            return;
        }

        internetSettingsKey.DeleteValue("AutoConfigURL", false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}