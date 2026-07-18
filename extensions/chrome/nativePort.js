// Native-messaging port to the Blocky host (spawned by Chrome over stdio — no TCP port).
// The host pushes a full rules message on every database change and answers get-rules.

export const HOST_NAME = "com.blocky.host";
export const PROTOCOL_VERSION = 1;

// Returns the connected port, or null when connectNative itself throws
// (e.g. nativeMessaging unavailable). Errors after connect surface via onDisconnect.
export function connectNativeHost({ onRules, onDisconnect }) {
    let port;
    try {
        port = chrome.runtime.connectNative(HOST_NAME);
    } catch (e) {
        console.warn("[Blocky] connectNative failed:", e);
        onDisconnect(String(e));
        return null;
    }

    port.onMessage.addListener(message => {
        if (!message || message.v !== PROTOCOL_VERSION) {
            console.warn("[Blocky] Ignoring message with unknown protocol version:", message?.v);
            return;
        }
        if (message.type === "rules") {
            onRules(message);
        } else if (message.type === "error") {
            console.warn("[Blocky] Host error:", message.code, message.message);
        } else {
            console.warn("[Blocky] Ignoring unknown message type:", message.type);
        }
    });

    port.onDisconnect.addListener(() => {
        const reason = chrome.runtime.lastError?.message ?? "host disconnected";
        onDisconnect(reason);
    });

    port.postMessage({ v: PROTOCOL_VERSION, type: "get-rules" });
    return port;
}
