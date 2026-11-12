// Shows a notice on the blocked page while the extension cannot reach the Blocky host:
// stored rules keep enforcing in that state, so a rule the user just removed in the app
// may still be blocking. Kept in its own file because MV3 forbids inline scripts.

const notice = document.getElementById("sync-notice");

function render(hostState) {
    notice.hidden = hostState === "connected";
}

chrome.storage.local.get({ hostState: "disconnected" }).then(state => render(state.hostState));

// Live update: the notice disappears as soon as the reconnect alarm succeeds.
chrome.storage.onChanged.addListener((changes, area) => {
    if (area === "local" && changes.hostState) {
        render(changes.hostState.newValue);
    }
});
