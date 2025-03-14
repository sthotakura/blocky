using Blocky.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Blocky.Services;

public sealed class CachedBlockyRuleRepo(ILogger<CachedBlockyRuleRepo> logger, IBlockyRuleRepo repo) : ICachedBlockyRuleRepo, IDisposable
{
    const string AllRulesKey = "all_rules";
    const string RuleKeyPrefix = "rule_";

    readonly MemoryCache _cache = new(new MemoryCacheOptions());
    readonly SemaphoreSlim _cacheLock = new(1, 1);

    public async ValueTask<BlockyRule?> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("Invalid rule id", nameof(id));
        
        var ruleKey = $"{RuleKeyPrefix}{id}";
        var rule = _cache.Get<BlockyRule>(ruleKey);
        if (rule != null) return rule;

        try
        {
            await _cacheLock.WaitAsync();
            rule = _cache.Get<BlockyRule>(ruleKey);
            if (rule != null) return rule;

            rule = await repo.GetByIdAsync(id);
            _cache.Set(ruleKey, rule);
            
            return rule;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting rule {id} from cache", id);
            throw;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task AddAsync(BlockyRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        
        await repo.AddAsync(rule);
        var ruleKey = $"{RuleKeyPrefix}{rule.Id}";

        try
        {
            await _cacheLock.WaitAsync();
            _cache.Set(ruleKey, rule);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding {rule} to cache", rule);
            throw;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task UpdateAsync(BlockyRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        
        await repo.UpdateAsync(rule);
        var ruleKey = $"{RuleKeyPrefix}{rule.Id}";

        try
        {
            await _cacheLock.WaitAsync();
            _cache.Set(ruleKey, rule);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating {rule} to cache", rule);
            throw;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("Invalid rule id", nameof(id));

        await repo.DeleteAsync(id);
        var ruleKey = $"{RuleKeyPrefix}{id}";

        try
        {
            await _cacheLock.WaitAsync();
            _cache.Remove(ruleKey);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting rule {id} from cache", id);
            throw;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task<List<BlockyRule>> GetActiveRulesAsync()
    {
        var rules = await GetRulesAsync();
        return rules.Where(r => r.IsEnabled).ToList();
    }

    public Task<List<BlockyRule>> GetAllRulesAsync() => GetRulesAsync();

    async Task<List<BlockyRule>> GetRulesAsync()
    {
        var rules = _cache.Get<List<BlockyRule>>(AllRulesKey);
        if (rules != null) return rules;

        try
        {
            await _cacheLock.WaitAsync();
            rules = _cache.Get<List<BlockyRule>>(AllRulesKey);
            if (rules != null) return rules;

            rules = await repo.GetAllRulesAsync();
            _cache.Set(AllRulesKey, rules);
            return rules;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting rules from cache");
            throw;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public void Dispose()
    {
        _cache.Dispose();
        _cacheLock.Dispose();
    }
}
