using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Blocky.Core.Data;

/// <summary>
/// Used by the dotnet-ef CLI to construct the context when generating migrations.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Filename={DbPaths.DatabasePath}")
            .Options;

        return new AppDbContext(options);
    }
}
