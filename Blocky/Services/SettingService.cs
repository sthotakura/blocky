using Blocky.Data;
using Microsoft.EntityFrameworkCore;

namespace Blocky.Services;

public sealed class SettingService(AppDbContext context) : ISettingsService
{
    public async Task<BlockySettings> GetSettingsAsync()
    {
        var settings = await context.Settings.FirstOrDefaultAsync();
        
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