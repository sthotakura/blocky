# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build Blocky.sln

# Run all tests
dotnet test Blocky.sln

# Run tests with output
dotnet test Blocky.sln --verbosity normal

# Run a specific test class
dotnet test --filter FullyQualifiedName~BlockyServiceTests

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Run the app
dotnet run --project Blocky/Blocky.csproj

# Publish (self-contained Windows executable)
dotnet publish Blocky/Blocky.csproj -c Release -r win-x64 --self-contained true
```

## Architecture

Blocky is a WPF desktop app (.NET 10, Windows-only) that blocks websites by pushing domain rules to a Chrome extension over WebSocket. No admin rights or system proxy required — blocking is enforced entirely through Chrome's Declarative Net Request API.

### Data flow

```
SQLite (EF Core) → CachedBlockyRuleRepo → BlockyService → BlockyWebServer (Kestrel :8080)
                                                                    ↓
                                               Chrome Extension (background.js)
                                                                    ↓
                                                    Chrome DNR dynamic rules
```

### Key layers

**Data** (`Blocky/Data/`)
- `AppDbContext` — EF Core SQLite at `%LocalAppData%\Blocky\blocky.db`; schema created via `EnsureCreated()` on startup (not Migrate)
- `BlockyRule` — entity with Domain, IsEnabled, HasTimeRestriction, StartTime, EndTime
- `BlockySettings` — singleton settings entity (fixed Guid PK); stores `ProxyPort` (default 8080, range 1024–65535)
- `BlockyRuleRepo` / `IBlockyRuleRepo` — repository CRUD; uses `IDbContextFactory<AppDbContext>` for thread-safe per-operation contexts

**Services** (`Blocky/Services/`)
- `CachedBlockyRuleRepo` — thread-safe MemoryCache wrapper over the repo; uses SemaphoreSlim with double-checked locking; invalidates on writes
- `BlockyService` — core logic: domain matching (exact + `www.` subdomain, case-insensitive), time-window evaluation, rule CRUD
- `BlockyWebServer` — Kestrel server on configurable port (from `BlockySettings.ProxyPort`) with two endpoints:
  - `GET /blocked-domains` — JSON array of currently blocked domains
  - `WS /ws` — broadcasts updated domain lists every 30 s; sends initial data immediately on connect; only broadcasts on change
- `BlockyWebServerHostedService` — `IHostedService` wrapper for server lifecycle
- `SettingService` — reads/writes `BlockySettings`; fires `SettingsUpdated` event when `ProxyPort` changes
- `DefaultDateTimeService` — `DateTime.Now`/`UtcNow` abstraction (injected for testability)
- Serilog writes daily log files to `%LocalAppData%\Blocky\logs\Blocky-{yyyyMMdd}.log`; configured via `appSettings.json`

**ViewModels / Views** (`Blocky/ViewModels/`, `Blocky/Views/`)
- ViewModels use CommunityToolkit.Mvvm source generators: `[ObservableProperty]` generates properties, `[RelayCommand]` generates `ICommand` implementations
- `IMessenger` (WeakReferenceMessenger) is used for cross-ViewModel messaging (e.g., `CloseSettingsViewMessage`)
- `MainWindowViewModel` — loads rules on startup, exposes `ObservableCollection<BlockyRule>`, commands for Add/Edit/Remove/Settings/Quit
- `RuleDialogViewModel` — validates domain (3–253 chars, no protocol/path/port), time-restriction toggle (defaults 08:00–19:00), start ≠ end validation
- `SettingsViewModel` — loads and saves `ProxyPort`; uses `ObservableValidator` with `[NotifyDataErrorInfo]` for inline validation
- MahApps.Metro provides the window chrome and DataGrid styling
- System tray via Hardcodet.NotifyIcon.Wpf; app starts minimized to tray

**Chrome Extension** (`extensions/chrome/`)
- MV3 service worker (`background.js`) connects to `ws://localhost:8080/ws`, applies exponential backoff reconnection, heartbeats every 25 s
- Translates received domain arrays into Chrome DNR `regexFilter` dynamic rules

### DI container

`App.xaml.cs` bootstraps `Microsoft.Extensions.DependencyInjection`. All services, repos, and ViewModels are registered as singletons. `IDbContextFactory<AppDbContext>` is used everywhere (not direct `AppDbContext` injection) to safely share the SQLite connection across threads.

### Build constraints

`TreatWarningsAsErrors` is enabled in both Debug and Release configurations — all warnings must be resolved before committing.

### Testing

- **NUnit 4.x** + **Moq** + **FluentAssertions**
- Tests in `Blocky.Tests/`: `BlockyServiceTests`, `CachedBlockyRuleRepoTests`, `BlockyRuleRepoTests`, `RuleDialogViewModelTests`
- `BlockyRuleRepoTests` uses EF Core InMemory provider
- `DefaultDateTimeService` is mocked to control time-based rule evaluation in tests

### CI/CD

GitHub Actions (`.github/workflows/build.yml`) triggers on `v*` tags: runs `dotnet test`, publishes self-contained `Blocky.exe`, zips it with the Chrome extension, and uploads to GitHub Releases.
