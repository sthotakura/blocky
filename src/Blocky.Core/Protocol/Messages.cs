using System.Text.Json.Serialization;

namespace Blocky.Core.Protocol;

/// <summary>
/// Wire types for the native-messaging channel between the host and the Chrome
/// extension. Serialized as camelCase JSON inside 4-byte little-endian length frames.
/// </summary>
public static class ProtocolConstants
{
    public const int Version = 1;

    public const string RulesMessageType = "rules";
    public const string ErrorMessageType = "error";
    public const string GetRulesRequestType = "get-rules";
}

/// <summary>A schedule in minutes since local midnight; start > end spans midnight.</summary>
public sealed record ScheduleDto(int StartMinutes, int EndMinutes);

public sealed record RuleDto(Guid Id, string Domain, bool Enabled, ScheduleDto? Schedule);

public sealed record RulesPayload(IReadOnlyList<RuleDto> Rules);

public sealed record RulesMessage(
    int V,
    string Type,
    string Rev,
    DateTimeOffset GeneratedAt,
    bool DbMissing,
    RulesPayload Payload);

public sealed record ErrorMessage(int V, string Type, string Code, string Message);

/// <summary>Extension → host request; only the type is meaningful today.</summary>
public sealed record ClientMessage(int V, string? Type);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RulesMessage))]
[JsonSerializable(typeof(ErrorMessage))]
[JsonSerializable(typeof(ClientMessage))]
[JsonSerializable(typeof(RulesPayload))]
public sealed partial class ProtocolJsonContext : JsonSerializerContext;
