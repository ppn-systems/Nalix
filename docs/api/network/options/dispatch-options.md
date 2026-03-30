# DispatchOptions

`DispatchOptions` controls per-connection queue behavior inside the dispatch layer.

## Source mapping

- `src/Nalix.Network/Configurations/DispatchOptions.cs`

## Properties

| Property | Meaning | Default |
|---|---|---:|
| `MaxPerConnectionQueue` | Max queued items for one connection. `0` means unlimited. | `0` |
| `DropPolicy` | What to do when the queue is full. | `DROP_NEWEST` |
| `BlockTimeout` | Wait budget when `DropPolicy` is `BLOCK`. | `1000 ms` |

## How to think about it

This is not the global dispatcher size. It is the bound applied to one connection's backlog.

Use it to stop one noisy client from creating unbounded memory growth or unfair tail latency.

## Recommended starting point

- interactive workloads: small bounded queue
- test/dev: unlimited or relaxed queue
- high-abuse environments: bounded queue + `DROP_NEWEST`

## Example

```csharp
var options = new DispatchOptions
{
    MaxPerConnectionQueue = 128,
    DropPolicy = DispatchDropPolicy.DROP_NEWEST,
    BlockTimeout = TimeSpan.FromMilliseconds(250)
};
```

## Related APIs

- [Packet Dispatch](../../routing/packet-dispatch.md)
- [Connection Limiter](../../middleware/connection-limiter.md)
