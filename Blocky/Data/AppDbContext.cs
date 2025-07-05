using Microsoft.EntityFrameworkCore;

namespace Blocky.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<BlockyRule> Rules { get; set; }

    public DbSet<BlockySettings> Settings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder
            .Entity<BlockyRule>()
            .HasKey(x => x.Id);

        modelBuilder.Entity<BlockyRule>()
            .Property(e => e.StartTime)
            .HasConversion(v => v.HasValue ? v.Value.TotalMinutes : (double?)null,
                v => v.HasValue ? TimeSpan.FromMinutes(v.Value) : null);

        modelBuilder.Entity<BlockyRule>()
            .Property(e => e.EndTime)
            .HasConversion(v => v.HasValue ? v.Value.TotalMinutes : (double?)null,
                v => v.HasValue ? TimeSpan.FromMinutes(v.Value) : null);
        
        modelBuilder.Entity<BlockySettings>().HasData(new BlockySettings());
    }
}