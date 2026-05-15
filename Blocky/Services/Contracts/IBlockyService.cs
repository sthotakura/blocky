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

    /// <summary>
    /// Checks whether a specific domain is currently blocked (respects time windows).
    /// Not used in the main Chrome blocking flow — useful for diagnostics and future UI queries.
    /// </summary>
    ValueTask<bool> IsDomainBlockedAsync(string domain);

    ValueTask<string[]> GetBlockedDomainsAsync();
}