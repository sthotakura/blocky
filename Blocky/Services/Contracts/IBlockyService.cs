using Blocky.Data;

namespace Blocky.Services.Contracts;

public interface IBlockyService
{
    Task AddRuleAsync(BlockyRule rule);
    
    Task UpdateRuleAsync(BlockyRule updatedRule);

    Task RemoveRuleAsync(Guid id);

    ValueTask<BlockyRule?> GetRuleAsync(Guid id);

    Task<List<BlockyRule>> GetAllRulesAsync();
    
    ValueTask<bool> IsDomainBlockedAsync(string domain);
    
    ValueTask<string[]> GetBlockedDomainsAsync();
}