namespace Blocky.Services;

public interface ITimerService
{
    ITimer Create(string action);
}