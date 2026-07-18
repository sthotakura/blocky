import test from "node:test";
import assert from "node:assert/strict";
import { buildRule, computeDesiredRules, diffRules, FIRST_RULE_ID, MAX_ACTIVE_RULES } from "../dnr.js";

test("buildRule: redirect main_frame requests for the domain and subdomains", () => {
    const rule = buildRule("example.com", 1000);

    assert.deepEqual(rule, {
        id: 1000,
        priority: 1,
        action: { type: "redirect", redirect: { extensionPath: "/blocked.html" } },
        condition: { requestDomains: ["example.com"], resourceTypes: ["main_frame"] }
    });
});

test("computeDesiredRules: assigns stable ids and keeps them across calls", () => {
    const first = computeDesiredRules(["b.com", "a.com"]);
    const aId = first.domainIdMap["a.com"];
    const bId = first.domainIdMap["b.com"];

    // a.com disappears, c.com appears — b.com must keep its id.
    const second = computeDesiredRules(["c.com", "b.com"], first.domainIdMap, first.nextDnrId);

    assert.equal(second.domainIdMap["b.com"], bId);
    assert.equal(second.domainIdMap["a.com"], aId, "map is append-only");
    assert.ok(second.domainIdMap["c.com"] > bId);
    assert.deepEqual(second.rules.map(r => r.condition.requestDomains[0]).sort(), ["b.com", "c.com"]);
});

test("computeDesiredRules: normalizes and de-duplicates domains", () => {
    const { rules } = computeDesiredRules(["Example.COM", "  example.com  ", "other.com"]);

    assert.deepEqual(rules.map(r => r.condition.requestDomains[0]).sort(), ["example.com", "other.com"]);
});

test("computeDesiredRules: starts ids at FIRST_RULE_ID", () => {
    const { rules } = computeDesiredRules(["a.com"]);
    assert.equal(rules[0].id, FIRST_RULE_ID);
});

test("computeDesiredRules: truncates deterministically over the limit", () => {
    const domains = Array.from({ length: MAX_ACTIVE_RULES + 10 }, (_, i) => `site${String(i).padStart(5, "0")}.com`);

    const { rules, truncated } = computeDesiredRules(domains);

    assert.equal(rules.length, MAX_ACTIVE_RULES);
    assert.equal(truncated, true);
    assert.equal(rules[0].condition.requestDomains[0], "site00000.com");
});

test("diffRules: no changes yields empty diff", () => {
    const desired = [buildRule("a.com", 1000)];
    const { removeRuleIds, addRules } = diffRules(desired, desired);

    assert.deepEqual(removeRuleIds, []);
    assert.deepEqual(addRules, []);
});

test("diffRules: adds new and removes stale rules", () => {
    const existing = [buildRule("old.com", 1000)];
    const desired = [buildRule("new.com", 1001)];

    const { removeRuleIds, addRules } = diffRules(existing, desired);

    assert.deepEqual(removeRuleIds, [1000]);
    assert.deepEqual(addRules.map(r => r.id), [1001]);
});

test("diffRules: replaces a rule whose id matches but whose content differs", () => {
    // A leftover from the legacy regexFilter extension occupying the same id.
    const legacy = {
        id: 1000,
        priority: 1,
        action: { type: "redirect", redirect: { extensionPath: "/blocked.html" } },
        condition: { regexFilter: "^https?://example\\.com", resourceTypes: ["main_frame"] }
    };
    const desired = [buildRule("example.com", 1000)];

    const { removeRuleIds, addRules } = diffRules([legacy], desired);

    assert.deepEqual(removeRuleIds, [1000]);
    assert.deepEqual(addRules.map(r => r.id), [1000]);
});

test("diffRules: tolerates empty existing rules", () => {
    const desired = [buildRule("a.com", 1000)];
    const { removeRuleIds, addRules } = diffRules([], desired);

    assert.deepEqual(removeRuleIds, []);
    assert.deepEqual(addRules, desired);
});
