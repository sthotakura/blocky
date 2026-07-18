using Blocky.Core.Data;

namespace Blocky.Core.Evaluation;

/// <summary>
/// Pure time-window evaluation. This is the C# reference implementation for the
/// extension's evaluator.js — both are exercised by the shared contract test vectors.
/// Windows are end-exclusive; start > end means the window spans midnight.
/// </summary>
public static class ScheduleEvaluator
{
    public static bool IsRuleActive(BlockyRule rule, TimeSpan timeOfDay)
    {
        if (!rule.IsEnabled || string.IsNullOrWhiteSpace(rule.Domain))
        {
            return false;
        }

        if (!rule.HasTimeRestriction)
        {
            return true;
        }

        if (rule.StartTime is null || rule.EndTime is null)
        {
            return false;
        }

        return IsWithinWindow(timeOfDay, rule.StartTime.Value, rule.EndTime.Value);
    }

    static bool IsWithinWindow(TimeSpan current, TimeSpan start, TimeSpan end) =>
        start <= end
            ? current >= start && current < end
            : current >= start || current < end;

    public static string[] GetActiveDomains(IEnumerable<BlockyRule> rules, TimeSpan timeOfDay) =>
        rules
            .Where(rule => IsRuleActive(rule, timeOfDay))
            .Select(rule => rule.Domain)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>
    /// The next time of day at which the active set may change: the earliest start or end
    /// boundary strictly after <paramref name="timeOfDay"/>, wrapping past midnight.
    /// Null when no enabled rule has a valid time restriction.
    /// </summary>
    public static TimeSpan? GetNextBoundary(IEnumerable<BlockyRule> rules, TimeSpan timeOfDay)
    {
        var day = TimeSpan.FromDays(1);
        TimeSpan? next = null;
        var bestDelta = TimeSpan.MaxValue;

        foreach (var rule in rules)
        {
            if (!rule.IsEnabled || !rule.HasTimeRestriction) continue;
            if (rule.StartTime is null || rule.EndTime is null) continue;

            Span<TimeSpan> boundaries = [rule.StartTime.Value, rule.EndTime.Value];
            foreach (var boundary in boundaries)
            {
                var delta = boundary - timeOfDay;
                if (delta <= TimeSpan.Zero) delta += day;

                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    next = boundary;
                }
            }
        }

        return next;
    }
}
