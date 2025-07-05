using Blocky.Data;
using Blocky.Services.Contracts;
using Microsoft.Extensions.Logging;

namespace Blocky.Services;

public sealed class BlockyService(
    ILogger<BlockyService> logger,
    ICachedBlockyRuleRepo repo,
    IDateTimeService dateTimeService) : IBlockyService
{
    readonly IBlockyRuleRepo _repo = repo;

    public Task AddRuleAsync(BlockyRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        logger.LogInformation("Adding rule {rule}", rule);

        return _repo.AddAsync(rule);
    }

    public Task UpdateRuleAsync(BlockyRule updatedRule)
    {
        ArgumentNullException.ThrowIfNull(updatedRule);

        logger.LogInformation("Updating rule {rule}", updatedRule);

        return _repo.UpdateAsync(updatedRule);
    }

    public Task RemoveRuleAsync(Guid id)
    {
        if (id.Equals(Guid.Empty)) throw new ArgumentException("Invalid rule id");

        logger.LogInformation("Removing rule with id {id}", id);

        return _repo.DeleteAsync(id);
    }

    public ValueTask<BlockyRule?> GetRuleAsync(Guid id)
    {
        if (id.Equals(Guid.Empty)) throw new ArgumentException("Invalid rule id");

        return _repo.GetByIdAsync(id);
    }

    public Task<List<BlockyRule>> GetAllRulesAsync() => _repo.GetAllRulesAsync();

    public async ValueTask<bool> IsDomainBlockedAsync(string domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        if (string.IsNullOrWhiteSpace(domain)) throw new ArgumentException("Domain cannot be null or empty");

        logger.LogInformation("Checking if domain {domain} is blocked", domain);

        var now = dateTimeService.Now.TimeOfDay;
        var rules = await _repo.GetActiveRulesAsync();

        var shouldBlock = rules.Any(rule =>
            IsExactOrWwwOnly(domain, rule.Domain) &&
            (!rule.HasTimeRestriction ||
             (rule is { StartTime: not null, EndTime: not null } &&
              now >= rule.StartTime.Value && now < rule.EndTime.Value)));

        logger.LogInformation("Domain {domain} is {status}", domain, shouldBlock ? "blocked" : "not blocked");

        return shouldBlock;
    }

    public async ValueTask<string[]> GetBlockedDomainsAsync()
    {
        logger.LogInformation("Retrieving all blocked domains");

        var rules = await _repo.GetActiveRulesAsync();

        var now = dateTimeService.Now.TimeOfDay;
        var blockedDomains = rules
            .Where(rule => rule.IsEnabled && !string.IsNullOrWhiteSpace(rule.Domain) && ((!rule.HasTimeRestriction ||
                (rule is { StartTime: not null, EndTime: not null } &&
                 now >= rule.StartTime.Value && now < rule.EndTime.Value))))
            .Select(rule => rule.Domain)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        logger.LogInformation("Found {count} blocked domains", blockedDomains.Length);

        return blockedDomains;
    }

    static bool IsExactOrWwwOnly(string host, string ruleDomain)
    {
        return string.Equals(host, ruleDomain, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(host, $"www.{ruleDomain}", StringComparison.OrdinalIgnoreCase);
    }
}