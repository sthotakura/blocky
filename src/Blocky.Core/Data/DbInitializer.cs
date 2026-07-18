using Microsoft.EntityFrameworkCore;

namespace Blocky.Core.Data;

/// <summary>
/// Brings the database to the current schema and enables WAL so the WPF app and the
/// native-messaging host can share the file across processes.
/// </summary>
public static class DbInitializer
{
    // Databases created by older app versions used EnsureCreated, which writes no
    // migrations history. Marking the initial migration as applied lets Migrate()
    // take over without trying to re-create the existing Rules table.
    const string BaselineMigrationId = "20260717160213_InitialCreate";
    const string ProductVersion = "10.0.10";

    public static void Initialize(AppDbContext context)
    {
        BaselineIfCreatedWithoutMigrations(context);
        context.Database.Migrate();

        // WAL is persistent per database file; re-running is a no-op.
        context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    }

    static void BaselineIfCreatedWithoutMigrations(AppDbContext context)
    {
        if (!TableExists(context, "Rules") || TableExists(context, "__EFMigrationsHistory"))
        {
            return;
        }

        context.Database.ExecuteSqlRaw(
            """
            CREATE TABLE "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """);
        context.Database.ExecuteSqlRaw(
            $"""
             INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
             VALUES ('{BaselineMigrationId}', '{ProductVersion}');
             """);
    }

    static bool TableExists(AppDbContext context, string tableName)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }
}
