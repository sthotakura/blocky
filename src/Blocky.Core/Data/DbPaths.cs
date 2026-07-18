namespace Blocky.Core.Data;

/// <summary>
/// Well-known locations shared by the WPF app and the native-messaging host.
/// The SQLite database is the single source of truth between the two processes.
/// </summary>
public static class DbPaths
{
    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Blocky");

    public static string DatabasePath => Path.Combine(DataDirectory, "blocky.db");
}
