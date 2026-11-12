// Translation of active domains into Chrome DNR dynamic rules, as a minimal diff:
// stable per-domain integer ids (append-only map in storage) keep updates small, and
// a single updateDynamicRules call applies remove+add atomically — there is never a
// moment with zero rules during an update.

// Redirect rules count against Chrome's "unsafe dynamic rules" cap (~5000); stay under it.
export const MAX_ACTIVE_RULES = 4500;
export const FIRST_RULE_ID = 1000;

export function buildRule(domain, id) {
    return {
        id,
        priority: 1,
        action: { type: "redirect", redirect: { extensionPath: "/blocked.html" } },
        condition: {
            // requestDomains matches the domain and all of its subdomains,
            // the same semantics the app's UI promises.
            requestDomains: [domain],
            resourceTypes: ["main_frame"]
        }
    };
}

// Returns { rules, domainIdMap, nextDnrId, truncated }. domainIdMap is append-only so a
// domain keeps its rule id for the lifetime of the extension profile.
export function computeDesiredRules(activeDomains, domainIdMap = {}, nextDnrId = FIRST_RULE_ID) {
    const map = { ...domainIdMap };
    let nextId = nextDnrId;

    const domains = [...new Set((activeDomains ?? []).map(d => d.trim().toLowerCase()).filter(Boolean))];
    domains.sort();

    const limited = domains.slice(0, MAX_ACTIVE_RULES);
    const rules = limited.map(domain => {
        if (!(domain in map)) {
            map[domain] = nextId++;
        }
        return buildRule(domain, map[domain]);
    });

    return { rules, domainIdMap: map, nextDnrId: nextId, truncated: domains.length > limited.length };
}

// Same matching semantics as DNR requestDomains: the domain itself or any subdomain.
export function isHostBlocked(hostname, domains) {
    const host = String(hostname ?? "").toLowerCase().replace(/\.$/, "");
    if (!host) return false;
    return (domains ?? []).some(domain => host === domain || host.endsWith(`.${domain}`));
}

// DNR only intercepts new navigations, so tabs already sitting on a domain when its
// block engages must be redirected explicitly. Returns the ids of those tabs.
export function findBlockedTabIds(tabs, domains) {
    const ids = [];
    for (const tab of tabs ?? []) {
        let url;
        try {
            url = new URL(tab.url);
        } catch {
            continue; // no url (no host access) or unparsable
        }
        if (url.protocol !== "http:" && url.protocol !== "https:") continue;
        if (isHostBlocked(url.hostname, domains)) ids.push(tab.id);
    }
    return ids;
}

function matchesDesired(existing, desired) {
    const existingDomains = existing.condition?.requestDomains;
    return existing.action?.type === "redirect"
        && Array.isArray(existingDomains)
        && existingDomains.length === 1
        && existingDomains[0] === desired.condition.requestDomains[0];
}

// Diffs the currently installed dynamic rules against the desired set. Rules whose id
// matches but whose content differs (e.g. leftovers from the legacy regexFilter
// extension) are replaced, not kept.
export function diffRules(existingRules, desiredRules) {
    const desiredById = new Map(desiredRules.map(rule => [rule.id, rule]));

    const removeRuleIds = [];
    const keptIds = new Set();
    for (const existing of existingRules ?? []) {
        const desired = desiredById.get(existing.id);
        if (desired && matchesDesired(existing, desired)) {
            keptIds.add(existing.id);
        } else {
            removeRuleIds.push(existing.id);
        }
    }

    const addRules = desiredRules.filter(rule => !keptIds.has(rule.id));
    return { removeRuleIds, addRules };
}
