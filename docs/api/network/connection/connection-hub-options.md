# Connection Hub Options

`ConnectionHubOptions` configures capacity, drop policy, sharding, and fan-out/disconnect behavior for `ConnectionHub`.

## Audit Summary

- Previous page listed properties that are not present in the current implementation.
- Required correction to actual option surface.

## Missing Content Identified

- Exact current properties and validation constraints.
- Clear note on `MaxConnections` and `ParallelDisconnectDegree` special values.

## Improvement Rationale

Accurate options documentation prevents misconfiguration in production.

## Source Mapping

- `src/Nalix.Network/Options/ConnectionHubOptions.cs`

## Current Properties

| Property | Meaning | Default |
|---|---|---:|
| `MaxConnections` | Maximum concurrent connections (`-1` = unlimited, `0` invalid). | `-1` |
| `DropPolicy` | Admission behavior at capacity (`DropNewest`, `DropOldest`, `Block`, `Coalesce`). | `DropNewest` |
| `ParallelDisconnectDegree` | Parallelism for bulk disconnect (`-1` = runtime default, `0` invalid). | `-1` |
| `BroadcastBatchSize` | Batch size for broadcast fan-out (`0` = no batching mode). | `0` |
| `ShardCount` | Number of connection storage shards (>= 1). | `Environment.ProcessorCount` |
| `IsEnableLatency` | Enables latency measurement diagnostics. | `true` |

## Validation Rules

- Data annotations validate numeric ranges.
- Additional guards:
  - `MaxConnections` cannot be `0`.
  - `ParallelDisconnectDegree` cannot be `0`.

## Best Practices

- Tune `MaxConnections`, `DropPolicy`, `ShardCount`, and `BroadcastBatchSize` together.
- Keep `ShardCount` >= CPU core count for high-connection workloads, then benchmark.

## Related APIs

- [Connection Hub](./connection-hub.md)
- [Network Options](../options/options.md)
