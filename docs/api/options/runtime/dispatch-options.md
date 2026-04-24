# DispatchOptions

`DispatchOptions` controls per-connection queue behavior inside the dispatch layer.

## Source mapping

- `src/Nalix.Runtime/Options/DispatchOptions.cs`

## Properties

| Property | Meaning | Default | Validation |
| --- | --- | ---: | --- |
| `MaxPerConnectionQueue` | Max queued items for one connection. `0` means unlimited and is not recommended for production. | `4096` | `0..1,048,576` |
| `DropPolicy` | What to do when the queue is full. | `DropNewest` | Must be a valid `DropPolicy` enum value. |
| `BlockTimeout` | Wait budget when `DropPolicy` is `Block`. | `1000 ms` | `TimeSpan` value; keep bounded in production. |
| `PriorityWeights` | Weighted round-robin weights for `[NONE, LOW, MEDIUM, HIGH, URGENT]`. | `1,2,4,8,16` | Comma-separated weight list consumed by the dispatch channel. |
| `BucketCountMultiplier` | Multiplier for internal bucket count based on CPU count. | `64` | `1..1024` |
| `MinBucketCount` | Minimum internal bucket count. | `256` | `1..65,536` and must be `<= MaxBucketCount`. |
| `MaxBucketCount` | Maximum internal bucket count. | `16384` | `1..1,048,576` and must be `>= MinBucketCount`. |

## Validation contract

`Validate()` runs data-annotation validation for the bounded numeric and enum fields, then enforces the cross-field rule that `MinBucketCount` cannot be greater than `MaxBucketCount`.

The default `MaxPerConnectionQueue` is intentionally bounded at `4096`. Setting it to `0` disables bounding and can expose the process to memory exhaustion if a client floods packets faster than handlers can process them.

## How to think about it

This is not the global dispatcher size. It is the bound applied to one connection's backlog.

Use it to stop one noisy client from creating unbounded memory growth or unfair tail latency.

`PriorityWeights` is the fairness knob. The default `1,2,4,8,16` makes urgent traffic much more likely to be served than `NONE` traffic without starving lower-priority queues.

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
    BlockTimeout = TimeSpan.FromMilliseconds(250),
    PriorityWeights = "1,2,4,8,16"
};

options.Validate();
```

## Related APIs

- [Packet Dispatch](../../runtime/routing/packet-dispatch.md)
- [Connection Limiter](../../network/connection/connection-limiter.md)
