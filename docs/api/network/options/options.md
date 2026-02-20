# Network Options

This page summarizes the main `Nalix.Network.Options` types that shape listener behavior, dispatch pressure, throttling, compression, idle cleanup, and object pooling.

## Source mapping

- `src/Nalix.Network/Options/NetworkSocketOptions.cs`
- `src/Nalix.Network/Options/PoolingOptions.cs`
- `src/Nalix.Network/Options/DispatchOptions.cs`
- `src/Nalix.Network/Options/ConnectionLimitOptions.cs`
- `src/Nalix.Network/Options/ConnectionHubOptions.cs`
- `src/Nalix.Network/Options/TimingWheelOptions.cs`
- `src/Nalix.Network/Options/NetworkCallbackOptions.cs`
- `src/Nalix.Network/Options/CompressionOptions.cs`
- `src/Nalix.Network/Options/TokenBucketOptions.cs`

## Core option types

| Type | Purpose | Examples |
|---|---|---|
| `NetworkSocketOptions` | Listen socket and accept worker tuning. | `Port`, `Backlog`, `MaxParallel`, `KeepAlive`, `ReuseAddress`, `EnableTimeout`, `EnableIPv6` |
| `PoolingOptions` | Pool capacities and preallocation for listener/dispatch objects. | accept context pool, socket args pool, packet context pool |
| `DispatchOptions` | Per-connection queue behavior inside dispatch. | `MaxPerConnectionQueue`, `DropPolicy`, `BlockTimeout` |
| `ConnectionLimitOptions` | Per-endpoint connection caps and burst bans. | concurrent cap, rate window, ban duration, cleanup |
| `ConnectionHubOptions` | Hub sharding, username rules, broadcast behavior. | `ShardCount`, `MaxConnections`, `DropPolicy`, `BroadcastBatchSize` |
| `TimingWheelOptions` | Idle connection timeout wheel. | bucket count, tick duration, idle timeout |
| `NetworkCallbackOptions` | Callback flood protection and pending callback caps. | per-connection and per-IP pending limits |
| `CompressionOptions` | Frame compression trigger rules. | enable flag, minimum size |
| `TokenBucketOptions` | Token bucket limiter behavior. | burst capacity, refill, cleanup, sharding |

## How they are used

- `TcpListenerBase` depends on `NetworkSocketOptions`, `PoolingOptions`, `TimingWheelOptions`, and `ConnectionLimitOptions`.
- `UdpListenerBase` uses `NetworkSocketOptions`.
- `ConnectionHub` reads `ConnectionHubOptions`.
- `PacketDispatchChannel` and routing infrastructure depend on `DispatchOptions`, `NetworkCallbackOptions`, `CompressionOptions`, and pooling-related settings.
- throttling components consume `TokenBucketOptions` and related limits.

## Basic usage

```csharp
NetworkSocketOptions socket = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
socket.Validate();

PoolingOptions pooling = ConfigurationManager.Instance.Get<PoolingOptions>();
pooling.Validate();

ConnectionLimitOptions limits = ConfigurationManager.Instance.Get<ConnectionLimitOptions>();
limits.Validate();
```

## Notes

- Validate options during startup, before activating listeners or dispatchers.
- `MaxConnections = -1` typically means unlimited where that pattern is used.
- Timeout-related options only take effect when the owning runtime path enables them, for example `NetworkSocketOptions.EnableTimeout`.

## Related APIs

- [Network Socket Options](./network-socket-options.md)
- [Dispatch Options](./dispatch-options.md)
- [Connection Limit Options](./connection-limit-options.md)
- [Connection Hub Options](../connection/connection-hub-options.md)
- [Timing Wheel Options](./timing-wheel-options.md)
- [Pooling Options](./pooling-options.md)
- [Network Callback Options](./network-callback-options.md)
- [Compression Options](./compression-options.md)
- [Token Bucket Options](./token-bucket-options.md)
- [Tcp Listener](../runtime/tcp-listener.md)
- [Connection Hub](../connection/connection-hub.md)
- [Connection Limiter](../../middleware/connection-limiter.md)
