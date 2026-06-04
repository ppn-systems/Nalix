# Connection Hub Options

`ConnectionHubOptions` configures server-wide connection capacity, connection-hub
sharding, broadcast batching, bulk-disconnect parallelism, and latency diagnostics
for `ConnectionHub`.

## Source Mapping

- `src/Nalix.Network/Options/ConnectionHubOptions.cs`
- `src/Nalix.Network/Connections/Connection.Hub.cs`
- `src/Nalix.Hosting/Bootstrap.cs`

## Defaults and Validation

| Property | Default | Validation | Runtime consumer |
| --- | ---: | --- | --- |
| `ParallelDisconnectDegree` | `-1` | `-1..int.MaxValue`; `0` rejected by `Validate()` | `ParallelOptions.MaxDegreeOfParallelism` in hub disposal cleanup. |
| `BroadcastBatchSize` | `0` | `0..int.MaxValue` | Enables `BroadcastBatchedAsync(...)` when greater than zero. |
| `ShardCount` | `max(1, Environment.ProcessorCount)` | `1..int.MaxValue` | Number of internal `ConcurrentDictionary` shards. |
| `IsEnableLatency` | `true` | Boolean | Gates performance timing logs for register, unregister, and broadcast paths. |

`Validate()` runs data-annotation validation, then explicitly rejects `0` for
`ParallelDisconnectDegree`. Use `-1` for the default mode or a positive value for an explicit limit.

## Hosting Initialization

`Bootstrap.Initialize()` materializes this option set during server startup:

```csharp
_ = ConfigurationManager.Instance.Get<ConnectionHubOptions>();
```

This ensures the generated active configuration file includes the connection-hub
capacity and concurrency policy.

## Construction and Sharding

`ConnectionHub` loads and validates `ConnectionHubOptions` in its constructor. It
then derives immutable runtime fields from the options:

- `_shardCount` is clamped with `Math.Max(1, ShardCount)`.
- `_shardMask` and `_isPowerOfTwoShardCount` optimize shard lookup for powers of two.
- Each shard is a `ConcurrentDictionary<ulong, IConnection>`.

Shard selection hashes the `ulong` connection id. Power-of-two shard counts use a
bit mask; other counts use modulo.

## Registration Flow

```mermaid
flowchart TD
    A["RegisterConnection(connection)"] --> B["TryRegisterCore(connection)"]
    B --> C{"Hub disposed?"}
    C -->|Yes| X["Return Disposed"]
    C -->|No| D["Increment active count"]
    D --> N["Add to shard"]
    N --> O["Subscribe OnCloseEvent"]
```

A successful registration increments `_count` before the connection is added
to the selected shard. If shard insertion fails, the `finally` block detaches the
close handler and decrements `_count` to roll back the reservation.

## Broadcast Behavior

`BroadcastAsync(...)` captures a point-in-time connection snapshot using an
`ArrayPool<IConnection>` buffer. The public method then chooses the send strategy:

- `BroadcastBatchSize == 0`: send through `BroadcastCoreAsync(...)` and await all
  incomplete send tasks together.

- `BroadcastBatchSize > 0`: send through `BroadcastBatchedAsync(...)`, renting arrays
  sized to the configured batch and awaiting each full batch before continuing.

`BroadcastWhereAsync(...)` always uses `BroadcastCoreAsync(...)` with a predicate;
it does not use `BroadcastBatchSize`.

Both broadcast implementations rent task/owner arrays and return them in `finally`.
Failed asynchronous sends are mapped back to owning connections for diagnostic logs.

## Bulk Close Behavior

Hub disposal snapshots current connections, detaches the close event
handler from each connection, then disposes connections through `Parallel.ForEach`.
`ParallelDisconnectDegree` is passed directly to
`ParallelOptions.MaxDegreeOfParallelism`:

- `-1` uses the runtime default parallelism.
- Positive values cap the maximum concurrent dispose workers.

After disposal, every shard and the anonymous queue are cleared and `_count` is reset
to zero.

## Latency Diagnostics

When `IsEnableLatency` is `true` and the logger has `Information` enabled, the hub
records elapsed time for:

- `RegisterConnection`
- `UnregisterConnection`
- `BroadcastAsync`

The timing path uses `TimingScope.Start()` and logs `[PERF.NW.*]` messages. Disabling
`IsEnableLatency` removes this measurement work even when information logging is
enabled.

## Reporting

`ConnectionHub` implements report-style diagnostics through:

- `GenerateReport()` for a human-readable status summary;
- `WriteReportData(Utf8JsonWriter)` for structured values such as total connections, evictions,
  rejections, shard count, anonymous queue depth, capacity policy, byte totals,
  uptime statistics, algorithm summary, permission-level summary, and sampled
  connection rows.

## Tuning Guidance

- Choose a power-of-two `ShardCount` for the fastest shard-index path.
- Set `BroadcastBatchSize` when large broadcasts create too many simultaneous send tasks.
- Tune `ParallelDisconnectDegree` for shutdown behavior; high values close faster but can create I/O bursts.

## Related APIs

- [Connection Hub](../../network/connection/connection-hub.md)
- [Connection Limiter](../../network/connection/connection-limiter.md)
- [Connection Quota Options](./connection-quota-options.md)
- [Connection Guard Options](./connection-guard-options.md)
- [Trusted Proxy Options](./trusted-proxy-options.md)
- [Session Store Options](./session-store-options.md)
