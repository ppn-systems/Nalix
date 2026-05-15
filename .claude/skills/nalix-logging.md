# Nalix.Logging

## Role

High-performance asynchronous logging subsystem. Provides a channel-based non-blocking logging pipeline with pluggable sinks (console, file), batched I/O, and a unified `NLogix` facade.

**Dependencies:** `Nalix.Abstractions`, `Nalix.Framework`

## Directory Structure

```
Nalix.Logging/
├── Abstractions/        # Logging abstractions and contracts
├── Exceptions/          # Logging-specific exceptions
├── Extensions/          # Logging extension methods
├── Formatters/          # Log message formatters
├── Internal/            # Internal pooling and channel helpers
├── Options/             # Logging configuration options
├── Sinks/               # Log output targets
│   ├── BatchConsoleLogTarget.cs    # Batched console output
│   └── BatchFileLogTarget.cs       # Batched file I/O with buffering
├── NLogix.cs            # Main logging facade (static API)
├── NLogix.Host.cs       # Host integration for NLogix
└── NLogixDistributor.cs # Channel-based log message distributor
```

## Key Components

### NLogix Facade

- `NLogix` — Static entry point for all logging calls.
- `NLogix.Host` — Host-level integration for startup/shutdown lifecycle.

### NLogixDistributor

Channel-based (`System.Threading.Channels`) log message distributor:
- Non-blocking writes from producer threads.
- Background consumer distributes to registered sinks.
- Zero-lock design for high-throughput scenarios.

### Sinks

| Sink | Purpose |
| :--- | :--- |
| `BatchConsoleLogTarget` | Batched console output with color formatting. |
| `BatchFileLogTarget` | Batched file I/O with configurable buffer size and flush intervals. |

Both sinks use batching to minimize I/O system calls under heavy load.

## Performance Rules

- Logging calls MUST NOT block the caller thread.
- Use `NLogix` facade — do NOT create custom logging pipelines.
- Batch file sink minimizes `FileStream.Write` calls via internal buffering.
- Log level filtering should happen as early as possible (before message formatting).

## Anti-Patterns

- Do NOT use `Console.WriteLine` directly — use `NLogix`.
- Do NOT create synchronous file sinks — always use batched variants.
- Do NOT log sensitive data (keys, tokens, passwords).
