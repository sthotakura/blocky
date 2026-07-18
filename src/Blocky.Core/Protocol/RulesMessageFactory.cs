using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Blocky.Core.Data;

namespace Blocky.Core.Protocol;

public static class RulesMessageFactory
{
    public static RulesMessage Create(IEnumerable<BlockyRule> rules, bool dbMissing, DateTimeOffset generatedAt)
    {
        var dtos = rules
            .Select(ToDto)
            .OrderBy(dto => dto.Id)
            .ToList();

        var payload = new RulesPayload(dtos);

        return new RulesMessage(
            ProtocolConstants.Version,
            ProtocolConstants.RulesMessageType,
            ComputeRev(payload, dbMissing),
            generatedAt,
            dbMissing,
            payload);
    }

    static RuleDto ToDto(BlockyRule rule)
    {
        var hasValidSchedule = rule is { HasTimeRestriction: true, StartTime: not null, EndTime: not null };

        // A time restriction with missing times is inactive today (see ScheduleEvaluator),
        // so it must not degrade into an always-on rule at the protocol level.
        var enabled = rule.IsEnabled && (!rule.HasTimeRestriction || hasValidSchedule);

        return new RuleDto(
            rule.Id,
            rule.Domain,
            enabled,
            hasValidSchedule
                ? new ScheduleDto((int)rule.StartTime!.Value.TotalMinutes, (int)rule.EndTime!.Value.TotalMinutes)
                : null);
    }

    /// <summary>
    /// Content hash of the canonical payload (rules sorted by id, camelCase, no whitespace)
    /// plus the dbMissing flag. Identical content always yields the same rev, so the
    /// extension can skip no-op updates and the host needs no persistent state.
    /// </summary>
    static string ComputeRev(RulesPayload payload, bool dbMissing)
    {
        var canonical = JsonSerializer.Serialize(payload, ProtocolJsonContext.Default.RulesPayload);
        var input = dbMissing ? canonical + "|db-missing" : canonical;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash)[..16];
    }
}
