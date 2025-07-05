namespace Blocky.Services.Contracts;

public interface ITimerService
{
    ITimer Create(string action);
}