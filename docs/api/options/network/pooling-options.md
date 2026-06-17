# Pooling Options

!!! note "Internal pool sizing"
    Neither `Nalix.Network` nor `Nalix.Runtime` exposes a public `PoolingOptions`
    class. Network-layer pool capacities (accept contexts, socket args, receive
    contexts, callback wrappers, timeout tasks, transports, connections) are derived
    internally from `ConnectionGuardOptions.MaxConnections` and
    `NetworkSocketOptions.MaxParallel` during server startup. To tune network pool
    sizes, adjust those options.

## Network Pool Sizing (Implementation Detail)

The `Connection` static constructor configures `ObjectPoolManager` capacities
for all network-layer pooled objects. The capacity for most pools equals
`ConnectionGuardOptions.MaxConnections` (default `2000`), and preallocation
counts are derived from `NetworkSocketOptions.MaxParallel`.

This is an internal detail — there is no separate `PoolingOptions` to configure
directly.

## Related APIs

- [Connection Guard Options](./connection-guard-options.md)
- [Network Socket Options](./network-socket-options.md)
- [Network Callback Options](./network-callback-options.md)
- [TCP Listener](../../network/tcp-listener.md)
