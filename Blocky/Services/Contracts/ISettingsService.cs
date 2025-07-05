using Blocky.Data;

namespace Blocky.Services.Contracts;

public interface ISettingsService
{
    Task<BlockySettings> GetSettingsAsync();

    Task UpdateSettingsAsync(BlockySettings settings);
    
    event EventHandler<BlockySettings> SettingsUpdated;
}