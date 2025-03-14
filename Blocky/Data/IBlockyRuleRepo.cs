namespace Blocky.Data;

public interface IBlockyRuleRepo
{
    ValueTask<BlockyRule?> GetByIdAsync(Guid id);

    Task AddAsync(BlockyRule rule);

    Task UpdateAsync(BlockyRule rule);

    Task DeleteAsync(Guid id);

    Task<List<BlockyRule>> GetActiveRulesAsync();

    Task<List<BlockyRule>> GetAllRulesAsync();
}

public interface ICachedBlockyRuleRepo : IBlockyRuleRepo
{
}
