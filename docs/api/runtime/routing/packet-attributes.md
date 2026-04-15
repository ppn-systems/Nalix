# Packet Attributes

Nalix uses packet attributes to declare handler routing and execution policy.

## Audit Summary

- Existing page used one incorrect attribute name (`PacketConcurrency`) and included behavior wording stronger than what attributes alone guarantee.
- Needed direct mapping to actual attribute types and their constructor parameters.

## Missing Content Identified

- Accurate attribute list with exact names from `Nalix.Common.Networking.Packets`.
- Clear distinction between declaration (attribute) and enforcement (runtime/middleware).

## Improvement Rationale

Precise attribute docs reduce handler-registration errors and avoid policy misunderstandings.

## Source Mapping

- `src/Nalix.Common/Networking/Packets/PacketControllerAttribute.cs`
- `src/Nalix.Common/Networking/Packets/PacketOpcodeAttribute.cs`
- `src/Nalix.Common/Networking/Packets/PacketPermissionAttribute.cs`
- `src/Nalix.Common/Networking/Packets/PacketRateLimitAttribute.cs`
- `src/Nalix.Common/Networking/Packets/PacketConcurrencyLimitAttribute.cs`
- `src/Nalix.Common/Networking/Packets/PacketEncryptionAttribute.cs`
- `src/Nalix.Common/Networking/Packets/PacketTimeoutAttribute.cs`

## Attribute Reference

| Attribute | Scope | Purpose |
|---|---|---|
| `PacketControllerAttribute` | Class | Marks a controller class for packet handlers. |
| `PacketOpcodeAttribute` | Method | Binds handler to opcode. |
| `PacketPermissionAttribute` | Method | Declares minimum `PermissionLevel`. |
| `PacketRateLimitAttribute` | Method | Declares requests-per-second and burst values. |
| `PacketConcurrencyLimitAttribute` | Method | Declares concurrency/queue limits. |
| `PacketEncryptionAttribute` | Method | Declares encryption requirement and algorithm hint. |
| `PacketTimeoutAttribute` | Method | Declares handler timeout budget (ms). |

## Why Attributes Exist

Attributes provide declarative metadata at registration time so dispatch/runtime layers can apply policy without embedding those policies directly inside business handlers.

## Practical Example

```csharp
using System.Threading.Tasks;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;

[PacketController("SecureChat", version: "1.2")]
public sealed class SecureChatController
{
    [PacketOpcode(0x3001)]
    [PacketPermission(PermissionLevel.USER)]
    [PacketRateLimit(10, burst: 2)]
    [PacketConcurrencyLimit(100, queue: true, queueMax: 1000)]
    [PacketEncryption(true)]
    [PacketTimeout(5000)]
    public static ValueTask HandleAsync(IPacketContext<ChatPacket> context)
    {
        // Application logic here
        return ValueTask.CompletedTask;
    }
}
```

For a comprehensive walkthrough of handler implementation, including error handling and registration, see the [Implementing Packet Handlers](../../../guides/implementing-packet-handlers.md) guide.

## Best Practices

- Treat attributes as policy declarations; verify enforcement in runtime middleware configuration.
- Keep opcode values centrally managed to avoid collisions.
- Use explicit permission/rate/concurrency attributes on public-facing handlers.

## Related APIs

- [Packet Metadata](./packet-metadata.md)
- [Packet Context](./packet-context.md)
- [Handler Return Types](./handler-results.md)
