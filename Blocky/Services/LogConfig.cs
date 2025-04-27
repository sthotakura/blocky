using System.IO;

namespace Blocky.Services;

public interface ILogConfig
{
    string GetCurrentLogFilePath();
}

public class LogConfig : ILogConfig
{
    static string GetLogBaseDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Blocky", "logs");
    }

    public static string GetLogFilePattern() => Path.Combine(GetLogBaseDirectory(), "log-.log");

    public string GetCurrentLogFilePath()
    {
        var todayLogFile = Path.Combine(
            GetLogBaseDirectory(), 
            $"log-{DateTime.Now:yyyyMMdd}.log");
            
        if (File.Exists(todayLogFile))
        {
            return todayLogFile;
        }
        
        // If today's log doesn't exist yet, try to find the most recent log file
        if (Directory.Exists(GetLogBaseDirectory()))
        {
            var logFiles = Directory.GetFiles(GetLogBaseDirectory(), "log-*.log")
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .ToArray();
                
            if (logFiles.Length > 0)
            {
                return logFiles[0];
            }
        }
        
        // Return the expected path even if it doesn't exist yet
        return todayLogFile;
    }
}
