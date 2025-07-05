// const BLOCKY_ENDPOINT = "http://localhost:8080/blocked-domains";

async function updateRules(domains) {
    try {
        // const res = await fetch(BLOCKY_ENDPOINT);
        // const domains = await res.json(); // expecting array of domains
        const rules = domains.map((domain, index) => ({
            id: 1000 + index,
            priority: 1,
            action: {
                type: "redirect",
                redirect: {extensionPath: "/blocked.html"}
            },
            condition: {
                urlFilter: `||${domain}^`,
                resourceTypes: ["main_frame"]
            }
        }));

        await chrome.declarativeNetRequest.updateDynamicRules({
            removeRuleIds: rules.map(r => r.id),
            addRules: rules
        });

        console.log("Blocky: Rules updated", rules);
    } catch (err) {
        console.error("Blocky: Failed to update rules", err);
    }
}

const WS_URL = "ws://localhost:8080/ws";

let socket;

function connectWebSocket() {
    if (socket && socket.readyState === WebSocket.OPEN) {
        console.warn("[Blocky] WebSocket already connected");
        return;
    }

    socket = new WebSocket(WS_URL);

    socket.onopen = () => {
        console.log("[Blocky] WebSocket connected");
    };

    socket.onmessage = async (event) => {
        try {
            const domains = JSON.parse(event.data);
            await updateRules(domains);
        } catch (e) {
            console.error("[Blocky] Failed to parse message:", e);
        }
    };

    socket.onclose = () => {
        console.warn("[Blocky] WebSocket disconnected. Reconnecting in 10s...");
        socket = null;
        setTimeout(connectWebSocket, 10000);
    };

    socket.onerror = (e) => {
        console.error("[Blocky] WebSocket error:", e);
        socket.close();
    };
}

// Update rules on install/startup
chrome.runtime.onInstalled.addListener(connectWebSocket);
//chrome.runtime.onStartup.addListener(connectWebSocket);
