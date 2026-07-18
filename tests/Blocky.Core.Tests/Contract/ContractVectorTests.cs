using System.Text.Json;
using Blocky.Core.Data;
using Blocky.Core.Evaluation;

namespace Blocky.Core.Tests.Contract;

/// <summary>
/// Runs the shared C#/JS contract vectors against the C# ScheduleEvaluator.
/// The same file is loaded by the extension's node tests; if either implementation
/// drifts from the contract, its build breaks.
/// </summary>
[TestFixture]
public class ContractVectorTests
{
    public sealed record VectorSchedule(int StartMinutes, int EndMinutes);

    public sealed record VectorRule(Guid Id, string Domain, bool Enabled, VectorSchedule? Schedule);

    public sealed record VectorExpectation(string[] ActiveDomains, int? NextBoundaryMinutes);

    public sealed record VectorCase(string Name, int NowMinutes, VectorRule[] Rules, VectorExpectation Expected)
    {
        public override string ToString() => Name;
    }

    public sealed record VectorFile(string Description, VectorCase[] Cases);

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEnumerable<VectorCase> LoadCases()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Contract", "time-window-vectors.json");
        var file = JsonSerializer.Deserialize<VectorFile>(File.ReadAllText(path), JsonOptions)!;
        return file.Cases;
    }

    [TestCaseSource(nameof(LoadCases))]
    public void Evaluator_MatchesContractVector(VectorCase vector)
    {
        var rules = vector.Rules.Select(ToBlockyRule).ToArray();
        var now = TimeSpan.FromMinutes(vector.NowMinutes);

        var activeDomains = ScheduleEvaluator.GetActiveDomains(rules, now);
        var nextBoundary = ScheduleEvaluator.GetNextBoundary(rules, now);

        activeDomains.Should().BeEquivalentTo(vector.Expected.ActiveDomains,
            because: "active domains must match the contract");
        (nextBoundary?.TotalMinutes).Should().Be(vector.Expected.NextBoundaryMinutes,
            because: "the next boundary must match the contract");
    }

    static BlockyRule ToBlockyRule(VectorRule rule) => new()
    {
        Id = rule.Id,
        Domain = rule.Domain,
        IsEnabled = rule.Enabled,
        HasTimeRestriction = rule.Schedule is not null,
        StartTime = rule.Schedule is null ? null : TimeSpan.FromMinutes(rule.Schedule.StartMinutes),
        EndTime = rule.Schedule is null ? null : TimeSpan.FromMinutes(rule.Schedule.EndMinutes)
    };
}
