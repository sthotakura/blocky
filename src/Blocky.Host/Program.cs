using Blocky.Core.Data;
using Blocky.Host;
using Serilog;

// stdout belongs exclusively to the native-messaging protocol — all diagnostics go to a file.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        Path.Combine(DbPaths.DataDirectory, "logs", "Blocky.Host-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Log.Information("Blocky host starting (pid {Pid})", Environment.ProcessId);

    await using var stdin = Console.OpenStandardInput();
    await using var stdout = Console.OpenStandardOutput();
    using var monitor = new DbChangeMonitor();

    var session = new HostSession(new DbRulesSource(TimeProvider.System), Log.Logger);
    await session.RunAsync(stdin, stdout, monitor, CancellationToken.None);

    Log.Information("Blocky host exiting");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Blocky host crashed");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
