# Permission Levels

`PermissionLevel` defines the coarse-grained authority levels used throughout the Nalix ecosystem for access control.

## Source mapping

- `src/Nalix.Common/Security/PermissionLevel.cs`

## Enum Definition

| Value | Name | Description |
| --- | --- | --- |
| `0` | `NONE` | No authority assigned. |
| `25` | `GUEST` | Minimal access for anonymous or guest users. |
| `50` | `READ_ONLY` | Read-only access. |
| `100` | `USER` | Standard authenticated user. |
| `175` | `SUPERVISOR` | Elevated privileges within a limited scope. |
| `200` | `TENANT_ADMINISTRATOR` | Administrative control over a single tenant or organization. |
| `225` | `SYSTEM_ADMINISTRATOR` | System-wide administrative authority. |
| `255` | `OWNER` | Highest authority level with unrestricted control. |

## Usage in Attributes

The `[PacketPermission]` attribute uses these levels to enforce security early in the pipeline.

```csharp
[PacketOpcode(0x1001)]
[PacketPermission(PermissionLevel.SYSTEM_ADMINISTRATOR)]
public void ResetServer(PacketContext<Control> context)
{
    // Only users with Level >= SYSTEM_ADMINISTRATOR can reach here
}
```

## Related APIs

- [Packet Attributes](../runtime/routing/packet-attributes.md)
- [Permission Middleware](../runtime/middleware/permission-middleware.md)
- [Connection Contracts](../common/connection-contracts.md)
