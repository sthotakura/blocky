using Blocky.Data;

namespace Blocky.Services.Contracts;

public interface IBlockyService
{
    bool IsRunning { get; }

    Task StartAsync();

    Task StopAsync();

    Task AddRuleAsync(BlockyRule rule);
    
    Task UpdateRuleAsync(BlockyRule updatedRule);

    Task RemoveRuleAsync(Guid id);

    ValueTask<BlockyRule?> GetRuleAsync(Guid id);

    Task<List<BlockyRule>> GetAllRulesAsync();
}