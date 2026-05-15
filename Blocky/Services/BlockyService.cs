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

    public event Action? RulesChanged;

    public async Task AddRuleAsync(BlockyRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        logger.LogInformation("Adding rule {rule}", rule);

        await _repo.AddAsync(rule);
        RulesChanged?.Invoke();
    }

    public async Task UpdateRuleAsync(BlockyRule updatedRule)
    {
        ArgumentNullException.ThrowIfNull(updatedRule);

        logger.LogInformation("Updating rule {rule}", updatedRule);

        await _repo.UpdateAsync(updatedRule);
        RulesChanged?.Invoke();
    }

    public async Task RemoveRuleAsync(Guid id)
    {
        if (id.Equals(Guid.Empty)) throw new ArgumentException("Invalid rule id");

        logger.LogInformation("Removing rule with id {id}", id);

        await _repo.DeleteAsync(id);
        RulesChanged?.Invoke();
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
            IsSubdomainOrExact(domain, rule.Domain) &&
            (!rule.HasTimeRestriction ||
             (rule is { StartTime: not null, EndTime: not null } &&
              IsWithinTimeWindow(now, rule.StartTime.Value, rule.EndTime.Value))));

        logger.LogInformation("Domain {domain} is {status}", domain, shouldBlock ? "blocked" : "not blocked");

        return shouldBlock;
    }

    public async ValueTask<string[]> GetBlockedDomainsAsync()
    {
        logger.LogInformation("Retrieving all blocked domains");

        var rules = await _repo.GetActiveRulesAsync();
        var now = dateTimeService.Now.TimeOfDay;

        var blockedDomains = rules
            .Where(rule => IsRuleActiveNow(rule, now))
            .Select(rule => rule.Domain)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        logger.LogInformation("Found {count} blocked domains", blockedDomains.Length);

        return blockedDomains;
    }

    static bool IsRuleActiveNow(BlockyRule rule, TimeSpan currentTime)
    {
        // Rule must be enabled and have a valid domain
        if (!rule.IsEnabled || string.IsNullOrWhiteSpace(rule.Domain))
        {
            return false;
        }

        // If no time restriction, rule is always active
        if (!rule.HasTimeRestriction)
        {
            return true;
        }

        // With time restriction, check if we're within the time window
        if (rule.StartTime is null || rule.EndTime is null)
        {
            return false; // Invalid time restriction
        }

        return IsWithinTimeWindow(currentTime, rule.StartTime.Value, rule.EndTime.Value);
    }

    static bool IsWithinTimeWindow(TimeSpan current, TimeSpan start, TimeSpan end) =>
        start <= end
            ? current >= start && current < end
            : current >= start || current < end;

    static bool IsSubdomainOrExact(string host, string ruleDomain)
    {
        // Exact match (e.g. "example.com" matches rule "example.com")
        if (string.Equals(host, ruleDomain, StringComparison.OrdinalIgnoreCase))
            return true;

        // Any subdomain (e.g. "www.example.com", "images.example.com", "a.b.example.com")
        return host.EndsWith($".{ruleDomain}", StringComparison.OrdinalIgnoreCase);
    }
}