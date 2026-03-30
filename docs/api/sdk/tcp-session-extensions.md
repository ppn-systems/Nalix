# Nalix.SDK.Transport.Extensions — TcpSession Helpers for Control, Directive, and Request Flows

The `Nalix.SDK.Transport.Extensions` namespace enriches `IClientConnection`/`TcpSession` with helpers for protocol control packets, directives, request/response coordination, throttling safety, and subscription management. The helpers keep receive loops resilient by owning leases and catching handler faults.

## Source mapping

- `src/Nalix.SDK/Transport/Extensions/ControlExtensions.cs`
- `src/Nalix.SDK/Transport/Extensions/DirectiveClientExtensions.cs`
- `src/Nalix.SDK/Transport/Extensions/RequestExtensions.cs`
- `src/Nalix.SDK/Transport/Extensions/TcpSessionSubscriptions.cs`

---

## Key capabilities

- Checklist:
  - Control helpers: `NewControl`, `PingAsync`, `AwaitControlAsync`, `SendControlAsync`.
  - Directives: `TryHandleDirectiveAsync`, throttle/redirect/NACK/NOTICE handling.
  - Requests: `RequestAsync` with `RequestOptions` (timeout, retry, encrypt).
  - Subscriptions: `On`, `OnOnce`, `SubscribeTemp`, `CompositeSubscription`.

- Fluent `Control` builders, PING/PONG helpers, and awaiters that use `PacketAwaiter` to avoid race conditions.
- Directive processing (`THROTTLE`, `REDIRECT`, `NACK`, `NOTICE`) with optional callbacks and default auto-redirect nursing.
- Request/response helpers (`RequestAsync`, `RequestOptions`) that send, await, optionally encrypt, and retry safely.
- Subscription helpers (`On<T>`, `OnOnce<T>`, `SubscribeTemp`, `Subscribe`) that automatically dispose leases and log handler errors.

---

## Control helpers (ControlExtensions)

- `NewControl(opCode, ControlType, ProtocolType)` starts a fluent builder that stamps `MonoTicks`/`Timestamp` and lets you chain `.WithSeq()`, `.WithReason()`, `.WithTransport()`, then `.Build()`.
- `AwaitPacketAsync<TPkt>` / `AwaitControlAsync` waits for a matching packet/control with timeout and cancellation.
- `PingAsync` sends a CONTROL PING and awaits the corresponding PONG, returns `(rttMs, Control pong)` and optionally syncs the client clock with the server timestamp.
- `SendControlAsync` materializes the builder, applies any extra configuration, and transmits the CONTROL frame.
- All helpers use `PACKET_AWAITER` to avoid races and to ensure timeouts/reconnects are handled uniformly.

### Example

```csharp
Control pong = await session.AwaitControlAsync(
    c => c.Type == ControlType.PONG,
    timeoutMs: 3000,
    ct);

var (rttMs, _) = await session.PingAsync(opCode: 0, timeoutMs: 3000, ct: ct);
```

---

## Directive handling (DirectiveClientExtensions)

- `TryHandleDirectiveAsync` inspects an incoming `Directive` packet and handles the four protocol control types:
  - `THROTTLE`: records the throttle window in monotonic ticks and triggers `OnThrottle` callbacks.
  - `REDIRECT`: optionally delegates to a callback, otherwise resolves `(host, port)` from the directive args, updates `TransportOptions`, disconnects, and reconnects.
- `NACK` / `NOTICE`: forwards to callbacks and logs the reason.
- `IsThrottled(out TimeSpan remaining)` reports active throttle windows based on monotonic clocks.
- `SendWithThrottleAsync` waits for the active throttle window before sending a packet, keeping the client protocol-compliant.
- `ClearThrottle` resets any stored throttle state for the client.

### DirectiveCallbacks

`DirectiveCallbacks` is the optional callback bundle passed into `TryHandleDirectiveAsync`.

It lets you plug custom behavior for:

- `OnNotice`
- `OnNack`
- `OnThrottle`
- `OnRedirectAsync`

Use it when you want the SDK helpers to keep directive parsing and throttle tracking, while your app decides how UI, logging, reconnect UX, or redirect policy should behave.

### Example

```csharp
var callbacks = new DirectiveCallbacks
{
    OnNotice = directive => Console.WriteLine(directive.Reason),
    OnNack = directive => Console.WriteLine(directive.Action),
    OnThrottle = (directive, delay) => Console.WriteLine(delay),
    OnRedirectAsync = async (directive, ct) =>
    {
        await Task.Yield();
        return false; // let the default reconnect flow continue
    }
};

bool handled = await session.TryHandleDirectiveAsync(
    directive,
    callbacks: callbacks,
    ct: ct);

if (session.IsThrottled(out TimeSpan remaining))
{
    Console.WriteLine($"throttled for {remaining.TotalMilliseconds} ms");
}
```

---

## Request/response helpers (RequestExtensions)

- `RequestAsync<TRequest, TResponse>` combines send+await into a single, race-free operation.
- Overload with `RequestOptions` controls timeout, retry count, and optional encryption (`Encrypt = true` requires `TcpSessionBase`).
- `RequestOptions.Default` ships with 5s timeout, no retries, and no encryption; fluent builders (`WithTimeout`, `WithRetry`, `WithEncrypt`) make tweaks easy.
- Only `TimeoutException` is retried; other fatal errors (disconnects, invalid packets) propagate immediately.
- `RequestAsync<TResponse>(IPacket request, RequestOptions? options = null, Func<TResponse, bool>? predicate = null)` handles both wildcard and filtered waits.
- The helpers log retry attempts via `ILogger` when `InstanceManager` provides one.

### Example

```csharp
Control request = session.NewControl(opCode: 1, type: ControlType.NOTICE).Build();
Control reply = await session.RequestAsync<Control>(
    request,
    RequestOptions.Default.WithTimeout(5_000),
    r => r.Type == ControlType.PONG,
    ct: ct);
```

---

## Subscription helpers (TcpSessionSubscriptions)

- `On<TPacket>` / `On(predicate, handler)` register handlers that own the `IBufferLease` and dispose it inside a `finally` block.
- `OnOnce<TPacket>` fires exactly once, auto-unsubscribing even under concurrent arrivals.
- `SubscribeTemp<TPacket>` combines a temporary message handler with an optional `OnDisconnected` hook for request/response scenarios.
- `Subscribe` op encodes multiple subscriptions into a `CompositeSubscription` for easy disposal.
- Exceptions thrown by subscribers are caught and logged so that the receive loop never faults.

### Example

```csharp
using var sub = session.On<Control>(packet =>
{
    Console.WriteLine(packet.ToString());
});

using var once = session.OnOnce<Directive>(directive =>
{
    Console.WriteLine(directive.Type);
});
```

---

## Best practices

Flow: connect session → perform handshake → optionally handle directives/throttle → use `PingAsync`/`RequestAsync` with awaiters → dispose subscriptions.

- Always dispose the `IDisposable` returned by subscription helpers (use `using var`), especially before issuing `RequestAsync` calls.
- When sending throttled traffic, wrap `SendWithThrottleAsync` around your packets so you never violate server directives.
- Use `RequestOptions.WithEncrypt()` only on `TcpSession`/`IoTTcpSession` instances; the base class exposes `SendAsync(packet, encrypt: true)` for encryption-aware transports.

## Example

```csharp
using var sub = session.On<Control>(packet =>
{
    Console.WriteLine("pong received");
});

Control reply = await session.RequestAsync<Control>(
    new Control { Type = ControlType.PING },
    RequestOptions.Default.WithTimeout(TimeSpan.FromSeconds(3)),
    r => r.Type == ControlType.PONG,
    ct);
```

## Related APIs

- [TCP Session](./tcp-session.md)
- [Subscriptions](./subscriptions.md)
- [Request Options](./options/request-options.md)
- [Cryptography](../security/cryptography.md)
- [Built-in Frames](../framework/packets/built-in-frames.md)
