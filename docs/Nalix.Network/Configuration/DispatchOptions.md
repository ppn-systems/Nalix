# DispatchOptions — Configure per-connection dispatch queue behavior

`DispatchOptions` governs how `PacketDispatchChannel` manages per-connection queues, drop policies, and blocking behavior when dispatch workloads spike.

---

## Properties

| Property | Description | Default |
|----------|-------------|---------|
| `MaxPerConnectionQueue` | Maximum packets queued per connection before action is taken. Set to `0` (or negative) for unlimited queueing; any positive value bounds the per-connection backlog. | `0` (unbounded) |
| `DropPolicy` | Controls the behavior when a connection queue is full: `DROP_NEWEST`, `DROP_OLDEST`, or `BLOCK`. | `DROP_NEWEST` |
| `BlockTimeout` | When `DropPolicy` is `BLOCK`, this `TimeSpan` defines how long pushers wait for room before timing out. | `00:00:01` |

> Match `DropPolicy` to your application’s tolerance for latency vs. fairness. `DROP_NEWEST` is safe for most workloads; `DROP_OLDEST` keeps “stale” requests alive longer; `BLOCK` is useful when you can afford backpressure but never want to drop work.

---

## Usage guidance

- Register the options with your dispatcher setup (`PacketDispatchChannel` constructor options) or `ConfigurationManager`.
- For high-throughput servers, keep `MaxPerConnectionQueue` relatively small (e.g., < 128) to avoid tail latencies.
- Switch to `BLOCK` with a short `BlockTimeout` when you prefer backpressure instead of silently discarding packets.
- Combine with middleware/outbound throttling so a blocked queue doesn't stall the entire worker loop for too long.

---

## See also

- [PacketDispatchChannel](../Routing/PacketDispatchChannel.md)
- [PacketDispatchOptions](../Routing/PacketDispatchChannel.md)
- [`DropPolicy` enum](../../src/Nalix.Common/Networking/NetworkingEnums.cs)
