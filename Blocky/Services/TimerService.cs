using System.Diagnostics;
using Blocky.Services.Contracts;
using Microsoft.Extensions.Logging;
using ITimer = Blocky.Services.Contracts.ITimer;

namespace Blocky.Services;

public class TimerService(ILogger<TimerService> logger) : ITimerService
{
    class AnonymousTimer(ILogger<TimerService> logger, string action) : ITimer
    {
        readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        
        public void Dispose()
        {
            _stopwatch.Stop();
            logger.LogInformation("Action '{Action}' took {Elapsed}", action, _stopwatch.Elapsed);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    public ITimer Create(string action) => new AnonymousTimer(logger, action);
}