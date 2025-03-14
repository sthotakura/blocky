namespace Blocky.Services;

public interface IDateTimeService
{
    DateTime Now { get; }
    
    DateTime UtcNow { get; }
}