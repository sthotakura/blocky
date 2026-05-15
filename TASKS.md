# Blocky — Audit & Task List

## Architecture Summary

WPF app → SQLite → Kestrel WS (localhost:8080) → Chrome extension → Chrome DNR rules.
No admin rights, no system proxy. Core concept is correct.

---

## Bugs

### 🔴 Critical

#### 1. CI fails — .NET version mismatch
**File:** `.github/workflows/build.yml:21`
`dotnet-version: '9.0.x'` but both csproj files target `net10.0-windows7.0` and `global.json` pins SDK `10.0.0`. Every CI build fails today.

**Fix:** Change workflow line to `dotnet-version: '10.0.x'`

- [x] Fix `.github/workflows/build.yml` dotnet-version to `10.0.x`

---

#### 2. Chrome MV3 service worker gets killed — extension stops blocking
**File:** `extensions/chrome/background.js`

Chrome kills MV3 service workers after ~30s of inactivity. Extension only calls `connectWebSocket` on `onInstalled` and `onStartup`. If Chrome kills the worker mid-session (very common), WebSocket is gone and never reconnects until browser restart. The 25s heartbeat `setInterval` is also killed with the worker. During testing this is invisible because DevTools keeps the worker alive.

**Fix:** Use `chrome.alarms` to wake the worker and reconnect:

```js
// In background.js — add to bottom
chrome.alarms.create('keepAlive', { periodInMinutes: 0.4 });
chrome.alarms.onAlarm.addListener((alarm) => {
  if (alarm.name === 'keepAlive') connectWebSocket();
});
```

Add `"alarms"` to `manifest.json` permissions array.

- [x] Add `chrome.alarms` keepalive in `background.js`
- [x] Add `"alarms"` permission to `manifest.json`

---

### 🟠 Major

#### 3. Time restriction can't span midnight
**File:** `Blocky/Services/BlockyService.cs:114` and `BlockyWebServer` (same logic)

`now >= start && now < end` always returns false when start > end (e.g. 22:00–06:00). No midnight-crossing support.

**Fix in `IsRuleActiveNow`:**
```csharp
// Replace the return statement at line 114:
bool inWindow = rule.StartTime.Value <= rule.EndTime.Value
    ? currentTime >= rule.StartTime.Value && currentTime < rule.EndTime.Value
    : currentTime >= rule.StartTime.Value || currentTime < rule.EndTime.Value;
return inWindow;
```

Same fix needed in `IsDomainBlockedAsync` (line 69).

- [x] Fix `IsRuleActiveNow` in `BlockyService.cs` for midnight-spanning windows
- [x] Fix same logic in `IsDomainBlockedAsync`
- [x] Add test cases: 22:00–06:00 rule — 23:00 (true), 03:00 (true), 10:00 (false), at start 22:00 (true), at end 06:00 (false)

---

#### 4. Port change in settings has no effect — server keeps old port
**File:** `Blocky/Services/BlockyWebServer.cs`, `Blocky/Services/SettingService.cs`

`SettingService` fires `SettingsUpdated` event on port change, but `BlockyWebServer` never subscribes to it. Changing port in Settings UI does nothing until app restart. No feedback shown to user.

**Fix:** Subscribe to `SettingsUpdated` in `BlockyWebServer.StartAsync`, stop and restart Kestrel on port change.

- [x] Subscribe `BlockyWebServer` to `ISettingsService.SettingsUpdated`
- [x] Implement stop-and-restart of Kestrel host on port change
- [ ] Show UI feedback (toast or status) when port changes take effect

---

#### 5. Two competing system tray libraries in csproj
**File:** `Blocky/Blocky.csproj`

Both `Chapter.Net.WPF.SystemTray` and `Hardcodet.NotifyIcon.Wpf` are referenced. Bloats binary, adds confusion. Pick one.

- [x] Remove the unused tray library from `Blocky.csproj`

---

### 🟡 Minor / Code Quality

#### 6. `BlockyService` stores `ICachedBlockyRuleRepo` as `IBlockyRuleRepo`
**File:** `Blocky/Services/BlockyService.cs:12`

Constructor param typed as `ICachedBlockyRuleRepo` but stored as `readonly IBlockyRuleRepo _repo`. Works via polymorphism but the downcast at the field level is misleading — the injected type annotation is pointless.

- [ ] ~~Change field type to `ICachedBlockyRuleRepo`~~ — kept as `IBlockyRuleRepo` intentionally; allows injecting alternative implementations later

---

#### 7. `CachedBlockyRuleRepo.GetByIdAsync` can cache `null`
**File:** `Blocky/Services/CachedBlockyRuleRepo.cs:30`

If rule doesn't exist, `_cache.Set(ruleKey, null)` stores null. `_cache.Get<BlockyRule>()` returns null for both "not in cache" and "cached null". Every call for a non-existent ID hits the DB repeatedly.

- [x] Use `TryGetValue` pattern or a sentinel to distinguish cached-null from cache-miss

---

#### 8. `IsDomainBlockedAsync` is dead code in the actual blocking flow
**File:** `Blocky/Services/Contracts/IBlockyService.cs`, `Blocky/Services/BlockyService.cs`

WebServer only calls `GetBlockedDomainsAsync`. Extension does domain matching client-side via DNR regex. `IsDomainBlockedAsync` is never called in production.

- [x] Either remove from interface or add a comment explaining intended use

---

#### 9. `SettingService.GetSettingsAsync` null-guard is unreachable
**File:** `Blocky/Services/SettingService.cs:14–19`

`AppDbContext.OnModelCreating` seeds a `BlockySettings` row via `HasData`. The "create if not found" branch is dead code.

- [x] Remove the redundant create branch in `GetSettingsAsync`

---

#### 10. Broadcast log shows wrong client count
**File:** `Blocky/Services/BlockyWebServer.cs:234`

Count is logged before acquiring the lock and removing stale clients. Log inflates the real number.

- [x] Move log line to after stale client removal inside the lock (done in task 4 rewrite)

---

#### 11. `BlockyRule` timestamps bypass `IDateTimeService`
**File:** `Blocky/Data/BlockyRule.cs:22–24`

`CreatedAt` and `LastUpdated` initialized with `DateTime.UtcNow` at property level. Not controllable in tests.

- ~~[ ] Inject `IDateTimeService` or set in repo/service layer instead~~ — won't fix: audit fields must reflect real wall-clock time; tests can set properties directly if needed

---

## Deployment / Product Gaps

### P0 — Chrome Web Store submission
Currently users must load the extension in Developer Mode (`chrome://extensions` → Load unpacked). Chrome shows a security warning; managed devices block it entirely. **This prevents any real-world use.**

- $5 one-time developer account fee
- 2–5 day review turnaround
- Extension is simple and privacy-friendly (no remote data collection) — should pass easily
- [ ] Create Chrome Web Store developer account
- [ ] Prepare store listing: description, screenshots, privacy policy
- [ ] Submit extension for review

---

### P1 — Windows Installer
Current distribution: raw zip → single EXE. Missing:
- Start on Windows login
- Proper uninstall entry in Programs
- Start Menu shortcut

**Recommended tool:** Inno Setup (free, ~50 lines, CI-scriptable)

- [ ] Write Inno Setup script with: startup registry key, Start Menu shortcut, uninstall entry
- [ ] Add installer build step to `build.yml` (output `BlockySetup.exe`)
- [ ] Upload installer as release artifact alongside zip

---

### P2 — Port 8080 conflict detection
Port 8080 is heavily used (Jupyter, dev servers, Tomcat). If taken, Kestrel throws and app either crashes or silently runs without a server. No UI feedback.

- [x] Fixed port 45678 (removed configurable port — extension can't follow dynamic port changes)
- [x] Deleted Settings UI, SettingsViewModel, SettingService, ISettingsService, BlockySettings, CloseSettingsViewMessage
- [x] Detect port conflict at startup — `IsPortInUse` traverses exception chain for `SocketError.AddressAlreadyInUse`
- [x] Show error dialog on port conflict then shutdown cleanly
- ~~[ ] Consider trying port 8081, 8082 as automatic fallback~~ — won't fix: extension hardcodes port, auto-fallback breaks it

---

### P3 — Auto-update check
No mechanism to notify users of new versions.

**Simple approach:** On startup, call GitHub Releases API, compare versions, show toast if newer.

- [ ] Add version check against GitHub Releases API on app startup
- [ ] Show non-intrusive toast notification when update available
- [ ] Link to GitHub Releases page from the toast

---

### P4 — README / Setup guide
End-to-end setup requires 3 non-obvious manual steps. No documentation.

- [x] Rewrote README: prerequisites, install steps, how it works, troubleshooting, build instructions
- [x] Updated Blocky.Tests/README.md: removed stale deleted test file references, accurate coverage table
- [ ] Add screenshots to README
- [ ] Add link to Chrome Web Store once published

---

## Fix Priority Order

| # | Task | Effort |
|---|------|--------|
| 1 | Fix CI .NET version | 5 min |
| 2 | Fix MV3 service worker keepalive | 1 h |
| 3 | Fix midnight-spanning time restrictions | 1 h |
| 4 | Port change restarts WebServer | 2 h |
| 5 | Remove duplicate tray library | 15 min |
| 6 | Inno Setup installer | 4 h |
| 7 | Port conflict detection | 1 h |
| 8 | Submit to Chrome Web Store | 1 day |
| 9 | Auto-update check | 2 h |
| 10 | Minor code quality fixes (#6–#11 above) | 2 h |