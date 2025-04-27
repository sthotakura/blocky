using Blocky.Services;
using Microsoft.EntityFrameworkCore;

namespace Blocky.Data;

public sealed class BlockyRuleRepo(AppDbContext context, ITimerService timerService) : IBlockyRuleRepo
{
    public Task<List<BlockyRule>> GetAllRulesAsync()
    {
        using var timer = timerService.Create(nameof(GetAllRulesAsync));
        return context.Rules.ToListAsync();
    }

    public ValueTask<BlockyRule?> GetByIdAsync(Guid id)
    {
        using var timer = timerService.Create($"GetByIdAsync/{id}");
        return context.Rules.FindAsync(id);
    }

    public Task AddAsync(BlockyRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        using var timer = timerService.Create($"Add rule: {rule}");
        context.Rules.Add(rule);
        return context.SaveChangesAsync();
    }

    public async Task UpdateAsync(BlockyRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        await using var timer = timerService.Create($"Update rule: {rule}");
        
        var existing = await context.Rules.FindAsync(rule.Id);
        if (existing == null)
        {
            throw new KeyNotFoundException($"Rule not found with id: {rule.Id}");
        }

        rule.LastUpdated = DateTime.UtcNow;
        context.Entry(existing).CurrentValues.SetValues(rule);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        if (id.Equals(Guid.Empty)) throw new ArgumentException("Invalid rule id");

        await using var timer = timerService.Create($"Delete Rule: {id}");
        
        var existing = await context.Rules.FindAsync(id);
        if (existing == null)
        {
            throw new KeyNotFoundException($"Rule not found with id: {id}");
        }

        context.Rules.Remove(existing);
        await context.SaveChangesAsync();
    }

    public Task<List<BlockyRule>> GetActiveRulesAsync() => context.Rules.Where(r => r.IsEnabled).ToListAsync();
}