# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build Blocky.sln

# Run all .NET tests
dotnet test Blocky.sln

# Run a specific test class
dotnet test --filter FullyQualifiedName~ScheduleEvaluatorTests

# Run Chrome extension tests (pure-module tests via node --test)
cd extensions/chrome && npm test

# Run the app
dotnet run --project src/Blocky.App/Blocky.App.csproj

# Publish (self-contained; both outputs go to one folder so install.ps1 works)
dotnet publish src/Blocky.App/Blocky.App.csproj -c Release -r win-x64 --self-contained true -o ./publish
dotnet publish src/Blocky.Host/Blocky.Host.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish

# Add an EF Core migration (design-time factory lives in Blocky.Core)
dotnet ef migrations add <Name> --project src/Blocky.Core
```

## Architecture

Blocky is a Windows-only website blocker with three cooperating pieces and **no network ports anywhere**:

1. **Blocky.App** (WPF, .NET 10) — a pure rule *editor*. It writes rules to SQLite and has no runtime channel to Chrome; the database is the interface.
2. **Blocky.Host** (console exe, published as `Blocky.Host.exe`) — a Chrome *native-messaging host*. Chrome spawns it and talks over stdio (4-byte LE length + UTF-8 JSON frames). It watches the database and pushes **full rule definitions** (never computed snapshots) to the extension.
3. **Chrome extension** (MV3, `extensions/chrome/`) — the *enforcer*. It persists rules in `chrome.storage.local`, evaluates time windows against the local clock, and maintains Chrome DNR dynamic rules. Enforcement works with the app closed, the host dead, and across browser restarts.

### Data flow

```
Blocky.App ──writes──▶ %LocalAppData%\Blocky\blocky.db (SQLite, WAL)
                              │  FileSystemWatcher on blocky.db* (WAL commits touch -wal)
                              │  + 300 ms debounce + 30 s poll fallback
                              ▼
                        Blocky.Host ──stdio push (rev-hashed rules)──▶ extension
                              ▲                                          │
        Chrome spawns it via HKCU\...\NativeMessagingHosts\com.blocky.host
                                                                         ▼
                                        chrome.storage.local → evaluator → DNR rules
                                        chrome.alarms fire at next window boundary
```

### Projects

**src/Blocky.Core** — shared by app and host
- `Data/` — `BlockyRule` entity, `AppDbContext`, `BlockyRuleRepo`/`IBlockyRuleRepo` (uses `IDbContextFactory<AppDbContext>` for thread-safe per-operation contexts), `DbPaths` (well-known file locations), `DbInitializer` (runs `Migrate()`, baselines databases created by old `EnsureCreated` versions, enables WAL)
- `Migrations/` — EF Core migrations; schema changes go through `dotnet ef migrations add`, never `EnsureCreated`
- `Evaluation/ScheduleEvaluator` — pure static time-window logic (minutes since midnight, 0–1439; start > end = overnight span; **end-exclusive**). This is the C# reference implementation; `extensions/chrome/evaluator.js` must match it (enforced by shared contract vectors)
- `Protocol/` — wire DTOs (`Messages.cs`, System.Text.Json source-generated, camelCase) and `RulesMessageFactory` (canonical serialization + SHA-256 `rev` content hash)

**src/Blocky.App** — WPF only (no server code)
- ViewModels use CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`)
- VMs never touch Views/`MessageBox`/`Process.Start` — they depend on `IDialogService` (rule dialog, error, confirm) and `IShellService` (open file); DI in `App.xaml.cs` (all singletons)
- `MainWindowViewModel` — awaited async load with a `LoadError` state (no fire-and-forget)
- `BlockyService` — rule CRUD + `GetActiveDomains` delegating to `ScheduleEvaluator` (UI "active now" display only; enforcement lives in the extension)
- Time is injected via .NET `TimeProvider` (mock with a fake in tests)
- MahApps.Metro window chrome; system tray via Chapter.Net.WPF.SystemTray; starts minimized
- `install.ps1`/`uninstall.ps1` (copied to output) — startup registration + native-messaging host manifest/registry (`HKCU\Software\Google\Chrome\NativeMessagingHosts\com.blocky.host`)

**src/Blocky.Host** — ~5 files, stateless except the last-pushed rev
- `Program.cs` — stdout belongs exclusively to the protocol; Serilog logs to `%LocalAppData%\Blocky\logs\Blocky.Host-*.log`
- `StdioFraming` — native-messaging frame read/write
- `HostSession` — replies to `get-rules`, pushes on DB change, suppresses same-rev pushes; missing DB ⇒ `dbMissing: true` (authoritative empty — extension clears all rules)
- `DbChangeMonitor` — FileSystemWatcher + debounce + poll backstop
- Unknown message types / newer protocol versions are logged and ignored, never fatal

**extensions/chrome** — ES modules, `"type": "module"`
- `background.js` — thin; every wake path (install, startup, any alarm, host push) funnels through one idempotent `reconcile()`: apply rules → ensure port connected. No other state machine, no keepalive hacks
- `evaluator.js` (pure: rules × now → active domains + next boundary), `dnr.js` (desired-rule computation + diff), `nativePort.js` (connect/handlers)
- DNR: `requestDomains` conditions (domain + subdomains), redirect to `blocked.html`; stable per-domain integer IDs (`domainIdMap`, append-only); one atomic `updateDynamicRules({removeRuleIds, addRules})` call; >4,500 active domains ⇒ truncate + "!" badge
- Alarms: one-shot `boundary` at next window edge; hourly `resync` (clock/timezone drift); `reconnect` every 1 min only while disconnected
- Failure modes: host unreachable ⇒ keep enforcing stored rules (fail closed) + "!" badge; `dbMissing` ⇒ clear everything
- `manifest.json` has a `key` field pinning the unpacked extension ID to `dnmkjkbjklkbjakdjhfjpcjknglifnmb`; that ID is hardcoded in `install.ps1` `allowed_origins`. Changing either breaks the native-messaging handshake (keygen documented in README)

### Build constraints

`TreatWarningsAsErrors` is enabled in all projects — all warnings must be resolved before committing.

## Testing

- **NUnit 4.x** + **Moq** + **FluentAssertions** for .NET; `node --test` for extension modules
- `tests/Blocky.Core.Tests` — `ScheduleEvaluatorTests`, `BlockyRuleRepoTests` (EF InMemory), `DbInitializerTests`, `RulesMessageFactoryTests`, `ContractVectorTests`
- `tests/Blocky.App.Tests` — `BlockyServiceTests`, `MainWindowViewModelTests`, `RuleDialogViewModelTests` (VMs tested against fakes)
- `tests/Blocky.Host.Tests` — `StdioFramingTests` (round-trips), `HostSessionTests` (over in-memory streams)
- `tests/contract/time-window-vectors.json` — shared vectors loaded by **both** `ContractVectorTests` (NUnit) and `extensions/chrome/tests/evaluator.test.js`, so C#/JS evaluator drift breaks the build. Any time-window semantics change must update the vectors and both implementations
- Async throw assertions must be awaited: `await act.Should().ThrowAsync<...>()`

## CI/CD

GitHub Actions (`.github/workflows/build.yml`): tests (dotnet + node) run on every push/PR to `main`; on `v*` tags it additionally publishes `Blocky.exe` + single-file `Blocky.Host.exe` into one folder, zips it (`blocky.zip`) alongside the extension (`blocky-chrome-extension.zip`), and uploads both to the GitHub Release.
