using Blocky.Core.Data;
using Blocky.Core.Protocol;
using Microsoft.EntityFrameworkCore;

namespace Blocky.Host;

/// <summary>
/// Reads the rule set from the shared SQLite database. Opens read-only so the host can
/// never create or alter the database — a missing file means "nothing configured"
/// (dbMissing: true), which the extension treats as an authoritative empty rule set.
/// </summary>
public sealed class DbRulesSource(TimeProvider timeProvider) : IRulesSource
{
    public async Task<RulesMessage> GetCurrentAsync(CancellationToken ct)
    {
        if (!File.Exists(DbPaths.DatabasePath))
        {
            return RulesMessageFactory.Create([], dbMissing: true, timeProvider.GetUtcNow());
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={DbPaths.DatabasePath};Mode=ReadOnly;Pooling=False")
            .Options;

        await using var context = new AppDbContext(options);
        var rules = await context.Rules.AsNoTracking().ToListAsync(ct);

        return RulesMessageFactory.Create(rules, dbMissing: false, timeProvider.GetUtcNow());
    }
}
