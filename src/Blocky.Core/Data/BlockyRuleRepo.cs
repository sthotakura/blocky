using Microsoft.EntityFrameworkCore;

namespace Blocky.Core.Data;

public sealed class BlockyRuleRepo(IDbContextFactory<AppDbContext> dbContextFactory) : IBlockyRuleRepo
{
    public async Task<List<BlockyRule>> GetAllRulesAsync()
    {
        await using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Rules.ToListAsync();
    }

    public async ValueTask<BlockyRule?> GetByIdAsync(Guid id)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Rules.FindAsync(id);
    }

    public async Task AddAsync(BlockyRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        await using var context = await dbContextFactory.CreateDbContextAsync();
        context.Rules.Add(rule);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(BlockyRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        await using var context = await dbContextFactory.CreateDbContextAsync();
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

        await using var context = await dbContextFactory.CreateDbContextAsync();
        var existing = await context.Rules.FindAsync(id);
        if (existing == null)
        {
            throw new KeyNotFoundException($"Rule not found with id: {id}");
        }

        context.Rules.Remove(existing);
        await context.SaveChangesAsync();
    }

    public async Task<List<BlockyRule>> GetActiveRulesAsync()
    {
        await using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Rules.Where(r => r.IsEnabled).ToListAsync();
    }
}
