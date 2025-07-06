using Blocky.Data;
using Blocky.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Blocky.Services;

public sealed class SettingService(IDbContextFactory<AppDbContext> dbContextFactory) : ISettingsService
{
    public async Task<BlockySettings> GetSettingsAsync()
    {
        await using var context = await dbContextFactory.CreateDbContextAsync();
        var settings = await context.Settings.OrderBy(s => s.Id).FirstOrDefaultAsync();
        
        if (settings == null)
        {
            settings = new BlockySettings();
            context.Settings.Add(settings);
            await context.SaveChangesAsync();
        }

        return settings;
    }

    public async Task UpdateSettingsAsync(BlockySettings settings)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync();
        var existingSettings = await context.Settings.FirstOrDefaultAsync();
        if (existingSettings != null)
        {
            if (existingSettings.ProxyPort != settings.ProxyPort)
            {
                existingSettings.ProxyPort = settings.ProxyPort;
                SettingsUpdated?.Invoke(this, settings);
            }
            existingSettings.LastModified = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public event EventHandler<BlockySettings>? SettingsUpdated;
}