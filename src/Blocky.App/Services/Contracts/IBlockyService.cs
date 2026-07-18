using Blocky.Core.Data;

namespace Blocky.Services.Contracts;

public interface IBlockyService
{
    /// <summary>
    /// Raised after any rule is added, updated, or removed. Sync to the browser happens
    /// through the database (the host watches it); this event is for UI refresh only.
    /// </summary>
    event Action? RulesChanged;

    Task AddRuleAsync(BlockyRule rule);

    Task UpdateRuleAsync(BlockyRule updatedRule);

    Task RemoveRuleAsync(Guid id);

    ValueTask<BlockyRule?> GetRuleAsync(Guid id);

    Task<List<BlockyRule>> GetAllRulesAsync();

    ValueTask<string[]> GetBlockedDomainsAsync();
}
