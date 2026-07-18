import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import {
    getActiveDomains,
    getNextBoundaryMinutes,
    isRuleActive,
    isWithinWindow,
    minutesSinceMidnight
} from "../evaluator.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const vectorsPath = path.join(here, "..", "..", "..", "tests", "contract", "time-window-vectors.json");
const vectors = JSON.parse(readFileSync(vectorsPath, "utf8"));

// The same vectors run against the C# ScheduleEvaluator in Blocky.Core.Tests;
// drift in either implementation breaks its build.
for (const vector of vectors.cases) {
    test(`contract: ${vector.name}`, () => {
        const active = getActiveDomains(vector.rules, vector.nowMinutes);
        assert.deepEqual([...active].sort(), [...vector.expected.activeDomains].sort());
        assert.equal(getNextBoundaryMinutes(vector.rules, vector.nowMinutes), vector.expected.nextBoundaryMinutes);
    });
}

test("isWithinWindow: same-day window is end-exclusive", () => {
    assert.equal(isWithinWindow(480, 480, 1020), true);
    assert.equal(isWithinWindow(1019, 480, 1020), true);
    assert.equal(isWithinWindow(1020, 480, 1020), false);
});

test("isWithinWindow: overnight window spans midnight", () => {
    assert.equal(isWithinWindow(1380, 1320, 360), true);
    assert.equal(isWithinWindow(180, 1320, 360), true);
    assert.equal(isWithinWindow(600, 1320, 360), false);
});

test("isRuleActive: malformed schedule is inactive, not always-on", () => {
    const rule = { domain: "x.com", enabled: true, schedule: { startMinutes: "oops" } };
    assert.equal(isRuleActive(rule, 600), false);
});

test("isRuleActive: blank domain is never active", () => {
    assert.equal(isRuleActive({ domain: "   ", enabled: true, schedule: null }, 600), false);
});

test("getActiveDomains: tolerates null/undefined rules array", () => {
    assert.deepEqual(getActiveDomains(null, 600), []);
    assert.deepEqual(getActiveDomains(undefined, 600), []);
});

test("minutesSinceMidnight: converts a local Date", () => {
    const date = new Date(2026, 6, 17, 9, 30, 45);
    assert.equal(minutesSinceMidnight(date), 570);
});
