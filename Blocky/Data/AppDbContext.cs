using System.IO;
using Microsoft.EntityFrameworkCore;

namespace Blocky.Data;

public class AppDbContext : DbContext
{
    readonly string _dbPath;
    
    public DbSet<BlockyRule> Rules { get; set; }

    public DbSet<BlockySettings> Settings { get; set; }
    
    public AppDbContext()
    {
        const Environment.SpecialFolder folder = Environment.SpecialFolder.LocalApplicationData;
        var folderPath = Environment.GetFolderPath(folder);
        _dbPath = Path.Join(folderPath, "Blocky", "blocky.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        EnsureDatabaseCreated();
        
        optionsBuilder.UseSqlite($"Filename={_dbPath}");
    }

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

    void EnsureDatabaseCreated()
    {
        if (File.Exists(_dbPath))
        {
            return;
        }
        
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        File.Create(_dbPath).Close();
    }
}