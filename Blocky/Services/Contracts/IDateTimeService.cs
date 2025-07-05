namespace Blocky.Services.Contracts;

public interface IDateTimeService
{
    DateTime Now { get; }
    
    DateTime UtcNow { get; }
}