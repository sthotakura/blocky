# Blocky

**Blocky** is a focused, no-nonsense website blocker for Windows.

Define domain-based blocking rules with optional time windows. Enforcement happens entirely inside Chrome via the Declarative Net Request API — no admin rights, no system proxy, no broken SSL.

---

## Features

- Block websites by domain (exact match + all subdomains)
- Optional time-of-day windows, including overnight spans (e.g. 22:00–06:00)
- Real-time rule push via WebSocket — changes apply immediately in Chrome
- Runs silently in the system tray
- No admin rights required
- No system-wide proxy

---

## Requirements

- Windows 10 or later
- Google Chrome

---

## Installation

### 1. Download and run the app

1. Go to the [Releases page](https://github.com/sthotakura/blocky/releases)
2. Download `blocky.zip` from the latest release
3. Extract the zip to any folder
4. Run `Blocky.exe`

The app starts minimized to the system tray. Right-click the tray icon to open or quit.

### 2. Install the Chrome extension

> The extension is not yet on the Chrome Web Store — load it manually for now.

1. Download `blocky-chrome-extension.zip` from the same release
2. Extract the zip to a folder (e.g. `C:\blocky-extension\`)
3. Open Chrome and go to `chrome://extensions`
4. Enable **Developer Mode** (toggle, top-right)
5. Click **Load unpacked**
6. Select the folder you extracted the extension into
7. Confirm the extension appears and is enabled

### 3. Verify it's working

1. Open Chrome DevTools on the extension's service worker:
   - Go to `chrome://extensions`
   - Find **Blocky Extension** → click **Service Worker**
2. You should see `[Blocky] WebSocket connected` in the console
3. Add a rule in the Blocky app (e.g. `example.com`)
4. Navigate to `https://example.com` — you should see the blocked page

---

## How It Works

```
Blocky.exe  ──(WebSocket ws://localhost:45678/ws)──►  Chrome Extension
    │                                                        │
    │  SQLite (rules)                                Chrome DNR dynamic rules
    │
    └──(HTTP GET /blocked-domains)──►  available for manual queries
```

1. `Blocky.exe` runs a local Kestrel server on port **45678**
2. The Chrome extension connects via WebSocket and receives the active domain list
3. Rules are translated into Chrome Declarative Net Request regex rules
4. Blocked sites redirect to the extension's built-in `blocked.html` page
5. When you add, edit, or remove a rule, the extension receives an immediate push — no 30-second wait

---

## Using the App

### Adding a rule

Click the **+** button in the top-right corner. Enter a domain (e.g. `reddit.com`) — all subdomains are blocked automatically (`www.reddit.com`, `old.reddit.com`, etc.).

### Time-based blocking

Toggle **Time Restriction** when adding or editing a rule. Set start and end times. Overnight windows work correctly (e.g. 22:00–06:00 blocks from 10pm until 6am).

### Editing and removing rules

Use the **Edit** (pencil) and **Delete** (bin) buttons on each row in the rule list.

### System tray

Blocky starts minimized. Double-click the tray icon to open the window. Right-click for **Quit**.

### Logs

Click the **book** icon in the toolbar to open the current log file.

---

## Troubleshooting

**Extension shows "WebSocket disconnected" or rules don't apply**

- Make sure `Blocky.exe` is running (check system tray)
- Confirm the extension is enabled in `chrome://extensions`
- Check the service worker console (`chrome://extensions` → Blocky → Service Worker) for error messages

**App fails to start with "Port Conflict" error**

Port 45678 is already in use by another application. Find and close the conflicting process, then restart Blocky.

```powershell
# Find what's using port 45678
netstat -ano | findstr :45678
```

**Extension installed but not blocking**

Chrome may have suspended the service worker. Open the extension's service worker DevTools to wake it, or navigate to any page to trigger a reconnect.

**Site is not blocked even though a rule exists**

- Confirm the rule is **enabled** in the app (checkbox in the Enabled column)
- If using a time restriction, confirm you are within the configured window
- Check the service worker console for `Blocky: Rules updated` to confirm the rule was pushed

---

## Building from source

```bash
# Requirements: .NET 10 SDK

# Build
dotnet build Blocky.sln

# Run tests
dotnet test Blocky.sln

# Run the app
dotnet run --project Blocky/Blocky.csproj

# Publish self-contained Windows executable
dotnet publish Blocky/Blocky.csproj -c Release -r win-x64 --self-contained true
```

---

## Releases

GitHub Actions builds and packages automatically on version tags (`v*`):

- `blocky.zip` — self-contained `Blocky.exe` (no .NET install needed)
- `blocky-chrome-extension.zip` — Chrome extension folder, ready to load unpacked

---

## Roadmap

- [ ] Chrome Web Store listing (removes the need for Developer Mode)
- [ ] Windows installer with startup registration
- [ ] Update notifications
- [ ] Firefox extension
- [ ] Pomodoro / temporary focus blocks
- [ ] Block statistics

---

## Disclaimer

Experimental tool. Bypasses require Chrome; other browsers are unaffected. Feedback and PRs welcome.
