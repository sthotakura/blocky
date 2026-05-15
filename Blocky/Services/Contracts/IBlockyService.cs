using Blocky.Data;

namespace Blocky.Services.Contracts;

public interface IBlockyService
{
    /// <summary>
    /// Raised after any rule is added, updated, or removed.
    /// Subscribers (e.g. the WebSocket server) can use this to push changes immediately.
    /// </summary>
    event Action? RulesChanged;

    Task AddRuleAsync(BlockyRule rule);

    Task UpdateRuleAsync(BlockyRule updatedRule);

    Task RemoveRuleAsync(Guid id);

    ValueTask<BlockyRule?> GetRuleAsync(Guid id);

    Task<List<BlockyRule>> GetAllRulesAsync();

    ValueTask<bool> IsDomainBlockedAsync(string domain);

    ValueTask<string[]> GetBlockedDomainsAsync();
}