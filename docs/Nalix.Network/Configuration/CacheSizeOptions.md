# CacheSizeOptions — Network buffer cache sizing for Nalix.Network

`CacheSizeOptions` controls how many frames the network layer keeps in memory while awaiting processing. It exists to avoid unbounded buffering when a client floods faster than your dispatch pipeline can consume.

---

## Key property

| Property  | Description | Default | Range |
|-----------|-------------|---------|-------|
| `Incoming` | Maximum number of incoming frames that may be buffered per connection before the receive loop starts dropping new arrivals. | `100` | `10` to `1_000_000` |

> Larger values help absorb occasional bursts but consume more frame/cache objects. Reduce the limit when you expect steady/low-latency traffic so the gate closes quickly on abusive peers.

---

## Usage

- Register the options via configuration (`ConfigurationManager.Instance.Get<CacheSizeOptions>()`) or pass them explicitly when constructing related components.
- Call `CacheSizeOptions.Validate()` during startup to ensure the configured value stays within the supported range.
- Tune `Incoming` upward if your handler/dispatch loop experiences transient backpressure (e.g., slow downstream I/O). Tune it downward if you want to tighten flood protection.

---

## Best practices

- Keep the cache small (< 500) for real-time systems to avoid queuing stale data.
- Combine with `DispatchOptions.MaxPerConnectionQueue` to keep both receive buffers and dispatch queues bounded for each connection.
- Monitor production telemetry (buffer drops, queue length) before increasing every limit globally.

---

## See also

- [DispatchOptions](./DispatchOptions.md)
- [NetworkCallbackOptions](./NetworkCallbackOptions.md)
- [PoolingOptions](./PoolingOptions.md)
