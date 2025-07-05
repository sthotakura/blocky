namespace Blocky.Services.Contracts;

public interface IBlockyWebServer
{
    /// <summary>
    /// Starts the Blocky web server.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stops the Blocky web server.
    /// </summary>
    Task StopAsync();
}