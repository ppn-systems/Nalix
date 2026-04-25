# Ping, Disconnect, and Time Sync Extensions

`Nalix.SDK.Transport.Extensions` includes small convenience APIs for the most common session-level control flows. These helpers are thin wrappers over `Control` packets and `RequestAsync<TResponse>` so callers do not need to manually manage sequence ids, predicates, and control-frame construction.

This is a high-level convenience page for client application code. These helpers are not server APIs. For manual `Control` construction and matching, see [Session Extensions](./tcp-session-extensions.md); for frame I/O internals, see [Frame Reader and Sender](./frame-reader-and-sender.md).

## Source mapping

- `src/Nalix.SDK/Transport/Extensions/PingExtensions.cs`
- `src/Nalix.SDK/Transport/Extensions/DisconnectExtensions.cs`
- `src/Nalix.SDK/Transport/Extensions/TimeSyncExtensions.cs`
- `src/Nalix.SDK/Transport/Extensions/ControlExtensions.cs`
- `src/Nalix.SDK/Transport/Extensions/RequestExtensions.cs`

## Capability summary

| Extension | Method | Control type | Main behavior |
| --- | --- | --- | --- |
| `PingExtensions` | `PingAsync` | `PING` -> `PONG` | Measures round-trip time using monotonic ticks. |
| `DisconnectExtensions` | `DisconnectGracefullyAsync` | `DISCONNECT` | Best-effort graceful shutdown notification, then optional local close. |
| `TimeSyncExtensions` | `SyncTimeAsync` | `TIMESYNCREQUEST` -> `TIMESYNCRESPONSE` | Measures RTT and optionally adjusts the local framework clock. |

## PingAsync

```csharp
public static ValueTask<double> PingAsync(
    this TcpSession session,
    int timeoutMs = 5000,
    CancellationToken ct = default)
```

`PingAsync` sends a `SYSTEM_CONTROL` `PING` frame and waits for a matching `PONG` with the same sequence id.

Important details:

- The sequence id is generated from a process-local atomic counter and truncated to `ushort`.
- The outgoing control packet is built through `NewControl(...)`, which stamps timestamp and monotonic tick fields.
- The response wait is implemented with `RequestAsync<Control>` and a predicate matching:
  - `ControlType.PONG`
  - the generated sequence id
- The returned value is RTT in milliseconds based on `Clock.MonoTicksNow()`.
- The request packet is disposed in a `finally` block after the operation completes.

```csharp
double rttMs = await session.PingAsync(timeoutMs: 2500, ct);
Console.WriteLine($"server RTT: {rttMs:n2} ms");
```

## DisconnectGracefullyAsync

```csharp
public static ValueTask DisconnectGracefullyAsync(
    this TcpSession session,
    ProtocolReason reason = ProtocolReason.NONE,
    bool closeLocalConnection = true,
    CancellationToken ct = default)
```

`DisconnectGracefullyAsync` is a best-effort control-frame notification before a local close.

Runtime behavior:

1. If `session.IsConnected` is true, it sends a `SYSTEM_CONTROL` `DISCONNECT` frame.
2. The outgoing control packet stores the supplied `ProtocolReason`.
3. Send failures caused by cancellation, disposal, invalid session state, or network shutdown are intentionally swallowed.
4. If `closeLocalConnection` is true, `session.DisconnectAsync()` runs after the send attempt.

This method is intentionally tolerant because disconnect paths often run while sockets are already closing.

```csharp
await session.DisconnectGracefullyAsync(
    reason: ProtocolReason.CLIENT_CLOSING,
    closeLocalConnection: true,
    ct: shutdownToken);
```

## SyncTimeAsync

```csharp
public static ValueTask<(double RttMs, double AdjustedMs)> SyncTimeAsync(
    this TcpSession session,
    int timeoutMs = 5000,
    CancellationToken ct = default)
```

`SyncTimeAsync` sends a `TIMESYNCREQUEST` control frame and waits for a matching `TIMESYNCRESPONSE`.

The method returns:

| Value | Meaning |
| --- | --- |
| `RttMs` | Measured round-trip time in milliseconds. |
| `AdjustedMs` | Clock adjustment applied by `Clock.SynchronizeUnixMilliseconds`; `0` when session time sync is disabled. |

Important details:

- Request/response correlation uses the generated sequence id.
- RTT is computed with the same monotonic clock as `PingAsync`.
- Local clock synchronization only happens when `session.Options.TimeSyncEnabled` is true.
- The server timestamp is adjusted using half the measured RTT.

```csharp
var (rttMs, adjustedMs) = await session.SyncTimeAsync(timeoutMs: 5000, ct);

if (adjustedMs != 0)
{
    Console.WriteLine($"clock adjusted by {adjustedMs:n2} ms, RTT {rttMs:n2} ms");
}
```

## Error and timeout model

These helpers use the same error model as the underlying session APIs:

- `ArgumentNullException` is thrown for a null session.
- `NetworkException` can be thrown when the session is not connected or the transport fails.
- `TimeoutException` can be thrown by request/response helpers when no matching response arrives before `timeoutMs`.
- `OperationCanceledException` can be observed when the supplied cancellation token is canceled, except in the best-effort graceful-disconnect send path where cancellation is swallowed.

## Related APIs

- [Session Extensions](./tcp-session-extensions.md)
- [Request Options](./options/request-options.md)
- [Control Type Enum](../common/protocols/control-type.md)
- [TCP Session](./tcp-session.md)
