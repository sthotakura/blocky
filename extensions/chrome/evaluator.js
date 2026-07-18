// Pure time-window evaluation. Mirrors Blocky.Core's ScheduleEvaluator (the C#
// reference implementation); both are exercised by tests/contract/time-window-vectors.json.
// Windows are end-exclusive; startMinutes > endMinutes means the window spans midnight.

export const MINUTES_PER_DAY = 1440;

export function minutesSinceMidnight(date) {
    return date.getHours() * 60 + date.getMinutes();
}

export function isWithinWindow(nowMinutes, startMinutes, endMinutes) {
    return startMinutes <= endMinutes
        ? nowMinutes >= startMinutes && nowMinutes < endMinutes
        : nowMinutes >= startMinutes || nowMinutes < endMinutes;
}

function hasValidSchedule(rule) {
    return rule.schedule
        && Number.isFinite(rule.schedule.startMinutes)
        && Number.isFinite(rule.schedule.endMinutes);
}

export function isRuleActive(rule, nowMinutes) {
    if (!rule || !rule.enabled) return false;
    if (typeof rule.domain !== "string" || rule.domain.trim() === "") return false;
    if (!rule.schedule) return true;
    if (!hasValidSchedule(rule)) return false;
    return isWithinWindow(nowMinutes, rule.schedule.startMinutes, rule.schedule.endMinutes);
}

export function getActiveDomains(rules, nowMinutes) {
    const seen = new Set();
    const domains = [];
    for (const rule of rules ?? []) {
        if (!isRuleActive(rule, nowMinutes)) continue;
        const domain = rule.domain.trim();
        const key = domain.toLowerCase();
        if (seen.has(key)) continue;
        seen.add(key);
        domains.push(domain);
    }
    return domains;
}

// The earliest start/end boundary strictly after nowMinutes (wrapping past midnight),
// or null when no enabled rule has a schedule. This is when the active set may next change.
export function getNextBoundaryMinutes(rules, nowMinutes) {
    let best = null;
    let bestDelta = Infinity;
    for (const rule of rules ?? []) {
        if (!rule || !rule.enabled || !hasValidSchedule(rule)) continue;
        for (const boundary of [rule.schedule.startMinutes, rule.schedule.endMinutes]) {
            let delta = boundary - nowMinutes;
            if (delta <= 0) delta += MINUTES_PER_DAY;
            if (delta < bestDelta) {
                bestDelta = delta;
                best = boundary;
            }
        }
    }
    return best;
}
