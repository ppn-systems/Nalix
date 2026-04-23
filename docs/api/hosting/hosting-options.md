# Hosting Options

`HostingOptions` configures the server-side environment, including console behavior, thread-pool tuning, and production stability features.

## Source Mapping

- `src/Nalix.Network.Hosting/Options/HostingOptions.cs`

## Properties

| Property | Type | Default | Purpose |
|---|---|---:|---|
| `DisableConsoleClear` | `bool` | `false` | If `true`, prevents the console from being cleared on startup. |
| `DisableStartupBanner` | `bool` | `false` | If `true`, hides the Nalix banner and diagnostic info. |
| `MinWorkerThreads` | `int` | `0` | Min worker threads (0 = system default). Recommended: `CPU Count * 2`. |
| `MinCompletionPortThreads` | `int` | `0` | Min IOCP threads (0 = system default). |
| `EnableGlobalExceptionHandling` | `bool` | `true` | Catch and log unhandled exceptions before process exit. |
| `EnableHighPrecisionTimer` | `bool` | `true` | (Windows) Enable `timeBeginPeriod(1)` for sub-millisecond timer resolution. |

## Production Stability

### Global Exception Handling
When `EnableGlobalExceptionHandling` is true, Nalix registers handlers for:
- `AppDomain.CurrentDomain.UnhandledException`: Logs critical failures and flushes configuration before exit.
- `TaskScheduler.UnobservedTaskException`: Logs and suppresses unobserved task errors to prevent process crashes.

### ThreadPool Tuning
For high-concurrency servers, setting `MinWorkerThreads` ensures that the .NET ThreadPool can respond immediately to burst traffic without waiting for its injection algorithm to scale up.

### High-Precision Timer (Windows)
Enabling `EnableHighPrecisionTimer` calls the Windows `timeBeginPeriod` API. This improves the resolution of `Task.Delay` and other timer-based operations, which is critical for low-latency network heartbeat and timeout logic.

## Startup Diagnostics

On startup, Nalix prints an initialization report including:
- **PID**: Process identifier.
- **Arch**: Process architecture (e.g., X64, Arm64).
- **Runtime**: .NET version and environment description.
- **Config Path**: The path to the loaded configuration file (usually `server.ini`).
- **Server GC**: A warning is printed if Server Garbage Collection is not enabled, as this is critical for high-throughput networking.

## Related APIs

- [Network Application](./network-application.md)
- [Configuration System](../../concepts/runtime/configuration.md)
