# TimingWheelOptions — Idle connection housekeeping for Nalix.Network

`TimingWheelOptions` configures the timing wheel used by the network stack to detect idle connections and trigger cleanup/auto-disconnect behavior.

---

## Key properties

| Property | Description | Default |
|----------|-------------|---------|
| `BucketCount` | Number of buckets in the timing wheel. More buckets reduce collision risk at the cost of slightly more memory. | `512` |
| `TickDuration` | Milliseconds between ticks; lower values increase resolution (and CPU) while higher values make idle detection coarser. | `1000` |
| `IdleTimeoutMs` | Idle duration (milliseconds) after which a connection is considered stale and will be disconnected. | `60000` |

> The wheel progresses once per `TickDuration`. Each bucket represents one tick, so a connection stays alive for roughly `BucketCount × TickDuration` unless it is refreshed sooner.

---

## Usage

- Load via configuration or pass explicitly to listeners/protocols that accept an idle timeout configuration.
- `IdleTimeoutMs` should be balanced with your application’s heartbeat/keep-alive strategy (e.g., 60s for typical TCP keep-alives).
- A smaller `TickDuration` (250–500ms) gives quicker disconnection detection but increases the tick processing CPU cost.
- Adjust `BucketCount` only when the default `512` leads to collisions (rare); large values are safe but use more memory.

---

## See also

- [DispatchOptions](./DispatchOptions.md)
- [NetworkCallbackOptions](./NetworkCallbackOptions.md)
- [ConnectionLimiter](../Throttling/ConnectionLimiter.md)
