# Nalix.Framework

Dependency injection, task orchestration, object pooling, identifiers, and diagnostics for Nalix.

Nalix.Framework is the shared runtime services package. It provides the object and buffer pools,
singleton container, Snowflake IDs, task workers, recurring scheduling, and report registries used
by the networking stack.

## Install

```bash
dotnet add package Nalix.Framework
```

## What It Provides

| Area | Purpose | Main types |
| :--- | :--- | :--- |
| Injection | Thread-safe service registration and activation | `InstanceManager`, `SingletonBase<T>` |
| Tasks | Priority workers, recurring jobs, and handles | `TaskManager`, `IWorker`, `IWorkerHandle`, `IRecurringHandle` |
| Object pooling | Reusable object pools with diagnostics | `ObjectPoolManager` |
| Buffer pooling | Shard-aware buffer slab pools | `BufferPoolManager` |
| Identifiers | Chronologically sortable 64-bit IDs | `Snowflake`, `ISnowflake` |
| Diagnostics | Telemetry reports and diagnostic events | `ReportRegistry`, `DiagnosticsEvents` |
| Options | Framework-level configuration models | `SnowflakeOptions`, `TaskManagerOptions`, `BufferOptions`, `ObjectPoolOptions` |

## Minimal Object Pool

```csharp
using Nalix.Abstractions;
using Nalix.Framework.Memory.Objects;

public sealed class ConnectionState : IPoolable
{
    public string SessionKey { get; set; } = string.Empty;

    public void ResetForPool() => this.SessionKey = string.Empty;
}

ObjectPoolManager pool = new();
pool.Prealloc<ConnectionState>(32);

ConnectionState state = pool.Get<ConnectionState>();
state.SessionKey = "session-1";
pool.Return(state);
```

## Design Notes

- Pools are explicit ownership boundaries. Return rented objects exactly once.
- `InstanceManager` is the Nalix service container; avoid adding a second container unless hosting integration requires it.
- Snowflake IDs are time ordered and suitable for connection and session identity.

## Documentation

- Package guide: https://ppn.io.vn/packages/nalix-framework/
- API reference: https://ppn.io.vn/api/framework/
