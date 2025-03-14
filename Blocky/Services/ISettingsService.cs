using Blocky.Data;

namespace Blocky.Services;

public interface ISettingsService
{
    Task<BlockySettings> GetSettingsAsync();

    Task UpdateSettingsAsync(BlockySettings settings);
    
    event EventHandler<BlockySettings> SettingsUpdated;
}