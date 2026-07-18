# Blocky

**Blocky** is a focused, no-nonsense website blocker for Windows.

Define domain-based blocking rules with optional time windows. Enforcement happens entirely inside Chrome via the Declarative Net Request API — no admin rights, no system proxy, no broken SSL, **and no open ports**. Blocking keeps working even when the Blocky app is closed.

---

## Features

- Block websites by domain (exact match + all subdomains)
- Optional time-of-day windows, including overnight spans (e.g. 22:00–06:00)
- Enforcement is independent of the app — schedules engage and lift even while `Blocky.exe` is not running
- Rule changes sync to Chrome within seconds via native messaging (no TCP port, no WebSocket)
- Runs silently in the system tray
- No admin rights required, no system-wide proxy

---

## Requirements

- Windows 10 or later
- Google Chrome

---

## Installation

### 1. Download and install the app

1. Go to the [Releases page](https://github.com/sthotakura/blocky/releases)
2. Download `blocky.zip` from the latest release
3. Extract the zip to a permanent folder (e.g. `C:\Tools\Blocky\`) — the install script registers absolute paths, so don't move the folder afterwards

   > **Windows SmartScreen:** `Blocky.exe` and `Blocky.Host.exe` are not code-signed, so on first run Windows may show *"Windows protected your PC"*. Click **More info** → **Run anyway**. This is expected for an unsigned indie tool, not a sign of tampering — but only proceed if you downloaded the zip from the official Releases page above.
4. Run `install.ps1` from that folder (right-click → *Run with PowerShell*, or from a terminal). No elevation needed. It:
   - adds `Blocky.exe` to Windows startup (HKCU Run key)
   - writes the Chrome native-messaging host manifest (`com.blocky.host.json`) and registers it under `HKCU\Software\Google\Chrome\NativeMessagingHosts`

   > **PowerShell execution policy:** if you see *"running scripts is disabled on this system"*, the zip's scripts are most likely still marked as downloaded from the internet. Either right-click `install.ps1` → **Properties** → **Unblock**, or run from a terminal: `powershell -ExecutionPolicy Bypass -File .\install.ps1`. No policy change is needed system-wide.
5. Start `Blocky.exe` (or log off/on and let startup do it)

The app starts minimized to the system tray. Double-click the tray icon to open it.

### 2. Install the Chrome extension

> The extension is not yet on the Chrome Web Store — load it manually for now.

1. Download `blocky-chrome-extension.zip` from the same release
2. Extract the zip to a permanent folder (e.g. `C:\Tools\blocky-extension\`)
3. Open Chrome and go to `chrome://extensions`
4. Enable **Developer Mode** (toggle, top-right)
5. Click **Load unpacked** and select the extracted folder
6. Confirm the extension appears with ID `dnmkjkbjklkbjakdjhfjpcjknglifnmb` — the ID is pinned by the `key` field in `manifest.json` and must match, because the native-messaging host only accepts connections from that ID

### 3. Verify it's working

1. Add a rule in the Blocky app (e.g. `example.com`)
2. Navigate to `https://example.com` — you should see the blocked page
3. (Optional) Open the extension's service worker console (`chrome://extensions` → Blocky Extension → **Service Worker**) — you should see `[Blocky] Synced N rules`

A red **!** badge on the extension icon means something needs attention — hover it for the reason (see Troubleshooting).

---

## How It Works

```
Blocky.exe (WPF editor)                Chrome
      │ writes                            │ spawns via registry (stdio, no port)
      ▼                                   ▼
%LocalAppData%\Blocky\blocky.db ◀── Blocky.Host.exe ──▶ Blocky extension (MV3)
   (SQLite, source of truth)     watch + push full rules      │
                                                              ▼
                                              chrome.storage.local (rules)
                                              evaluate time windows locally
                                              Chrome DNR dynamic rules
```

1. The Blocky app is a pure rule editor — it writes rules to a local SQLite database and has no runtime connection to Chrome
2. Chrome itself spawns `Blocky.Host.exe` (registered as a native-messaging host) and talks to it over stdio — no TCP port exists anywhere
3. The host watches the database for changes and pushes **full rule definitions** (domains + schedules) to the extension
4. The extension stores the rules in `chrome.storage.local`, evaluates time windows against the local clock, and maintains Chrome DNR rules; alarms wake it exactly at window boundaries
5. Because rules and schedules live inside Chrome, blocking keeps working when the app is closed, when the host isn't running, and across browser restarts. Blocked sites redirect to the extension's `blocked.html`

Uninstalling Blocky (run `uninstall.ps1`, delete `%LocalAppData%\Blocky`) genuinely unblocks: when the database is gone the host reports it, and the extension clears all rules.

---

## Using the App

### Adding a rule

Click the **+** button in the top-right corner. Enter a domain (e.g. `reddit.com`) — all subdomains are blocked automatically (`www.reddit.com`, `old.reddit.com`, etc.).

### Time-based blocking

Toggle **Time Restriction** when adding or editing a rule. Set start and end times. Overnight windows work correctly (e.g. 22:00–06:00 blocks from 10pm until 6am). The end time is exclusive: a 09:00–17:00 window unblocks at exactly 17:00.

### Editing and removing rules

Use the **Edit** (pencil) and **Delete** (bin) buttons on each row in the rule list.

### System tray

Blocky starts minimized. Double-click the tray icon to open the window. Right-click for **Quit**. Quitting only closes the editor — blocking continues.

### Logs

Click the **book** icon in the toolbar to open the current app log file. The native host writes its own logs to `%LocalAppData%\Blocky\logs\Blocky.Host-*.log`.

---

## Troubleshooting

**"Windows protected your PC" when running `Blocky.exe`, `Blocky.Host.exe`, or `install.ps1`**

Expected — the binaries aren't code-signed yet. Click **More info** → **Run anyway**. Only do this for a zip downloaded from the official [Releases page](https://github.com/sthotakura/blocky/releases).

**`install.ps1` fails with "running scripts is disabled on this system"**

The script is blocked because Windows marked it as downloaded from the internet. Right-click `install.ps1` → **Properties** → check **Unblock** → **OK**, then run it again. Or run it once via `powershell -ExecutionPolicy Bypass -File .\install.ps1` — this doesn't change your system's script execution policy.

**Extension badge shows "!" with "not connected to the Blocky app"**

Rules that were already synced still enforce; only *new edits* won't arrive until the connection is restored. The extension retries every minute. Check:

- `install.ps1` was run from the folder containing `Blocky.Host.exe` (it registers the host's absolute path)
- The extension ID on `chrome://extensions` is exactly `dnmkjkbjklkbjakdjhfjpcjknglifnmb` — if you loaded a modified `manifest.json` without the `key` field the ID will differ and Chrome will refuse the connection
- `%LocalAppData%\Blocky\logs\Blocky.Host-*.log` for host-side errors

**Extension badge shows "!" with "rule limit exceeded"**

More than ~4,500 domains are active at once; the first 4,500 are enforced. Reduce the rule count.

**Site is not blocked even though a rule exists**

- Confirm the rule is **enabled** in the app
- If using a time restriction, confirm you are within the configured window (end time is exclusive)
- Check the service worker console for `[Blocky] Synced N rules` after saving the rule

**Rule changes don't show up in Chrome**

The host pushes changes within a second or two of saving, with a 30-second polling fallback. If nothing arrives, see the "not connected" checklist above.

---

## Building from source

```bash
# Requirements: .NET 10 SDK; Node.js 20+ for extension tests

# Build
dotnet build Blocky.sln

# Run all .NET tests
dotnet test Blocky.sln

# Run extension tests
cd extensions/chrome && npm test

# Run the app
dotnet run --project src/Blocky.App/Blocky.App.csproj

# Publish self-contained Windows executables (app + native host into one folder)
dotnet publish src/Blocky.App/Blocky.App.csproj -c Release -r win-x64 --self-contained true -o ./publish
dotnet publish src/Blocky.Host/Blocky.Host.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
```

### Extension ID pinning

The unpacked extension's ID is pinned by the `key` field in `extensions/chrome/manifest.json` (the base64 DER-encoded RSA public key; Chrome derives the ID from its SHA-256). The matching ID is hardcoded in `install.ps1` as the host manifest's `allowed_origins`. If you ever need to regenerate the key pair:

```bash
# Private key (keep out of the repo)
openssl genrsa -out blocky-ext.pem 2048

# manifest.json "key" value
openssl rsa -in blocky-ext.pem -pubout -outform DER | openssl base64 -A

# Resulting extension ID (first 16 bytes of SHA-256 of the DER public key, hex mapped 0-9a-f -> a-p)
openssl rsa -in blocky-ext.pem -pubout -outform DER | openssl dgst -sha256 -binary | xxd -p -c 32 | head -c 32 | tr '0-9a-f' 'a-p'
```

Update `$ExtensionId` in `install.ps1` to the new ID and re-run it.

---

## Releases

GitHub Actions runs tests on every push/PR and builds release packages on version tags (`v*`):

- `blocky.zip` — self-contained `Blocky.exe` + `Blocky.Host.exe` + install/uninstall scripts (no .NET install needed)
- `blocky-chrome-extension.zip` — Chrome extension folder, ready to load unpacked

---

## Roadmap

- [ ] Chrome Web Store listing (removes the need for Developer Mode)
- [ ] Windows installer
- [ ] Update notifications
- [ ] Firefox extension
- [ ] Pomodoro / temporary focus blocks
- [ ] Block statistics

---

## Disclaimer

Experimental tool. Bypasses require Chrome; other browsers are unaffected. Feedback and PRs welcome.
