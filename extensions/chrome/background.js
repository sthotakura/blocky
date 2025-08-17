// const BLOCKY_ENDPOINT = "http://localhost:8080/blocked-domains";

// Helpers to work with DNR using Promises (MV3 service workers can use callbacks; we wrap them)
function getExistingDynamicRuleIds() {
    return new Promise((resolve) => {
        try {
            chrome.declarativeNetRequest.getDynamicRules((rules) => {
                resolve(rules.map(r => r.id));
            });
        } catch (e) {
            console.error("Blocky: getDynamicRules failed", e);
            resolve([]);
        }
    });
}

function updateDynamicRulesAsync(options) {
    return new Promise((resolve, reject) => {
        try {
            chrome.declarativeNetRequest.updateDynamicRules(options, () => {
                if (chrome.runtime.lastError) {
                    reject(chrome.runtime.lastError);
                } else {
                    resolve();
                }
            });
        } catch (e) {
            reject(e);
        }
    });
}

function escapeDomainForRegex(domain) {
    // Escape dots and other regex specials that may appear in domains
    return domain.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function buildRuleForDomain(domain, id) {
    const safe = escapeDomainForRegex(domain.trim());
    if (!safe) return null;
    // Match http/https, optional subdomains, and ensure path boundary
    const regex = `^https?:\\/\\/([^\\/]*\\.)?${safe}(?:[\\/:?#]|$)`;
    return {
        id,
        priority: 1,
        action: { type: "redirect", redirect: { extensionPath: "/blocked.html" } },
        condition: {
            regexFilter: regex,
            resourceTypes: ["main_frame"]
        }
    };
}

async function updateRules(domains) {
    try {
        // const res = await fetch(BLOCKY_ENDPOINT);
        // const domains = await res.json(); // expecting array of domains
        const cleaned = Array.isArray(domains) ? domains.filter(Boolean) : [];
        const rules = cleaned
            .map((domain, index) => buildRuleForDomain(String(domain), 1000 + index))
            .filter(Boolean);

        // Remove all existing dynamic rules to avoid stale entries, then add the new set
        const existingIds = await getExistingDynamicRuleIds();
        await updateDynamicRulesAsync({ removeRuleIds: existingIds, addRules: [] });
        if (rules.length > 0) {
            await updateDynamicRulesAsync({ removeRuleIds: [], addRules: rules });
        }

        console.log("Blocky: Rules updated", rules);
    } catch (err) {
        console.error("Blocky: Failed to update rules", err);
    }
}

const WS_URL = "ws://localhost:8080/ws";

let socket;
let isConnecting = false;
let reconnectTimerId = null;
let heartbeatTimerId = null;
let reconnectDelayMs = 1000; // start with 1s backoff
const RECONNECT_MAX_DELAY_MS = 60000; // cap at 60s
const HEARTBEAT_INTERVAL_MS = 25000; // 25s heartbeat to keep connection alive

function clearHeartbeat() {
    if (heartbeatTimerId) {
        clearInterval(heartbeatTimerId);
        heartbeatTimerId = null;
    }
}

function scheduleReconnect() {
    if (reconnectTimerId) {
        clearTimeout(reconnectTimerId);
        reconnectTimerId = null;
    }
    const delay = reconnectDelayMs;
    console.warn(`[Blocky] WebSocket disconnected. Reconnecting in ${Math.round(delay/1000)}s...`);
    reconnectTimerId = setTimeout(() => {
        reconnectTimerId = null;
        connectWebSocket();
    }, delay);
    reconnectDelayMs = Math.min(reconnectDelayMs * 2, RECONNECT_MAX_DELAY_MS);
}

function connectWebSocket() {
    if (socket && (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING)) {
        console.warn("[Blocky] WebSocket already connected/connecting");
        return;
    }
    if (isConnecting) {
        console.warn("[Blocky] WebSocket connect already in progress");
        return;
    }

    isConnecting = true;
    try {
        socket = new WebSocket(WS_URL);
    } catch (e) {
        console.error("[Blocky] Failed to create WebSocket:", e);
        isConnecting = false;
        scheduleReconnect();
        return;
    }

    socket.onopen = () => {
        isConnecting = false;
        reconnectDelayMs = 1000; // reset backoff on success
        console.log("[Blocky] WebSocket connected");
        // Start heartbeat to keep connection alive if server expects activity
        clearHeartbeat();
        heartbeatTimerId = setInterval(() => {
            try {
                if (socket && socket.readyState === WebSocket.OPEN) {
                    socket.send(JSON.stringify({ type: "ping", ts: Date.now() }));
                }
            } catch (_) { /* ignore */ }
        }, HEARTBEAT_INTERVAL_MS);
    };

    socket.onmessage = async (event) => {
        try {
            const data = event.data;
            // Only try to parse JSON arrays/objects; ignore other payloads (e.g., pong strings)
            if (typeof data === "string" && (data.trim().startsWith("[") || data.trim().startsWith("{"))) {
                const domains = JSON.parse(data);
                await updateRules(domains);
            }
        } catch (e) {
            // Silently ignore unexpected payloads
        }
    };

    socket.onclose = () => {
        isConnecting = false;
        clearHeartbeat();
        socket = null;
        scheduleReconnect();
    };

    socket.onerror = (e) => {
        console.error("[Blocky] WebSocket error:", e);
        try { socket && socket.close(); } catch {}
    };
}

// Connect on install and on browser startup
chrome.runtime.onInstalled.addListener(connectWebSocket);
chrome.runtime.onStartup.addListener(connectWebSocket);
