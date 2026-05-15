# Nalix.Framework

## Role

Foundational runtime utilities layer. Provides DI/instance management, Snowflake identifiers, memory pool managers, object pooling, and task orchestration. Acts as the "runtime services" bridge between low-level `Nalix.Environment` and higher-level projects.

**Dependencies:** `Nalix.Abstractions`, `Nalix.Environment`, `Nalix.Codec`

## Directory Structure

```
Nalix.Framework/
├── Extensions/          # Framework extension methods
├── Identifiers/         # Snowflake distributed ID generator
├── Injection/           # InstanceManager (lightweight DI container)
├── Memory/              # BufferPoolManager, ObjectPoolManager implementations
├── Options/             # Framework configuration option POCOs
├── Tasks/               # TaskManager, worker scheduling, recurring task infrastructure
├── DiagnosticsEvents.cs # EventSource diagnostics for framework operations
```

## Key Components

### Instance Manager (DI)

Nalix uses a custom lightweight DI container (`InstanceManager`), not `Microsoft.Extensions.DependencyInjection`. This is intentional for zero-allocation and tight control over object lifecycle.

### Snowflake Identifiers

- Implements `ISnowflake` from Abstractions.
- Twitter-style distributed unique IDs with timestamp + worker + sequence components.
- Used for connection IDs, packet correlation, and session tokens.

### Memory Pooling

| Type | Purpose |
| :--- | :--- |
| `BufferPoolManager` | Implements `IBufferPoolManager`. Manages pooled `BufferLease` instances. |
| `ObjectPoolManager` | Implements `IObjectPoolManager`. Generic object pool with `IPoolable` lifecycle. |

### Task Orchestration

- `TaskManager` — Implements `ITaskManager`. Manages background workers and recurring tasks.
- Worker priority system: `WorkerPriority.Low`, `Normal`, `High`, `Critical`.
- Recurring tasks with configurable intervals via `IRecurringOptions`.

## Performance Rules

- Object pools must use `IPoolable.Reset()` before returning to pool.
- `BufferPoolManager` MUST NOT allocate new buffers on the hot path — rent from pool.
- Snowflake generation must be lock-free or use minimal synchronization.
- TaskManager workers should not block the thread pool.

## Anti-Patterns

- Do NOT use `Microsoft.Extensions.DependencyInjection` — use `InstanceManager`.
- Do NOT create ad-hoc object pools — use `ObjectPoolManager`.
- Do NOT bypass `BufferPoolManager` for buffer allocation in hot-path code.
