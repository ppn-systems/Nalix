# Network Configuration — Nalix.Network tuning & protection settings

This folder explains the main `Nalix.Network.Configurations` classes you can override or configure via `ConfigurationManager`. Each option class governs a specific concern in the network layer (dispatch queues, callback throttles, cache sizing, compression, idle detection, etc.).

---

## Option suites

| Config class | Purpose | Key knobs |
|--------------|---------|-----------|
| `DispatchOptions` | Controls per-connection dispatch queues, drop policy, and blocking timeout inside `PacketDispatchChannel`. | `MaxPerConnectionQueue`, `DropPolicy`, `BlockTimeout` |
| `NetworkCallbackOptions` | Tunes the `AsyncCallback` dispatcher: per-connection receive queues plus global/per-IP callback caps for DDoS protection. | `MaxPerConnectionPendingPackets`, `MaxPendingNormalCallbacks`, `MaxPendingPerIp` |
| `CacheSizeOptions` | Sets how many incoming frames the receive loop caches before dropping them. | `Incoming` buffer depth |
| `CompressionOptions` | Enables/limits when network data is compressed before dispatch. | `Enabled`, `MinSizeToCompress` |
| `TimingWheelOptions` | Configures the idle-connection detection/auto-disconnect timing wheel. | `BucketCount`, `TickDuration`, `IdleTimeoutMs` |

---

## How to use

1. Configure via `ConfigurationManager.Instance.Get<YourOptions>()` or apply them through dependency injection when constructing listeners, dispatchers, or other services.
2. Call `options.Validate()` during startup to ensure values respect the documented ranges.
3. Adjust values conservatively—each option has a default that balances performance/resilience; tune only after observing telemetry.

---

## See also

- [ConnectionHubOptions](../Connections/ConnectionHub.md) (related connection tracking limits)
- [PacketDispatchChannel](../Routing/PacketDispatchChannel.md) (dispatch queue behavior powered by these options)
- [ConnectionLimiter](../Throttling/ConnectionLimiter.md) (per-IP connection enforcement for DDoS resilience)
- [ConfigurationBuilder](../../Nalix.Framework/Configuration.md) (how `ConfigurationManager` loads these classes)
