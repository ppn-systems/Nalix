# Hosting Options

`HostingOptions` configures the server-side environment, including console behavior, thread-pool tuning, and production stability features.

## Source Mapping

- `src/Nalix.Hosting/Options/HostingOptions.cs`

## Bootstrap lifecycle

The Hosting assembly calls `Bootstrap.Initialize()` from a module initializer, so server defaults are applied automatically when the assembly is loaded. The method can also be called manually when explicit startup ordering is required.

Initialization performs the following source-defined actions:

1. sets the configuration file to `server.ini` under `Directories.ConfigurationDirectory`
2. enables `PacketOptions.EnablePooling` for server throughput
3. loads framework, network, runtime, and hosting options so templates are present in `server.ini`
4. applies configured ThreadPool minimums when either value is greater than `0`
5. registers global exception handlers when enabled
6. enables `timeBeginPeriod(1)` on Windows when high-precision timers are enabled
7. flushes configuration to disk
8. prints startup diagnostics only when `Environment.UserInteractive` is `true` and the banner is not disabled

## Properties

| Property | Type | Default | Purpose |
| --- | --- | ---: | --- |
| `DisableConsoleClear` | `bool` | `false` | If `true`, prevents the console from being cleared before the startup banner. Console clearing is also skipped when output is redirected. |
| `DisableStartupBanner` | `bool` | `false` | If `true`, hides the Nalix banner and diagnostic info. |
| `MinWorkerThreads` | `int` | `0` | Min worker threads (0 = system default). Recommended by the source comment: `processor count * 2`. |
| `MinCompletionPortThreads` | `int` | `0` | Min IOCP threads (0 = system default). |
| `EnableGlobalExceptionHandling` | `bool` | `true` | Catch and log unhandled exceptions before process exit. |
| `EnableHighPrecisionTimer` | `bool` | `true` | On Windows, call `timeBeginPeriod(1)` for improved timer resolution. |

## Production Stability

### Global Exception Handling

When `EnableGlobalExceptionHandling` is true, Nalix registers handlers for:

- `AppDomain.CurrentDomain.UnhandledException`: Logs critical failures and flushes configuration when the process is terminating.
- `TaskScheduler.UnobservedTaskException`: Logs and marks the exception observed to prevent unobserved task escalation.

### ThreadPool Tuning

For high-concurrency servers, setting `MinWorkerThreads` ensures that the .NET ThreadPool can respond immediately to burst traffic without waiting for its injection algorithm to scale up.

`Bootstrap.Initialize()` preserves existing ThreadPool minimums for any value left at `0`. For example, setting only `MinWorkerThreads` keeps the current IOCP minimum.

### High-Precision Timer (Windows)

Enabling `EnableHighPrecisionTimer` calls the Windows `timeBeginPeriod` API. This improves the resolution of `Task.Delay` and other timer-based operations, which is useful for low-latency network heartbeat and timeout logic.

Nalix calls `timeEndPeriod(1)` during process exit only if high-precision timing was successfully enabled.

## Startup Diagnostics

On startup, Nalix prints an initialization report including:

- **Version**: Hosting assembly version.
- **PID**: Process identifier.
- **OS**: Operating system version string.
- **Arch**: Process architecture (for example, X64 or Arm64).
- **Runtime**: .NET runtime description.
- **Config**: The active configuration file path, normally `server.ini`.
- **GC Mode**: Server GC or Workstation GC.
- **Processors**: `Environment.ProcessorCount`.

A warning is printed when Server GC is not enabled.

## Shutdown behavior

Bootstrap subscribes to `Console.CancelKeyPress` and `AppDomain.CurrentDomain.ProcessExit`. On exit it:

- unregisters the process-exit handlers
- cancels all workers from the registered `TaskManager`, when present
- flushes pending configuration changes to disk
- disables the high-precision Windows timer when it was enabled

## Related APIs

- [Network Application](../../hosting/network-application.md)
- [Configuration System](../../concepts/runtime/configuration.md)
