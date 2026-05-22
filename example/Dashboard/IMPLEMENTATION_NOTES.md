# Nalix Admin Dashboard — Implementation Notes

> **Purpose:** Continuity document for a large in-progress rebuild.  
> If context/tokens run out, the next agent reads this file and continues from the TODO section.

---

## Current Goal and Scope

Rebuild `example/Dashboard` from a single-page design into a professional, portable, multi-page Nalix Admin Dashboard.

**Constraints:**
- Self-contained under `example/Dashboard` + `example/Contracts`
- .NET 10 / C# 14 / Blazor Interactive Server / MudBlazor 9.4.0
- No circular dependencies (Contracts → Nalix.Abstractions, Nalix.Codec)
- Backward-compatible (no changes to existing Contracts packet format)
- Portable: users can copy into their own Nalix project

---

## Decisions Made

### Contracts
- **No changes to Contracts.** Binary serialization uses `SerializeLayout.Explicit`. Adding any field breaks packet compatibility. The existing `AuthorityGrant` (0x5100) and `GenerationReport` (0x5101) are sufficient.
- All improvements live in the dashboard's own code.

### Architecture
- **One route per metric target** — `/metrics/dispatch`, `/metrics/tasks`, etc.
- **Shared `ReportPollingController`** (singleton hosted service) manages one active target at a time. Pages register on init, deregister on dispose.
- **`MetricPageBase`** base class for all metric pages — handles lifecycle, polling activation, state subscription, auth guard redirect.
- **`IAdminSettingsStore`** → `BrowserAdminSettingsStore` (JS interop). Non-sensitive settings in `localStorage`. API key: **never** in `localStorage`. API key in `sessionStorage` only when `RememberSessionUntilTabClose = true`.
- **`DashboardState`** is trimmed: removed `IsReportNavigationOpen`, `IsConfigView`, `ActiveReportTarget`, `BackendEndpoint/Address/Port` (now from `AdminClientOptions`), `PollIntervalMs/PingIntervalMs/RequestTimeoutMs` (now from `AdminSettings`). Retains: `IsConnected`, `HasApiKey`, `LastError`, `LastPingMs`, `PingSamples`, `Reports`, `Logs`.
- **Navigation** is data-driven via `AppNavigation` + `NavItem` + `NavSection`. Permission-filtered at render time.
- **`NalixAdminClient`** replaces `DashboardTcpClient`. Moved to `Infrastructure/Nalix/`. Same TcpSession/handshake/authorize/request pattern.
- **Per-target typed domain models** (C# records) with `System.Text.Json` parsers. Fallback to generic `ReportMetricLayoutBuilder` for unknown fields.
- **`ReportDescriptor`** per target: route, title, description, default/min/max poll interval.
- MudBlazor `MudLayout` / `MudAppBar` / `MudDrawer` / `MudNavMenu` used for shell layout.

### Polling Design
- `ReportPollingController` is `BackgroundService` + implements `IReportPollingController`.
- Internal `CancellationTokenSource _pollCts` replaced atomically on `Activate()` / `Deactivate()`.
- `Activate(target, route, intervalMs)` — sets active target and restarts poll loop.
- `Deactivate(route)` — clears only if `ActiveRoute == route` (prevents stale deactivation).
- Loop: request → delay(intervalMs) → repeat. On cancel: restart loop. On error: backoff.
- `Pause()` / `Resume()` suspend/restart the loop without changing active target.
- `RequestOnceAsync(target, ct)` — fires a single request outside the loop (manual refresh).
- `PingKeepAliveService` is a separate `BackgroundService` for periodic pings.

### Security
- API key: never logged, never in `localStorage`, never rendered in HTML.
- `DataJson` parsed by `System.Text.Json` — never string-concatenated into HTML.
- `BrowserAdminSettingsStore` separates settings (localStorage) from API key (sessionStorage only).
- Clear sessionStorage on logout (explicit `ClearApiKeyAsync()` in settings store).
- `RawJsonDialog` is the only place raw JSON is shown, behind an explicit user action.

### Settings Storage
- `AdminSettings` POCO with all configurable values.
- `IAdminSettingsStore` interface: `LoadAsync`, `SaveAsync`, `GetApiKeyAsync`, `SetApiKeyAsync`, `ClearApiKeyAsync`.
- `BrowserAdminSettingsStore`: settings JSON in `localStorage["nalix-admin-settings"]`. API key in `sessionStorage["nalix-admin-apikey"]` (only when `RememberSessionUntilTabClose`).
- JS module at `wwwroot/adminStorage.js` handles interop.

### Navigation Model
```
NavSection("Dashboard") → [NavItem("Overview", "/")]
NavSection("Metrics")   → [Dispatch, Tasks, Buffers, Connections, Instances, Object Pools, Connection Guard]
NavSection("System")    → [Diagnostics, Settings]
```
- Login page (`/login`) has no nav shell (different layout).
- All non-login pages require `HasApiKey`. If false → redirect to `/login`.

---

## Files Created / Modified

### Deleted (old architecture)
- `Application/Polling/DashboardPollingService.cs` ✅
- `Application/Reports/DashboardReportTargets.cs` ✅
- `Application/Reports/GenerationReportDataParser.cs` ✅
- `Application/Abstractions/IDashboardClient.cs` ✅
- `Application/Options/DashboardOptions.cs` ✅
- `Infrastructure/Tcp/DashboardTcpClient.cs` ✅
- `Components/Pages/Home.razor` + `.cs` + `.css` ✅
- `Components/Layout/NavMenu.razor` + `.css` ✅
- `Components/Panels/` (entire directory) ✅

### Created So Far (Phase 1 — partial, some writes failed)
> Files with ✅ are confirmed written. Files with ⬜ need to be written.

---

## New File Manifest (complete list — tick off as created)

### Domain Models
- ⬜ `Domain/Reports/Dispatch/DispatchReport.cs`
- ⬜ `Domain/Reports/Tasks/TasksReport.cs`
- ⬜ `Domain/Reports/Buffers/BuffersReport.cs`
- ⬜ `Domain/Reports/Connections/ConnectionsReport.cs`
- ⬜ `Domain/Reports/Instances/InstancesReport.cs`
- ⬜ `Domain/Reports/ObjectPools/ObjectPoolsReport.cs`
- ⬜ `Domain/Reports/ConnectionGuard/ConnectionGuardReport.cs`

### Application — Abstractions
- ⬜ `Application/Abstractions/IAdminClient.cs`
- ⬜ `Application/Abstractions/IReportParser.cs`
- ⬜ `Application/Abstractions/IReportPollingController.cs`
- ⬜ `Application/Abstractions/IAdminSettingsStore.cs`

### Application — Navigation
- ⬜ `Application/Navigation/NavItem.cs`
- ⬜ `Application/Navigation/NavSection.cs`
- ⬜ `Application/Navigation/AppNavigation.cs`

### Application — Settings
- ⬜ `Application/Settings/AdminSettings.cs`

### Application — Reports (descriptors + parsers)
- ⬜ `Application/Reports/ReportDescriptor.cs`
- ⬜ `Application/Reports/ReportDescriptorRegistry.cs`
- ⬜ `Application/Reports/ReportParserRegistry.cs`
- ⬜ `Application/Reports/Dispatch/DispatchReportParser.cs`
- ⬜ `Application/Reports/Tasks/TasksReportParser.cs`
- ⬜ `Application/Reports/Buffers/BuffersReportParser.cs`
- ⬜ `Application/Reports/Connections/ConnectionsReportParser.cs`
- ⬜ `Application/Reports/Instances/InstancesReportParser.cs`
- ⬜ `Application/Reports/ObjectPools/ObjectPoolsReportParser.cs`
- ⬜ `Application/Reports/ConnectionGuard/ConnectionGuardReportParser.cs`

### Application — Services
- ⬜ `Application/Services/ReportPollingController.cs`
- ⬜ `Application/Services/PingKeepAliveService.cs`

### Application — State (updated)
- ⬜ `Application/State/IDashboardStateReader.cs` (trimmed)
- ⬜ `Application/State/IDashboardStateWriter.cs` (trimmed)
- ⬜ `Application/State/DashboardState.cs` (trimmed)

### Infrastructure
- ⬜ `Infrastructure/Options/AdminClientOptions.cs`
- ⬜ `Infrastructure/Nalix/NalixAdminClient.cs`
- ⬜ `Infrastructure/BrowserStorage/BrowserAdminSettingsStore.cs`
- ⬜ `Infrastructure/Serialization/ReportJsonParser.cs`

### Presentation — Base
- ⬜ `Presentation/Pages/MetricPageBase.cs`

### Components — Layout
- ⬜ `Components/Layout/MainLayout.razor` (rewritten)
- ⬜ `Components/Layout/MainLayout.razor.css`
- ⬜ `Components/Layout/AdminNavMenu.razor`
- ⬜ `Components/Layout/AdminNavMenu.razor.css`

### Components — Shell
- ⬜ `Components/Shell/ReportPageShell.razor`
- ⬜ `Components/Shell/ReportPageShell.razor.css`
- ⬜ `Components/Shell/PollingToolbar.razor`
- ⬜ `Components/Shell/PollingToolbar.razor.css`

### Components — Cards
- ⬜ `Components/Cards/MetricCard.razor`
- ⬜ `Components/Cards/MetricCardGrid.razor`
- ⬜ `Components/Cards/MetricCardGrid.razor.css`

### Components — Feedback
- ⬜ `Components/Feedback/LoadingState.razor`
- ⬜ `Components/Feedback/EmptyState.razor`
- ⬜ `Components/Feedback/ErrorState.razor`
- ⬜ `Components/Feedback/StaleState.razor`

### Components — Charts
- ⬜ `Components/Charts/PingChart.razor`
- ⬜ `Components/Charts/PingChart.razor.css`
- ⬜ `Components/Charts/BarChart.razor`
- ⬜ `Components/Charts/BarChart.razor.css`

### Components — Dialogs
- ⬜ `Components/Dialogs/RawJsonDialog.razor`

### Components — Tables
- ⬜ `Components/Tables/ReportTable.razor`

### Pages
- ⬜ `Components/Pages/LoginPage.razor` + `.cs` + `.css`
- ⬜ `Components/Pages/OverviewPage.razor` + `.cs`
- ⬜ `Components/Pages/Metrics/DispatchPage.razor` + `.cs`
- ⬜ `Components/Pages/Metrics/TasksPage.razor` + `.cs`
- ⬜ `Components/Pages/Metrics/BuffersPage.razor` + `.cs`
- ⬜ `Components/Pages/Metrics/ConnectionsPage.razor` + `.cs`
- ⬜ `Components/Pages/Metrics/InstancesPage.razor` + `.cs`
- ⬜ `Components/Pages/Metrics/ObjectPoolsPage.razor` + `.cs`
- ⬜ `Components/Pages/Metrics/ConnectionGuardPage.razor` + `.cs`
- ⬜ `Components/Pages/DiagnosticsPage.razor` + `.cs`
- ⬜ `Components/Pages/SettingsPage.razor` + `.cs`

### Root / Config
- ⬜ `Program.cs` (rewritten)
- ⬜ `Components/App.razor` (updated — add adminStorage.js)
- ⬜ `Components/Routes.razor` (unchanged or minor tweak)
- ⬜ `Components/_Imports.razor` (updated namespaces)
- ⬜ `appsettings.json` (updated)
- ⬜ `appsettings.Development.json` (updated)

### Static Assets
- ⬜ `wwwroot/adminStorage.js`
- ⬜ `wwwroot/assets/logo/nalix-admin.svg`
- ⬜ `wwwroot/assets/illustrations/empty-state.svg`
- ⬜ `wwwroot/assets/illustrations/offline.svg`
- ⬜ `wwwroot/assets/illustrations/no-data.svg`

### Documentation
- ⬜ `README.md`

---

## Report Target → Route → Parser → Domain Model Mapping

| Target | Route | Parser | Domain Model |
|---|---|---|---|
| DISPATCH | /metrics/dispatch | DispatchReportParser | DispatchReport |
| TASKS | /metrics/tasks | TasksReportParser | TasksReport |
| BUFFERS | /metrics/buffers | BuffersReportParser | BuffersReport |
| CONNECTIONS | /metrics/connections | ConnectionsReportParser | ConnectionsReport |
| INSTANCES | /metrics/instances | InstancesReportParser | InstancesReport |
| OBJECT_POOLS | /metrics/object-pools | ObjectPoolsReportParser | ObjectPoolsReport |
| CONNECTION_GUARD | /metrics/connection-guard | ConnectionGuardReportParser | ConnectionGuardReport |

---

## JSON Schemas (confirmed from source)

### DISPATCH (PacketDispatchChannel.WriteReportData)
```json
{
  "UtcNow": "...", "Running": bool, "DispatchLoops": long,
  "TotalPackets": long, "TotalConnections": long, "ReadyConnections": long,
  "WakeSignals": long, "WakeReads": long, "WakeRequested": long,
  "PacketRegistryType": "...",
  "PendingPerPriority": { "priority_name": count },
  "PendingByConnection": [{"EndPoint": "...", "Pending": long}]
}
```
Fallback (InlinePacketDispatcher): `{ "Running": bool }`

### TASKS (TaskManager.Report)
```json
{
  "UtcNow": "...", "RecurringCount": int, "WorkersTotal": int, "WorkersRunning": int,
  "DynamicAdjustmentEnabled": bool, "CurrentConcurrencyLimit": int, "MaxWorkers": int,
  "HighCpuThreshold": double, "LowCpuThreshold": double,
  "ObservingIntervalSeconds": int, "WarmupDurationSeconds": int, "AdjustmentStreakRequired": int,
  "Memory": {"WorkingSetMB": double, "PrivateMB": double, "VirtualMB": double},
  "Process": {"Threads": int, "CompletedWorkItems": long, "ThreadsRunning": int, "Handles": int,
              "GCGen0": long, "GCGen1": long, "GCGen2": long, "ManagedHeapMB": double,
              "UptimeDays": double, "StartTimeUtc": "..."},
  "WorkerExecutionCount": long, "AverageWorkerExecutionTimeMs": double,
  "P95WorkerExecutionTimeMs": double, "P99WorkerExecutionTimeMs": double,
  "AverageWorkerWaitTimeMs": double, "PeakRunningWorkerCount": int,
  "WorkerErrorCount": long, "RecurringExecutionCount": long,
  "AverageRecurringExecutionTimeMs": double, "RecurringErrorCount": long,
  "Recurring": [...], "TopRecurringByFailures": [...], "WorkersByGroup": {...},
  "TopRunningWorkers": [{"Id":"...","Name":"...","Group":"...","StartedUtc":"...","Progress":double,"LastHeartbeatUtc":"..."}]
}
```

### BUFFERS (BufferPoolManager.Report)
```json
{
  "UtcNow": "...", "Initialized": bool, "TotalBuffersConfigured": int, "PoolCount": int,
  "MinBufferSize": int, "MaxBufferSize": int, "EnableTrimming": bool, "EnableAnalytics": bool,
  "FallbackToArrayPool": bool, "TrimIntervalMinutes": int, "DeepTrimIntervalMinutes": int,
  "TrimCycleCount": long, "FallbackCount": long, "BucketCacheHits": long, "BucketCacheMisses": long,
  "PeakMemoryUsageBytes": long, "ThroughputMBps": double,
  "ShrinkSafetyPolicy": {"MinimumRetentionPercent": double, "MaxSingleShrinkStep": double,
                         "MaxShrinkPercentPerCycle": double, "AbsoluteMinimum": int},
  "Pools": [{"BufferSize": int, "Initial": int, "Total": int, "Free": int, "InUse": int,
             "Hits": long, "Expands": long, "Shrinks": long, "UsageRatio": double,
             "MissRate": double, "ShrinkSkipped": long, "BytesReturned": long}],
  "TotalHits": long, "TotalMisses": long, "TotalExpands": long, "TotalShrinks": long, "HitRate": double
}
```

### CONNECTIONS (Connection.Hub)
```json
{"UtcNow": "...", "TotalConnections": long, "ShardCount": int,
 "TotalBytesSent": long, "TotalBytesReceived": long}
```

### INSTANCES (InstanceManager.Report)
```json
{
  "UtcNow": "...", "CachedInstanceCount": int, "InstanceCreationCount": long,
  "InstanceCacheHitCount": long, "SignatureInstanceCount": int, "ActivatorFactoryCount": int,
  "DisposableCount": int, "SlotsInvalidated": long, "TotalGetOrCreateCalls": long,
  "HitRatePermille": long,
  "Instances": [{"Type": "...", "IsDisposable": bool, "Source": "..."}]
}
```

### OBJECT_POOLS (ObjectPoolManager.Report)
```json
{
  "UtcNow": "...", "UptimeSeconds": double, "PoolCount": int, "PeakPoolCount": int,
  "UnhealthyPoolCount": int, "DefaultMaxPoolSize": int, "StartTime": "...",
  "LastHealthCheckTicks": long, "TotalGetOperations": long, "TotalReturnOperations": long,
  "TotalCacheHits": long, "TotalCacheMisses": long, "TotalCreated": long, "TotalDisposed": long,
  "TotalLeaked": long, "CacheHitRate": double, "Throughput": double, "CreationRate": double,
  "NetObjects": long,
  "Pools": [{"Type": "...", "Available": int, "MaxCapacity": int, "IsActive": bool,
             "Gets": long, "Hits": long, "Misses": long, "HitRate": double,
             "LastAccessUtc": "...", "Outstanding": int, "ConsecutiveFailures": int, "Status": "..."}],
  "UnhealthyPools": [...]  // optional, same shape as Pools entries
}
```

### CONNECTION_GUARD (Connection.Guard.Report)
```json
{
  "UtcNow": "...", "MaxPerEndpoint": int, "CleanupIntervalSeconds": int,
  "InactivityThresholdSeconds": int, "TrackedEndpoints": int, "TotalConcurrent": long,
  "TotalAttempts": long, "TotalRejections": long, "TotalCleaned": long, "RejectionRate": double,
  "TopEndpoints": [{"Address": "...", "CurrentConnections": int,
                    "TotalConnectionsToday": long, "LastConnectionUtc": "..."}]
}
```

---

## Key Existing Files to KEEP (do not delete)

These files survive the rebuild unchanged (or with minor namespace additions):

| File | Status |
|---|---|
| `Domain/Logs/DashboardLogEntry.cs` | Keep |
| `Domain/Metrics/DashboardPingSample.cs` | Keep |
| `Domain/Reports/DashboardReportSnapshot.cs` | Keep |
| `Infrastructure/Security/IServerPublicKeyResolver.cs` | Keep |
| `Infrastructure/Security/ServerPublicKeyResolver.cs` | Keep |
| `Presentation/DashboardDisplay.cs` | Keep |
| `Presentation/NumberDisplayFormatter.cs` | Keep |
| `Presentation/Metrics/PingChartPointBuilder.cs` | Keep |
| `Presentation/ReportValues/ReportMetricLayoutBuilder.cs` | Keep |
| `Presentation/ReportValues/ReportRecordChartBuilder.cs` | Keep |
| `Presentation/ReportValues/ReportRecordLayoutBuilder.cs` | Keep |
| `Presentation/ReportValues/ReportValueFormatter.cs` | Keep |
| `Presentation/ReportValues/ReportValueParser.cs` | Keep |
| `Components/ReportValueView.razor` + `.css` | Keep |
| `Components/Pages/Error.razor` | Keep |
| `Components/Pages/NotFound.razor` | Keep |
| `Components/Layout/ReconnectModal.*` | Keep |
| `GlobalSuppressions.cs` | Keep |

---

## Polling Lifecycle (detailed)

```
Page.OnInitializedAsync()
  → RequireLogin() → Nav.NavigateTo("/login") if !HasApiKey
  → PollingController.Activate(target, route, intervalMs)
  → subscribe PollingController.Changed, State.Changed

Page.Dispose()
  → PollingController.Deactivate(route)
  → unsubscribe events

PollingController background loop:
  while running:
    if _activeTarget == null || _paused: idle-delay(50ms); continue
    try:
      await client.RefreshAsync(_activeTarget, linked(stopping, _pollCts))
      _lastSuccessUtc = now
      StateWriter.UpdateReport(snapshot)
      await delay(_intervalMs, linked)
    catch OperationCanceled when _pollCts cancelled: continue (restart)
    catch OperationCanceled when stopping: break
    catch NonFatal: _lastFailureUtc = now; backoff delay; continue

Activate(target, route, intervalMs):
  old = Interlocked.Exchange(ref _pollCts, new CTS)
  old?.Cancel(); old?.Dispose()
  _activeTarget = target; _activeRoute = route; _intervalMs = intervalMs
  _pollingState.Changed?.Invoke()

Deactivate(route):
  if _activeRoute != route: return (stale deactivation guard)
  old = Interlocked.Exchange(ref _pollCts, null)
  old?.Cancel(); old?.Dispose()
  _activeTarget = null; _activeRoute = null
  _pollingState.Changed?.Invoke()
```

---

## Settings/Storage Design

```csharp
class AdminSettings {
    ThemeMode ThemeMode = Dark
    int DefaultPollingIntervalMs = 3000
    Dictionary<string,int> PerPagePollingIntervalMs = {}   // keyed by route
    int PingIntervalMs = 5000
    int RequestTimeoutMs = 5000
    bool AutoReconnect = true
    int MaxReconnectAttempts = 10
    int ReconnectBackoffMinMs = 500
    int ReconnectBackoffMaxMs = 30000
    bool UseTls = false
    string WebSocketPath = "/ws/"
    bool RememberSessionUntilTabClose = false
    bool ShowRawJsonDebug = false
    bool CompactTableDensity = false
    int ChartTimeWindowSeconds = 120
    int MaxChartSamples = 120
    int MaxLogEntries = 250
}

interface IAdminSettingsStore {
    Task<AdminSettings> LoadAsync(CancellationToken ct)
    Task SaveAsync(AdminSettings settings, CancellationToken ct)
    Task<string?> GetApiKeyAsync(CancellationToken ct)
    Task SetApiKeyAsync(string apiKey, AdminSettings settings, CancellationToken ct)
    Task ClearApiKeyAsync(CancellationToken ct)
}
// localStorage key: "nalix-admin-settings"
// sessionStorage key: "nalix-admin-apikey" (only when RememberSessionUntilTabClose)
```

---

## Component Hierarchy

```
MainLayout (MudLayout)
  ├── MudAppBar (logo, hamburger, theme toggle)
  ├── MudDrawer
  │   └── AdminNavMenu (data-driven from AppNavigation)
  └── MudMainContent
      └── @Body
            ├── LoginPage          @page "/login"  (no auth guard, no drawer)
            ├── OverviewPage       @page "/"
            ├── DispatchPage       @page "/metrics/dispatch"
            │   └── ReportPageShell (title, PollingToolbar, feedback states, ChildContent)
            │       └── DispatchMetrics (MetricCardGrid, tables)
            ├── TasksPage          @page "/metrics/tasks"
            ├── BuffersPage        @page "/metrics/buffers"
            ├── ConnectionsPage    @page "/metrics/connections"
            ├── InstancesPage      @page "/metrics/instances"
            ├── ObjectPoolsPage    @page "/metrics/object-pools"
            ├── ConnectionGuardPage @page "/metrics/connection-guard"
            ├── DiagnosticsPage    @page "/diagnostics"
            └── SettingsPage       @page "/settings"
```

---

## DI Registration (Program.cs)

```csharp
// Settings
services.AddSingleton<IAdminSettingsStore, BrowserAdminSettingsStore>()

// State
services.AddSingleton<DashboardState>()
services.AddSingleton<IDashboardStateReader>(sp => sp.GetRequired<DashboardState>())
services.AddSingleton<IDashboardStateWriter>(sp => sp.GetRequired<DashboardState>())

// Client
services.AddSingleton<IServerPublicKeyResolver, ServerPublicKeyResolver>()
services.AddSingleton<IAdminClient, NalixAdminClient>()

// Polling
services.AddSingleton<ReportPollingController>()
services.AddSingleton<IReportPollingController>(sp => sp.GetRequired<ReportPollingController>())
services.AddHostedService(sp => sp.GetRequired<ReportPollingController>())
services.AddHostedService<PingKeepAliveService>()

// Report descriptors + parsers
services.AddSingleton<ReportDescriptorRegistry>()
services.AddSingleton<ReportParserRegistry>()
```

---

## Security and Portability Constraints

1. API key: never in localStorage, never logged, never in HTML output.
2. DataJson: always parsed by System.Text.Json before display. Never string-concatenated as HTML.
3. Raw JSON: only in `RawJsonDialog`, behind explicit user action.
4. Settings storage in localStorage keyed to `nalix-admin-settings` (non-sensitive).
5. `BrowserAdminSettingsStore` uses JS module interop via `wwwroot/adminStorage.js`.
6. `AdminClientOptions` from `appsettings.json` — no machine-specific defaults.
7. `ServerPublicKeyResolver` searches for certificate file relative to working directory. For portability, the public key can also be inlined in `appsettings.json` under `Dashboard.ServerPublicKey`.
8. All polling throttled: one concurrent request, timeout on every request, exponential backoff on failure.
9. Manual "Refresh All" only on Diagnostics page, only on explicit button click, throttled with one-at-a-time sequential requests.
10. `BuildSelfContained` not required. Works with `dotnet run` from the Dashboard folder.

---

## Build / Test Commands

```bash
# Build (run from repo root)
dotnet build src/Nalix.sln --configuration Release

# Run dashboard (after full build)
cd example/Dashboard && dotnet run

# Dashboard URL: http://localhost:57207
```

**Status:** Build not yet run (implementation incomplete).  
Changes are in `example/Dashboard/` only — `src/` is untouched.

---

## TODO Checklist (remaining work)

### Domain Models
- [ ] `Domain/Reports/Dispatch/DispatchReport.cs`
- [ ] `Domain/Reports/Tasks/TasksReport.cs`
- [ ] `Domain/Reports/Buffers/BuffersReport.cs`
- [ ] `Domain/Reports/Connections/ConnectionsReport.cs`
- [ ] `Domain/Reports/Instances/InstancesReport.cs`
- [ ] `Domain/Reports/ObjectPools/ObjectPoolsReport.cs`
- [ ] `Domain/Reports/ConnectionGuard/ConnectionGuardReport.cs`

### Application — Abstractions
- [ ] `Application/Abstractions/IAdminClient.cs`
- [ ] `Application/Abstractions/IReportParser.cs`
- [ ] `Application/Abstractions/IReportPollingController.cs`
- [ ] `Application/Abstractions/IAdminSettingsStore.cs`

### Application — Navigation + Settings
- [ ] `Application/Navigation/NavItem.cs`
- [ ] `Application/Navigation/NavSection.cs`
- [ ] `Application/Navigation/AppNavigation.cs`
- [ ] `Application/Settings/AdminSettings.cs`

### Application — Reports
- [ ] `Application/Reports/ReportDescriptor.cs`
- [ ] `Application/Reports/ReportDescriptorRegistry.cs`
- [ ] `Application/Reports/ReportParserRegistry.cs`
- [ ] `Application/Reports/Dispatch/DispatchReportParser.cs`
- [ ] `Application/Reports/Tasks/TasksReportParser.cs`
- [ ] `Application/Reports/Buffers/BuffersReportParser.cs`
- [ ] `Application/Reports/Connections/ConnectionsReportParser.cs`
- [ ] `Application/Reports/Instances/InstancesReportParser.cs`
- [ ] `Application/Reports/ObjectPools/ObjectPoolsReportParser.cs`
- [ ] `Application/Reports/ConnectionGuard/ConnectionGuardReportParser.cs`

### Application — Services + State
- [ ] `Application/Services/ReportPollingController.cs`
- [ ] `Application/Services/PingKeepAliveService.cs`
- [ ] `Application/State/IDashboardStateReader.cs` (update)
- [ ] `Application/State/IDashboardStateWriter.cs` (update)
- [ ] `Application/State/DashboardState.cs` (update)

### Infrastructure
- [ ] `Infrastructure/Options/AdminClientOptions.cs`
- [ ] `Infrastructure/Nalix/NalixAdminClient.cs`
- [ ] `Infrastructure/BrowserStorage/BrowserAdminSettingsStore.cs`
- [ ] `Infrastructure/Serialization/ReportJsonParser.cs`

### Presentation Base
- [ ] `Presentation/Pages/MetricPageBase.cs`

### Components — Layout
- [ ] `Components/Layout/MainLayout.razor` (rewrite)
- [ ] `Components/Layout/MainLayout.razor.css`
- [ ] `Components/Layout/AdminNavMenu.razor`
- [ ] `Components/Layout/AdminNavMenu.razor.css`

### Components — Shell
- [ ] `Components/Shell/ReportPageShell.razor`
- [ ] `Components/Shell/ReportPageShell.razor.css`
- [ ] `Components/Shell/PollingToolbar.razor`
- [ ] `Components/Shell/PollingToolbar.razor.css`

### Components — Cards, Feedback, Charts, Tables, Dialogs
- [ ] `Components/Cards/MetricCard.razor`
- [ ] `Components/Cards/MetricCardGrid.razor` + `.css`
- [ ] `Components/Feedback/LoadingState.razor`
- [ ] `Components/Feedback/EmptyState.razor`
- [ ] `Components/Feedback/ErrorState.razor`
- [ ] `Components/Feedback/StaleState.razor`
- [ ] `Components/Charts/PingChart.razor` + `.css`
- [ ] `Components/Charts/BarChart.razor` + `.css`
- [ ] `Components/Dialogs/RawJsonDialog.razor`
- [ ] `Components/Tables/ReportTable.razor`

### Pages
- [ ] `Components/Pages/LoginPage.razor` + `.cs` + `.css`
- [ ] `Components/Pages/OverviewPage.razor` + `.cs`
- [ ] `Components/Pages/Metrics/DispatchPage.razor` + `.cs`
- [ ] `Components/Pages/Metrics/TasksPage.razor` + `.cs`
- [ ] `Components/Pages/Metrics/BuffersPage.razor` + `.cs`
- [ ] `Components/Pages/Metrics/ConnectionsPage.razor` + `.cs`
- [ ] `Components/Pages/Metrics/InstancesPage.razor` + `.cs`
- [ ] `Components/Pages/Metrics/ObjectPoolsPage.razor` + `.cs`
- [ ] `Components/Pages/Metrics/ConnectionGuardPage.razor` + `.cs`
- [ ] `Components/Pages/DiagnosticsPage.razor` + `.cs`
- [ ] `Components/Pages/SettingsPage.razor` + `.cs`

### Root + Config
- [ ] `Program.cs` (rewrite)
- [ ] `Components/App.razor` (add adminStorage.js ref)
- [ ] `Components/_Imports.razor` (update namespaces)
- [ ] `appsettings.json` (update)
- [ ] `appsettings.Development.json` (update)

### Static Assets
- [ ] `wwwroot/adminStorage.js`
- [ ] `wwwroot/assets/logo/nalix-admin.svg`
- [ ] `wwwroot/assets/illustrations/empty-state.svg`
- [ ] `wwwroot/assets/illustrations/offline.svg`
- [ ] `wwwroot/assets/illustrations/no-data.svg`

### Documentation
- [ ] `README.md`

---

## Known Risks / Unresolved Questions

1. **BrowserAdminSettingsStore JS interop timing**: `IJSRuntime` is only available after SignalR connects (Blazor Server). Session restore (reading API key from sessionStorage) must happen in `OnAfterRenderAsync(firstRender: true)` in `MainLayout`, not in `OnInitializedAsync`.

2. **Polling controller and Blazor Server circuit isolation**: The `ReportPollingController` is a singleton shared across all Blazor circuits. The `Activate/Deactivate` with route guards prevents crosstalk, but multiple browser tabs will share one controller (expected behavior — only one target polled at a time across all tabs).

3. **ReportPollingController target conflict on multi-tab**: If two browser tabs have different metric pages open, whichever calls `Activate` last wins. This is acceptable behavior — the singleton only supports one active report at a time.

4. **`DashboardState.Reports` cache eviction**: Currently all 7 report snapshots are held in memory forever. On navigation, old snapshots remain (by design for "stale" state display). No eviction is needed.

5. **`InlinePacketDispatcher` fallback JSON**: Only emits `{"Running": bool}`. The DISPATCH page must handle this gracefully (show minimal metrics, not error).

6. **`MetricPageBase` + Blazor lifecycle**: Pages using `MetricPageBase` must call `base.OnInitializedAsync()` and `base.Dispose()` if they override.

7. **MudBlazor 9.4.0 breaking API**: Some MudBlazor APIs changed in v9. Use `MudTextField`, `MudSelect`, `MudNavMenu`, `MudNavLink`, `MudDrawer` as primary components. Avoid deprecated MudBlazor v6/v7 patterns.
