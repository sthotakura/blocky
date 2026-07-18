using Blocky.Core.Data;
using Blocky.Core.Evaluation;

namespace Blocky.Core.Tests.Evaluation;

[TestFixture]
public class ScheduleEvaluatorTests
{
    #region IsRuleActive

    [Test]
    public void IsRuleActive_NoTimeRestriction_IsAlwaysActive()
    {
        var rule = Rule("example.com");

        ScheduleEvaluator.IsRuleActive(rule, TimeSpan.FromHours(3)).Should().BeTrue();
        ScheduleEvaluator.IsRuleActive(rule, TimeSpan.FromHours(23)).Should().BeTrue();
    }

    [Test]
    public void IsRuleActive_DisabledRule_IsNeverActive()
    {
        var rule = Rule("example.com", isEnabled: false);

        ScheduleEvaluator.IsRuleActive(rule, TimeSpan.FromHours(10)).Should().BeFalse();
    }

    [Test]
    public void IsRuleActive_EmptyDomain_IsNeverActive()
    {
        var rule = Rule("   ");

        ScheduleEvaluator.IsRuleActive(rule, TimeSpan.FromHours(10)).Should().BeFalse();
    }

    [Test]
    public void IsRuleActive_TimeRestrictionWithMissingTimes_IsInactive()
    {
        var rule = Rule("example.com");
        rule.HasTimeRestriction = true;
        rule.StartTime = TimeSpan.FromHours(8);
        rule.EndTime = null;

        ScheduleEvaluator.IsRuleActive(rule, TimeSpan.FromHours(10)).Should().BeFalse();
    }

    [Test]
    public void IsRuleActive_WithinWindow_IsActive()
    {
        var rule = ScheduledRule("example.com", TimeSpan.FromHours(8), TimeSpan.FromHours(17));

        ScheduleEvaluator.IsRuleActive(rule, TimeSpan.FromHours(10)).Should().BeTrue();
    }

    [Test]
    public void IsRuleActive_BeforeWindow_IsInactive()
    {
        var rule = ScheduledRule("example.com", TimeSpan.FromHours(8), TimeSpan.FromHours(17));

        ScheduleEvaluator.IsRuleActive(rule, TimeSpan.FromHours(7)).Should().BeFalse();
    }

    [Test]
    public void IsRuleActive_AfterWindow_IsInactive()
    {
        var rule = ScheduledRule("example.com", TimeSpan.FromHours(8), TimeSpan.FromHours(17));

        ScheduleEvaluator.IsRuleActive(rule, TimeSpan.FromHours(18)).Should().BeFalse();
    }

    [Test]
    public void IsRuleActive_AtStartTime_IsActive()
    {
        var rule = ScheduledRule("example.com", TimeSpan.FromHours(8), TimeSpan.FromHours(17));

        ScheduleEvaluator.IsRuleActive(rule, TimeSpan.FromHours(8)).Should().BeTrue();
    }

    [Test]
    public void IsRuleActive_AtEndTime_IsInactive()
    {
        var rule = ScheduledRule("example.com", TimeSpan.FromHours(8), TimeSpan.FromHours(17));

        ScheduleEvaluator.IsRuleActive(rule, TimeSpan.FromHours(17)).Should().BeFalse();
    }

    [Test]
    public void IsRuleActive_MidnightSpanning_EveningPortion_IsActive()
    {
        var rule = ScheduledRule("example.com", TimeSpan.FromHours(22), TimeSpan.FromHours(6));

        ScheduleEvaluator.IsRuleActive(rule, TimeSpan.FromHours(23)).Should().BeTrue();
    }

    [Test]
    public void IsRuleActive_MidnightSpanning_MorningPortion_IsActive()
    {
        var rule = ScheduledRule("example.com", TimeSpan.FromHours(22), TimeSpan.FromHours(6));

        ScheduleEvaluator.IsRuleActive(rule, TimeSpan.FromHours(3)).Should().BeTrue();
    }

    [Test]
    public void IsRuleActive_MidnightSpanning_OutsideWindow_IsInactive()
    {
        var rule = ScheduledRule("example.com", TimeSpan.FromHours(22), TimeSpan.FromHours(6));

        ScheduleEvaluator.IsRuleActive(rule, TimeSpan.FromHours(10)).Should().BeFalse();
    }

    [Test]
    public void IsRuleActive_MidnightSpanning_AtStartTime_IsActive()
    {
        var rule = ScheduledRule("example.com", TimeSpan.FromHours(22), TimeSpan.FromHours(6));

        ScheduleEvaluator.IsRuleActive(rule, TimeSpan.FromHours(22)).Should().BeTrue();
    }

    [Test]
    public void IsRuleActive_MidnightSpanning_AtEndTime_IsInactive()
    {
        var rule = ScheduledRule("example.com", TimeSpan.FromHours(22), TimeSpan.FromHours(6));

        ScheduleEvaluator.IsRuleActive(rule, TimeSpan.FromHours(6)).Should().BeFalse();
    }

    #endregion

    #region GetActiveDomains

    [Test]
    public void GetActiveDomains_MixedRules_ReturnsOnlyActiveOnes()
    {
        var rules = new[]
        {
            Rule("always.com"),
            Rule("disabled.com", isEnabled: false),
            ScheduledRule("workhours.com", TimeSpan.FromHours(8), TimeSpan.FromHours(17)),
            ScheduledRule("night.com", TimeSpan.FromHours(22), TimeSpan.FromHours(6))
        };

        var active = ScheduleEvaluator.GetActiveDomains(rules, TimeSpan.FromHours(10));

        active.Should().BeEquivalentTo("always.com", "workhours.com");
    }

    [Test]
    public void GetActiveDomains_DuplicateDomains_ReturnsDistinctCaseInsensitive()
    {
        var rules = new[]
        {
            Rule("example.com"),
            Rule("example.com"),
            Rule("EXAMPLE.COM")
        };

        var active = ScheduleEvaluator.GetActiveDomains(rules, TimeSpan.FromHours(10));

        active.Should().HaveCount(1);
        active.Should().Contain("example.com");
    }

    [Test]
    public void GetActiveDomains_NoRules_ReturnsEmpty()
    {
        ScheduleEvaluator.GetActiveDomains([], TimeSpan.FromHours(10)).Should().BeEmpty();
    }

    #endregion

    #region GetNextBoundary

    [Test]
    public void GetNextBoundary_NoScheduledRules_ReturnsNull()
    {
        var rules = new[] { Rule("always.com"), Rule("disabled.com", isEnabled: false) };

        ScheduleEvaluator.GetNextBoundary(rules, TimeSpan.FromHours(10)).Should().BeNull();
    }

    [Test]
    public void GetNextBoundary_BeforeWindow_ReturnsStart()
    {
        var rules = new[] { ScheduledRule("example.com", TimeSpan.FromHours(8), TimeSpan.FromHours(17)) };

        ScheduleEvaluator.GetNextBoundary(rules, TimeSpan.FromHours(6))
            .Should().Be(TimeSpan.FromHours(8));
    }

    [Test]
    public void GetNextBoundary_InsideWindow_ReturnsEnd()
    {
        var rules = new[] { ScheduledRule("example.com", TimeSpan.FromHours(8), TimeSpan.FromHours(17)) };

        ScheduleEvaluator.GetNextBoundary(rules, TimeSpan.FromHours(10))
            .Should().Be(TimeSpan.FromHours(17));
    }

    [Test]
    public void GetNextBoundary_AfterWindow_WrapsToNextDayStart()
    {
        var rules = new[] { ScheduledRule("example.com", TimeSpan.FromHours(8), TimeSpan.FromHours(17)) };

        ScheduleEvaluator.GetNextBoundary(rules, TimeSpan.FromHours(20))
            .Should().Be(TimeSpan.FromHours(8));
    }

    [Test]
    public void GetNextBoundary_ExactlyAtBoundary_SkipsToNextOne()
    {
        var rules = new[] { ScheduledRule("example.com", TimeSpan.FromHours(8), TimeSpan.FromHours(17)) };

        ScheduleEvaluator.GetNextBoundary(rules, TimeSpan.FromHours(8))
            .Should().Be(TimeSpan.FromHours(17));
    }

    [Test]
    public void GetNextBoundary_MultipleRules_ReturnsEarliest()
    {
        var rules = new[]
        {
            ScheduledRule("a.com", TimeSpan.FromHours(8), TimeSpan.FromHours(17)),
            ScheduledRule("b.com", TimeSpan.FromHours(12), TimeSpan.FromHours(14))
        };

        ScheduleEvaluator.GetNextBoundary(rules, TimeSpan.FromHours(13))
            .Should().Be(TimeSpan.FromHours(14));
    }

    #endregion

    static BlockyRule Rule(string domain, bool isEnabled = true) => new()
    {
        Id = Guid.NewGuid(),
        Domain = domain,
        IsEnabled = isEnabled
    };

    static BlockyRule ScheduledRule(string domain, TimeSpan start, TimeSpan end) => new()
    {
        Id = Guid.NewGuid(),
        Domain = domain,
        IsEnabled = true,
        HasTimeRestriction = true,
        StartTime = start,
        EndTime = end
    };
}
