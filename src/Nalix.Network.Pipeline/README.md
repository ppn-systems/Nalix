# Nalix.Network.Pipeline

> Inbound packet middleware and protection primitives for Nalix runtime dispatch.

## Key Features

| Feature | Source | Description |
| :--- | :--- | :--- |
| 🛡️ **Permission Gate** | `Inbound/PermissionMiddleware.cs` | Enforces `[PacketPermission]` metadata and fails closed when permission metadata is missing. |
| 🚦 **Rate Limiting** | `Inbound/RateLimitMiddleware.cs`, `Throttling/*` | Applies `[PacketRateLimit]` policies or global per-endpoint token buckets. |
| ⚙️ **Concurrency Gate** | `Inbound/ConcurrencyMiddleware.cs`, `Throttling/ConcurrencyGate.cs` | Enforces `[PacketConcurrencyLimit]` with optional queueing and circuit-breaker protection. |
| ⏱️ **Timeout Guard** | `Inbound/TimeoutMiddleware.cs` | Applies `[PacketTimeout]` limits and emits transient timeout directives. |
| 🕒 **Time Sync Service** | `Timekeeping/TimeSynchronizer.cs` | Emits optional Unix-millisecond ticks at a default 16 ms cadence. |

## Installation

```bash
dotnet add package Nalix.Network.Pipeline
```

## Documentation

See [Nalix.Network.Pipeline](https://ppn-systems.me/packages/nalix-network-pipeline/) for source-mapped package details and option references.
