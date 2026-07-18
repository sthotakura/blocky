// Blocky service worker. All state lives in chrome.storage.local and in the browser's
// DNR rule set, both of which survive worker death and browser restarts. Every wake
// path (install, browser start, any alarm, host push) funnels through reconcile(),
// which is idempotent from wall-clock time — there is no other state machine.

import { getActiveDomains, getNextBoundaryMinutes, minutesSinceMidnight, MINUTES_PER_DAY } from "./evaluator.js";
import { computeDesiredRules, diffRules } from "./dnr.js";
import { connectNativeHost } from "./nativePort.js";

const BOUNDARY_ALARM = "boundary";
const RESYNC_ALARM = "resync";
const RECONNECT_ALARM = "reconnect";

const STATE_DEFAULTS = {
    schemaVersion: 1,
    rules: [],          // full rule definitions in wire shape; the offline source of truth
    rev: null,          // content hash of the last synced rule set
    lastSyncAt: 0,
    domainIdMap: {},    // domain -> stable DNR rule id (append-only)
    nextDnrId: 1000,
    hostState: "disconnected"
};

// In-memory only; dies with the worker. reconcile() reconnects on every wake.
let port = null;

async function loadState() {
    return chrome.storage.local.get(STATE_DEFAULTS);
}

async function applyRules() {
    const state = await loadState();
    const now = minutesSinceMidnight(new Date());

    const active = getActiveDomains(state.rules, now);
    const { rules: desired, domainIdMap, nextDnrId, truncated } =
        computeDesiredRules(active, state.domainIdMap, state.nextDnrId);

    const existing = await chrome.declarativeNetRequest.getDynamicRules();
    const { removeRuleIds, addRules } = diffRules(existing, desired);
    if (removeRuleIds.length > 0 || addRules.length > 0) {
        await chrome.declarativeNetRequest.updateDynamicRules({ removeRuleIds, addRules });
        console.log(`[Blocky] DNR updated: -${removeRuleIds.length} +${addRules.length} (${desired.length} active)`);
    }

    await chrome.storage.local.set({ domainIdMap, nextDnrId });
    await updateBadge(state.hostState, truncated);
    await scheduleBoundaryAlarm(state.rules, now);
}

async function scheduleBoundaryAlarm(rules, nowMinutes) {
    await chrome.alarms.clear(BOUNDARY_ALARM);
    const boundary = getNextBoundaryMinutes(rules, nowMinutes);
    if (boundary === null) return;

    let delayMinutes = boundary - nowMinutes;
    if (delayMinutes <= 0) delayMinutes += MINUTES_PER_DAY;
    // Fire just after the boundary minute starts so the new state is already in effect.
    await chrome.alarms.create(BOUNDARY_ALARM, { delayInMinutes: delayMinutes + 0.05 });
}

async function updateBadge(hostState, truncated) {
    const problem = truncated ? "rule limit exceeded"
        : hostState !== "connected" ? "not connected to the Blocky app"
        : null;
    await chrome.action.setBadgeText({ text: problem ? "!" : "" });
    if (problem) {
        await chrome.action.setBadgeBackgroundColor({ color: "#C0392B" });
        await chrome.action.setTitle({ title: `Blocky: ${problem}` });
    } else {
        await chrome.action.setTitle({ title: "Blocky is active" });
    }
}

async function handleRulesMessage(message) {
    const state = await loadState();
    await chrome.alarms.clear(RECONNECT_ALARM);

    if (message.rev !== null && message.rev === state.rev && state.hostState === "connected") {
        await chrome.storage.local.set({ lastSyncAt: Date.now() });
        return;
    }

    // dbMissing means the database genuinely does not exist (e.g. the app was
    // uninstalled) — that is an authoritative empty rule set, so blocking clears.
    const rules = message.dbMissing ? [] : (message.payload?.rules ?? []);
    await chrome.storage.local.set({
        rules,
        rev: message.rev ?? null,
        lastSyncAt: Date.now(),
        hostState: "connected"
    });
    console.log(`[Blocky] Synced ${rules.length} rules (rev ${message.rev}, dbMissing: ${message.dbMissing})`);
    await applyRules();
}

async function handleDisconnect(reason) {
    port = null;
    console.warn("[Blocky] Host disconnected:", reason);
    // Keep enforcing the stored rules (fail closed); retry via alarm — in-memory
    // backoff would die with the worker anyway.
    await chrome.storage.local.set({ hostState: "disconnected" });
    await chrome.alarms.create(RECONNECT_ALARM, { periodInMinutes: 1 });
    await applyRules();
}

function ensurePortConnected() {
    if (port) return;
    port = connectNativeHost({
        onRules: message => { handleRulesMessage(message).catch(logError("sync")); },
        onDisconnect: reason => { handleDisconnect(reason).catch(logError("disconnect")); }
    });
}

async function reconcile() {
    await applyRules();
    ensurePortConnected();
}

function logError(context) {
    return e => console.error(`[Blocky] ${context} failed:`, e);
}

chrome.runtime.onInstalled.addListener(() => { reconcile().catch(logError("install")); });
chrome.runtime.onStartup.addListener(() => { reconcile().catch(logError("startup")); });

chrome.alarms.onAlarm.addListener(alarm => {
    if ([BOUNDARY_ALARM, RESYNC_ALARM, RECONNECT_ALARM].includes(alarm.name)) {
        reconcile().catch(logError(`alarm ${alarm.name}`));
    }
});

// Hourly belt-and-braces resync: covers clock/timezone changes and missed alarms.
chrome.alarms.create(RESYNC_ALARM, { periodInMinutes: 60 });

// Top-level code runs on every worker wake, whatever caused it.
reconcile().catch(logError("wake"));
