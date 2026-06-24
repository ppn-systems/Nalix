# Nalix.Framework

> High-performance dependency injection, task orchestration, memory/object pooling, unique identity generation, and diagnostic reporting — the foundational engine room of Nalix.

## Key Features

| Feature | Description | Key Concept / Type |
| :--- | :--- | :--- |
| 💉 **Dependency Injection** | Highly optimized, thread-safe service registration and transient/singleton activation container. | `InstanceManager`, `Activator` |
| ⚙️ **Task Orchestration** | Background thread worker priority queuing, recurring scheduling, group cancellation, and diagnostic monitoring. | `TaskManager`, `IWorker`, `IRecurringHandle` |
| 🧠 **Memory & Object Pooling** | Shard-aware memory buffer and reusable object pools with sentinel leak detection. | `ObjectPoolManager`, `BufferPoolManager` |
| 🆔 **Identifiers** | 64-bit globally unique, chronologically sortable distributed identity generation. | `Snowflake`, `ISnowflake` |
| 📊 **Observability** | Solution-wide performance telemetry collection, report registries, and custom diagnostic listeners. | `ReportRegistry`, `DiagnosticsEvents` |

## Key Namespaces

| Namespace | Purpose | Key Types |
| :--- | :--- | :--- |
| `Nalix.Framework` | Root namespace containing observability registries and diagnostics events | `ReportRegistry`, `DiagnosticsEvents` |
| `Nalix.Framework.Injection` | Thread-safe service container and dependency injection activators | `InstanceManager`, `SingletonBase<T>` |
| `Nalix.Framework.Tasks` | High-performance background worker execution, recurring jobs, and scheduler runners | `TaskManager`, `IWorker`, `IWorkerHandle`, `IRecurringHandle` |
| `Nalix.Framework.Identifiers` | Dynamic 64-bit chronological unique Snowflake identifier models | `Snowflake` |
| `Nalix.Framework.Memory.Objects` | Custom reusable object pools, periodic scrubbing, and memory sentinel diagnostics | `ObjectPoolManager` |
| `Nalix.Framework.Memory.Buffers` | High-performance shard-aware pinned memory buffer slab pools | `BufferPoolManager` |
| `Nalix.Framework.Options` | Core framework options POCO settings mapping configurations | `SnowflakeOptions`, `TaskManagerOptions`, `BufferOptions`, `ObjectPoolOptions` |

## Installation

```bash
dotnet add package Nalix.Framework
```

## Quick Example: Object Pooling

```csharp
using System;
using Nalix.Abstractions;
using Nalix.Framework.Memory.Objects;

// 1. Define a class implementing IPoolable
public class ConnectionSession : IPoolable
{
    public string SessionKey { get; set; } = string.Empty;

    public void ResetForPool()
    {
        SessionKey = string.Empty; // Reset state for safe pool reuse
    }
}

// 2. Rent and return using ObjectPoolManager
ObjectPoolManager manager = new();

// Preallocate 32 instances to warm up the pool
manager.Prealloc<ConnectionSession>(32);

// Rent a connection session instance
ConnectionSession session = manager.Get<ConnectionSession>();
session.SessionKey = "Session_abc123";

// ... perform work ...

// Return the instance back to the pool (automatically invoking ResetForPool)
manager.Return(session);
```

## Quick Example: Unique ID Generation (Snowflake)

```csharp
using System;
using Nalix.Abstractions.Identity;
using Nalix.Framework.Identifiers;

// Generate a new 64-bit unique Snowflake ID for a Session entity
Snowflake sessionId = Snowflake.NewId(SnowflakeType.Session);

Console.WriteLine($"Generated ID: {sessionId}");
Console.WriteLine($"Timestamp Component: {sessionId.Value}");
Console.WriteLine($"Machine ID Component: {sessionId.MachineId}");
```

## Documentation

For deep dives into dependency injection, task scheduling, and memory pooling, see the [official documentation](https://ppn.io.vn/concepts/packet-system).


