using Blocky.Services.Contracts;

namespace Blocky.Services;

public sealed class DefaultDateTimeService : IDateTimeService
{
    public DateTime Now => DateTime.Now;
    
    public DateTime UtcNow => DateTime.UtcNow;
}