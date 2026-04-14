# Network Options

Network options define listener behavior, connection admission policy, queueing pressure, timeout cleanup, callbacks, and related runtime controls.

## Audit Summary

- Existing page had useful coverage but mixed types from multiple packages without clear boundary notes.
- Needed clearer package ownership mapping.

## Missing Content Identified

- Package ownership for each options type (`Nalix.Network`, `Nalix.Runtime`, `Nalix.Framework`, `Nalix.Network.Pipeline`).
- Explicit startup validation recommendation.

## Improvement Rationale

Clear ownership reduces configuration mistakes in modular deployments.

## Source Mapping

- `src/Nalix.Network/Options/NetworkSocketOptions.cs`
- `src/Nalix.Network/Options/PoolingOptions.cs`
- `src/Nalix.Network/Options/ConnectionLimitOptions.cs`
- `src/Nalix.Network/Options/ConnectionHubOptions.cs`
- `src/Nalix.Network/Options/TimingWheelOptions.cs`
- `src/Nalix.Network/Options/NetworkCallbackOptions.cs`
- `src/Nalix.Network/Options/SessionStoreOptions.cs`
- `src/Nalix.Runtime/Options/DispatchOptions.cs`
- `src/Nalix.Framework/Options/CompressionOptions.cs`
- `src/Nalix.Network.Pipeline/Options/TokenBucketOptions.cs`

## Option Ownership Matrix

| Option type | Package | Primary scope |
|---|---|---|
| `NetworkSocketOptions` | `Nalix.Network` | socket/listener behavior |
| `PoolingOptions` (network) | `Nalix.Network` | network object pooling |
| `ConnectionLimitOptions` | `Nalix.Network` | admission/rate controls |
| `ConnectionHubOptions` | `Nalix.Network` | hub capacity/sharding |
| `TimingWheelOptions` | `Nalix.Network` | idle timeout wheel |
| `NetworkCallbackOptions` | `Nalix.Network` | callback pressure limits |
| `SessionStoreOptions` | `Nalix.Network` | resumable session TTL |
| `DispatchOptions` | `Nalix.Runtime` | dispatch queue behavior |
| `CompressionOptions` | `Nalix.Framework` | compression thresholds |
| `TokenBucketOptions` | `Nalix.Network.Pipeline` | reusable token-bucket limiter |

## Best Practices

- Validate all option objects during startup before `Activate()`.
- Keep network/runtime/pipeline options grouped by owning package in configuration files.
- Tune queue limits, drop policy, and connection limits together.

## Related APIs

- [TCP Listener](../tcp-listener.md)
- [UDP Listener](../udp-listener.md)
- [Connection Hub](../connection/connection-hub.md)
- [Dispatch Options](../../runtime/options/dispatch-options.md)
