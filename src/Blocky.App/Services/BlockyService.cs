using Blocky.Core.Data;
using Blocky.Core.Evaluation;
using Blocky.Services.Contracts;
using Microsoft.Extensions.Logging;

namespace Blocky.Services;

public sealed class BlockyService(
    ILogger<BlockyService> logger,
    IBlockyRuleRepo repo,
    TimeProvider timeProvider) : IBlockyService
{
    public event Action? RulesChanged;

    public async Task AddRuleAsync(BlockyRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        logger.LogInformation("Adding rule {rule}", rule);

        await repo.AddAsync(rule);
        RulesChanged?.Invoke();
    }

    public async Task UpdateRuleAsync(BlockyRule updatedRule)
    {
        ArgumentNullException.ThrowIfNull(updatedRule);

        logger.LogInformation("Updating rule {rule}", updatedRule);

        await repo.UpdateAsync(updatedRule);
        RulesChanged?.Invoke();
    }

    public async Task RemoveRuleAsync(Guid id)
    {
        if (id.Equals(Guid.Empty)) throw new ArgumentException("Invalid rule id");

        logger.LogInformation("Removing rule with id {id}", id);

        await repo.DeleteAsync(id);
        RulesChanged?.Invoke();
    }

    public ValueTask<BlockyRule?> GetRuleAsync(Guid id)
    {
        if (id.Equals(Guid.Empty)) throw new ArgumentException("Invalid rule id");

        return repo.GetByIdAsync(id);
    }

    public Task<List<BlockyRule>> GetAllRulesAsync() => repo.GetAllRulesAsync();

    public async ValueTask<string[]> GetBlockedDomainsAsync()
    {
        logger.LogInformation("Retrieving all blocked domains");

        var rules = await repo.GetActiveRulesAsync();
        var blockedDomains = ScheduleEvaluator.GetActiveDomains(rules, timeProvider.GetLocalNow().TimeOfDay);

        logger.LogInformation("Found {count} blocked domains", blockedDomains.Length);

        return blockedDomains;
    }
}
