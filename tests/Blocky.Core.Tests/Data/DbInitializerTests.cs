using Blocky.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Blocky.Core.Tests.Data;

/// <summary>
/// Uses real SQLite files (not the InMemory provider) because migration history,
/// baselining, and WAL are file-level behaviors.
/// </summary>
[TestFixture]
public class DbInitializerTests
{
    private string _dbPath = null!;

    [SetUp]
    public void Setup()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"blocky-test-{Guid.NewGuid():N}.db");
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _dbPath + suffix;
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Filename={_dbPath}")
            .Options);

    [Test]
    public void Initialize_FreshDatabase_CreatesSchemaHistoryAndWal()
    {
        using var context = CreateContext();

        DbInitializer.Initialize(context);

        context.Rules.Count().Should().Be(0);
        context.Database.GetAppliedMigrations().Should().ContainSingle(m => m.EndsWith("_InitialCreate"));
        JournalMode(context).Should().Be("wal");
    }

    [Test]
    public void Initialize_LegacyEnsureCreatedDatabase_BaselinesWithoutDataLoss()
    {
        // Simulate a database created by an older app version via EnsureCreated:
        // schema exists, data exists, no migrations history.
        var ruleId = Guid.NewGuid();
        using (var legacy = CreateContext())
        {
            legacy.Database.EnsureCreated();
            legacy.Rules.Add(new BlockyRule { Id = ruleId, Domain = "example.com" });
            legacy.SaveChanges();
        }

        using var context = CreateContext();

        // Must not throw "table Rules already exists".
        DbInitializer.Initialize(context);

        context.Rules.Single().Id.Should().Be(ruleId);
        context.Database.GetAppliedMigrations().Should().ContainSingle(m => m.EndsWith("_InitialCreate"));
        JournalMode(context).Should().Be("wal");
    }

    [Test]
    public void Initialize_RunTwice_IsIdempotent()
    {
        using (var first = CreateContext())
        {
            DbInitializer.Initialize(first);
        }

        using var context = CreateContext();
        var act = () => DbInitializer.Initialize(context);

        act.Should().NotThrow();
        context.Database.GetAppliedMigrations().Should().ContainSingle(m => m.EndsWith("_InitialCreate"));
    }

    private static string JournalMode(AppDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        return (string)command.ExecuteScalar()!;
    }
}
