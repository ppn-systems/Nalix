# Packet Options

`PacketOptions` configures packet-level behavior, primarily focusing on memory management and pooling strategies.

## Source Mapping

- `src/Nalix.Framework/Options/PacketOptions.cs`

## Properties

| Property | Type | Default | Purpose |
|---|---|---:|---|
| `EnablePooling` | `bool` | `true` | Enables object pooling for all packets using `PacketBase`. |

## Why Pooling Matters

Packet pooling is a core pillar of the Nalix **Zero-Allocation Hot Path**. When enabled:
- Packets are rented from the `ObjectPoolManager` during deserialization.
- Packets must be explicitly disposed (via `using` or manual `.Dispose()`) to return to the pool.
- Gen 0/1/2 GC churn is eliminated for messaging traffic.

## Environment-Aware Defaults

The Nalix `Bootstrap` system sets different defaults based on the assembly being used:

- **Server-Side (`Nalix.Network.Hosting`)**: `EnablePooling` defaults to **`true`**. High-performance servers require pooling to handle massive packet throughput without GC pauses.
- **Client-Side (`Nalix.SDK`)**: `EnablePooling` defaults to **`false`**. Client applications (especially in engines like Unity) often have complex lifecycles where forgetting to dispose a packet could lead to pool exhaustion or data corruption. Disabling pooling on the client provides a safer "new instance" experience.

!!! tip "Manual Override"
    You can manually override these defaults in your `server.ini` or `client.ini`:
    ```ini
    [Packet]
    EnablePooling = true
    ```

## Related APIs

- [Object Pooling](../memory/object-pooling.md)
- [Zero-Allocation Path](../../../concepts/internals/zero-allocation.md)
- [Configuration System](../../../concepts/runtime/configuration.md)
