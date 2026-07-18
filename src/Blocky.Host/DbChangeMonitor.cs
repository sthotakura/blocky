using Blocky.Core.Data;
using Timer = System.Timers.Timer;

namespace Blocky.Host;

public interface IChangeMonitor : IDisposable
{
    /// <summary>Raised when the rule set may have changed; consumers diff by rev.</summary>
    event Action? Changed;

    void Start();
}

/// <summary>
/// Watches the SQLite database for writes. Under WAL, commits touch blocky.db-wal
/// rather than the main file, so the watcher covers blocky.db* with a debounce.
/// A 30-second poll backstops FileSystemWatcher's known unreliability; spurious
/// signals are cheap because the session suppresses same-rev pushes.
/// </summary>
public sealed class DbChangeMonitor : IChangeMonitor
{
    readonly Timer _debounce = new(300) { AutoReset = false };
    readonly Timer _poll = new(TimeSpan.FromSeconds(30).TotalMilliseconds) { AutoReset = true };
    FileSystemWatcher? _watcher;

    public event Action? Changed;

    public void Start()
    {
        Directory.CreateDirectory(DbPaths.DataDirectory);

        _watcher = new FileSystemWatcher(DbPaths.DataDirectory, "blocky.db*")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
        };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Deleted += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
        _watcher.EnableRaisingEvents = true;

        _debounce.Elapsed += (_, _) => Changed?.Invoke();
        _poll.Elapsed += (_, _) => Changed?.Invoke();
        _poll.Start();
    }

    void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce.Dispose();
        _poll.Dispose();
    }
}
