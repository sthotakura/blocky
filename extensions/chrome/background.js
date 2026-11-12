// Blocky service worker. All state lives in chrome.storage.local and in the browser's
// DNR rule set, both of which survive worker death and browser restarts. Every wake
// path (install, browser start, any alarm, host push) funnels through reconcile(),
// which is idempotent from wall-clock time — there is no other state machine.

import { getActiveDomains, getNextBoundaryMinutes, minutesSinceMidnight, msUntilBoundary } from "./evaluator.js";
import { computeDesiredRules, diffRules, findBlockedTabIds } from "./dnr.js";
import { connectNativeHost, isHostUnregistered } from "./nativePort.js";

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
    const nowDate = new Date();
    const now = minutesSinceMidnight(nowDate);

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
    await scheduleBoundaryAlarm(state.rules, nowDate);
    await sweepOpenTabs(desired.map(rule => rule.condition.requestDomains[0]));
}

async function scheduleBoundaryAlarm(rules, nowDate) {
    await chrome.alarms.clear(BOUNDARY_ALARM);
    const boundary = getNextBoundaryMinutes(rules, minutesSinceMidnight(nowDate));
    if (boundary === null) return;

    // Fire one second into the boundary minute so the new state is already in effect.
    const when = nowDate.getTime() + msUntilBoundary(nowDate, boundary) + 1000;
    await chrome.alarms.create(BOUNDARY_ALARM, { when });
}

// DNR rules only catch new navigations; redirect tabs that are already on a blocked
// domain. Best effort — a failure here must never break rule application.
async function sweepOpenTabs(domains) {
    if (domains.length === 0) return;
    try {
        const blockedUrl = chrome.runtime.getURL("blocked.html");
        const tabIds = findBlockedTabIds(await chrome.tabs.query({}), domains);
        await Promise.allSettled(tabIds.map(id => chrome.tabs.update(id, { url: blockedUrl })));
        if (tabIds.length > 0) console.log(`[Blocky] Redirected ${tabIds.length} open tab(s)`);
    } catch (e) {
        console.warn("[Blocky] Tab sweep failed:", e);
    }
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
    const update = { hostState: "disconnected" };
    if (isHostUnregistered(reason)) {
        // No host is registered at all: Blocky was uninstalled, and with the host gone
        // nothing could ever deliver dbMissing. Treat it as the same authoritative empty
        // set. A reinstall resyncs on the next reconnect (rev is reset).
        console.warn("[Blocky] Host is not registered; clearing rules");
        update.rules = [];
        update.rev = null;
    }
    await chrome.storage.local.set(update);
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
