# NetworkCallbackOptions — Async callback throttling & DDoS protection

`NetworkCallbackOptions` tunes the two throttling layers inside `AsyncCallback` / `FramedSocketConnection` to protect against floods, keep receive loops responsive, and prevent a single IP from monopolizing the global callback queue.

---

## Layer 1 — Per-connection receive throttling

| Property | Description | Default |
|----------|-------------|---------|
| `MaxPerConnectionPendingPackets` | Maximum number of packets buffered per connection before the receive loop drops new ones. Valuable for guarding against fast senders that outrun your handler. | `8` |

> Packets arriving when this cap is reached are dropped immediately and logged with a warning. The limit applies before the dispatcher receives the packet, so it keeps bad clients from overloading thread-pool work.

---

## Layer 2 — Global/per-IP callback caps

| Property | Description | Default |
|----------|-------------|---------|
| `MaxPendingNormalCallbacks` | Global cap for pending *normal*-priority callbacks (exception: close/disconnect callbacks bypass this). | `10_000` |
| `CallbackWarningThreshold` | When pending callbacks cross this value (multiple times), throttle warnings are emitted. Set `0` to disable repeated warnings. | `5_000` |
| `MaxPendingPerIp` | Maximum pending callbacks allowed for a single remote IP; new callbacks from that IP are dropped until the count falls below this cap. | `64` |
| `MaxPooledCallbackStates` | Ceiling for the reusable `StateWrapper` object pool inside `AsyncCallback`. Larger values reduce allocations at the cost of more pooled objects. | `1_000` |

> Lower the per-IP and global caps to defend poorly behaved clients or DDoS traffic. Keep `MaxPooledCallbackStates` high enough to handle your highest expected concurrency without GC spikes.

---

## Usage & validation

- Load via configuration (`ConfigurationManager.Instance.Get<NetworkCallbackOptions>()`) or pass to constructors expecting callback throttling data.
- Call `Validate()` to enforce cross-field rules (warning threshold < global cap; per-IP cap ≤ global cap).
- These options are shared between `AsyncCallback`, `FramedSocketConnection`, and other callback-driven components in `Nalix.Network`.

---

## See also

- [CompressionOptions](./CompressionOptions.md)
- [ConnectionLimiter](../Throttling/ConnectionLimiter.md)
- [PacketDispatchChannel](../Routing/PacketDispatchChannel.md)
