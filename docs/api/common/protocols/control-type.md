# Control Type

`ControlType` identifies the kind of system-level message used by the Nalix protocol layer for signaling and state management.

## Source mapping

- `src/Nalix.Common/Networking/Protocols/ControlType.cs`

## Enum Definition

| Hex | Name | Description |
|---|---|---|
| `0x00` | `NONE` | No control message specified. |
| `0x01` | `PING` | Check connection liveness. |
| `0x02` | `PONG` | Response sent to a ping. |
| `0x03` | `ACK` | Confirmation of receipt. |
| `0x04` | `DISCONNECT` | Graceful disconnect notification. |
| `0x05` | `ERROR` | Description of a protocol-level failure. |
| `0x07` | `HEARTBEAT` | Regular pulse to keep a connection active. |
| `0x08` | `NACK` | Indication that processing failed. |
| `0x09` | `RESUME` | Resume an interrupted session. |
| `0x0A` | `SHUTDOWN` | Request server-side graceful shutdown. |
| `0x0B` | `REDIRECT` | Instruct client to reconnect elsewhere. |
| `0x0C` | `THROTTLE` | Request client to reduce transmission rate. |
| `0x0D` | `NOTICE` | Informational maintenance or system notice. |
| `0x10` | `TIMEOUT` | Operation timed out server-side. |
| `0x11` | `FAIL` | Generic operation failure. |
| `0x12` | `TIMESYNCREQUEST` | Client requesting server's high-resolution time. |
| `0x13` | `TIMESYNCRESPONSE` | Server responding with high-resolution time. |
| `0x14` | `CIPHER_UPDATE` | Request to change the active cipher suite algorithm. |
| `0x15` | `CIPHER_UPDATE_ACK` | Acknowledges a cipher suite update request. |
| `0xFE` | `RESERVED1` | Reserved for future extension. |
| `0xFF` | `RESERVED2` | Reserved for future extension. |

## Usage

Control frames are typically created and consumed automatically by the transport and SDK layers, but can also be triggered manually for custom signaling.

```csharp
var control = new Control();
control.Initialize(ControlType.PING, sequenceId: 42, flags: PacketFlags.SYSTEM | PacketFlags.RELIABLE);
```

## Related APIs

- [Built-in Frames](../../framework/packets/built-in-frames.md)
- [Protocol String Extensions](../../sdk/protocol-string-extensions.md)
- [Session Extensions](../../sdk/tcp-session-extensions.md)
