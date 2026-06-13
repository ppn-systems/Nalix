# Pooling Options

!!! note "Two PoolingOptions in Nalix"
    `Nalix.Network` no longer has a `PoolingOptions` class. Network-layer pool
    capacities (accept contexts, socket args, receive contexts, callback wrappers,
    timeout tasks, transports, connections) are derived internally from
    `ConnectionGuardOptions.MaxConnections` and `NetworkSocketOptions.MaxParallel`
    during server startup. To tune network pool sizes, adjust those options.
    `Nalix.Runtime.Options.PoolingOptions` still controls the **packet dispatch**
    pool (`PacketContext<T>`).

## Network Pool Sizing (Implementation Detail)

The `Connection` static constructor configures `ObjectPoolManager` capacities
for all network-layer pooled objects. The capacity for most pools equals
`ConnectionGuardOptions.MaxConnections` (default `2000`), and preallocation
counts are derived from `NetworkSocketOptions.MaxParallel`.

This is an internal detail — there is no separate Network `PoolingOptions` to
configure directly.

### Runtime PoolingOptions

`Nalix.Runtime.Options.PoolingOptions` remains available and controls the
packet dispatch pool:

| Property | Default | Validation | Runtime consumer |
| --- | ---: | --- | --- |
| `PacketContextCapacity` | `8192` | `1..1_000_000` | Maximum pooled `PacketContext<T>` instances. |
| `PacketContextPreallocate` | `64` | `0..1_000_000` | `PacketContext<T>` instances created at startup. |

`Validate()` runs DataAnnotation validation and enforces that
`PacketContextPreallocate` does not exceed `PacketContextCapacity`.

## Related APIs

- [Connection Guard Options](./connection-guard-options.md)
- [Network Socket Options](./network-socket-options.md)
- [Network Callback Options](./network-callback-options.md)
- [TCP Listener](../../network/tcp-listener.md)
