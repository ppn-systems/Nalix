# Nalix.Common

Shared contracts, packet metadata, and middleware primitives used across SDK and server code.

## Where it fits

```mermaid
flowchart LR
    A["Nalix.Common"] --> B["Contracts"]
    A --> C["Packet attributes"]
    A --> D["Middleware contracts"]
    B --> E["Nalix.Network"]
    B --> F["Nalix.SDK"]
```

### Core contracts
These contracts keep SDK and server code aligned.

**Key Components**
- `IPacket`
- `IConnection`
- `PacketControllerAttribute`
- `PacketOpcodeAttribute`

### Quick example

```csharp
[PacketController("SamplePingHandlers")]
public class SamplePingHandlers
{
    [PacketOpcode(1)]
    public IPacket HandlePing(IPacketContext<IPacket> request)
        => request.Packet;
}
```

Legacy handlers that take `(TPacket, IConnection[, CancellationToken])` are still supported, but `PacketContext<TPacket>` is the preferred shape when you need context, sender, or metadata access.

### Metadata and attributes
Metadata is built once during handler registration and later exposed through `PacketContext`.

**Key Components**
- `PacketMetadata`
- `IPacketContext<TPacket>`

```csharp
// Metadata attributes are applied to handlers or packets
[PacketOpcode(1)]
[SampleTenantMetadata("Tenant-A")]
public IPacket HandlePing(IPacketContext<IPacket> request) => request.Packet;
```

### Middleware primitives
Middleware runs over packet contexts and can short-circuit outbound flows.

**Key Components**
- `IPacketMiddleware<TPacket>`
- `IPacketContext<TPacket>`
- `IPacketSender<TPacket>`

### Quick example

```csharp
public sealed class SamplePacketMiddleware : IPacketMiddleware<IPacket>
{
    public async Task InvokeAsync(
        IPacketContext<IPacket> context,
        Func<CancellationToken, Task> next)
    {
        await next(context.CancellationToken);
    }
}
```

### Shared enums
Enums keep policies consistent across the stack.

**Key Components**
- `CipherSuiteType`
- `DropPolicy`

## Key API pages

- [Packet Contracts](../api/common/packet-contracts.md)
- [Connection Contracts](../api/common/connection-contracts.md)
- [Packet Attributes](../api/runtime/routing/packet-attributes.md)
- [Packet Metadata](../api/runtime/routing/packet-metadata.md)
- [Concurrency Contracts](../api/common/concurrency-contracts.md)
