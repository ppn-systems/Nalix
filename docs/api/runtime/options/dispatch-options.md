# DispatchOptions

`DispatchOptions` controls per-connection queue behavior inside the dispatch layer.

## Source mapping

- `src/Nalix.Runtime/Options/DispatchOptions.cs`

## Properties

| Property | Meaning | Default |
|---|---|---:|
| `MaxPerConnectionQueue` | Max queued items for one connection. `0` means unlimited (not recommended). | `4096` |
| `DropPolicy` | What to do when the queue is full. | `DropNewest` |
| `BlockTimeout` | Wait budget when `DropPolicy` is `Block`. | `1000 ms` |
| `BucketCountMultiplier` | Multiplier for internal bucket count based on CPU count. | `64` |
| `MinBucketCount` | Minimum internal bucket count. | `256` |
| `MaxBucketCount` | Maximum internal bucket count. | `16384` |

## How to think about it

This is not the global dispatcher size. It is the bound applied to one connection's backlog.

Use it to stop one noisy client from creating unbounded memory growth or unfair tail latency.

## Recommended starting point

- interactive workloads: small bounded queue
- test/dev: unlimited or relaxed queue
- high-abuse environments: bounded queue + `DropNewest`

## Example

```csharp
var options = new DispatchOptions
{
    MaxPerConnectionQueue = 128,
    DropPolicy = DropPolicy.DropNewest,
    BlockTimeout = TimeSpan.FromMilliseconds(250)
};
```

## Related APIs

- [Packet Dispatch](../routing/packet-dispatch.md)
- [Connection Limiter](../../network/connection/connection-limiter.md)
