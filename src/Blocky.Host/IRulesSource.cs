using Blocky.Core.Protocol;

namespace Blocky.Host;

public interface IRulesSource
{
    Task<RulesMessage> GetCurrentAsync(CancellationToken ct);
}
