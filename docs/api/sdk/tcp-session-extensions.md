# Nalix.SDK.Transport.Extensions — TcpSession Helpers for Control, Directive, and Request Flows

The `Nalix.SDK.Transport.Extensions` namespace enriches `TcpSession` with helpers for protocol control packets, directives, request/response coordination, throttling safety, and subscription management. The helpers keep receive loops resilient by owning leases and catching handler faults.

## Source mapping

- `src/Nalix.SDK/Transport/Extensions/ControlExtensions.cs`
- `src/Nalix.SDK/Transport/Extensions/RequestExtensions.cs`
- `src/Nalix.SDK/Transport/Extensions/TcpSessionSubscriptions.cs`
- `src/Nalix.SDK/Transport/Extensions/TcpSessionX25519Extensions.cs`

---

## Key capabilities

- Checklist:
  - Control helpers: `NewControl`, `AwaitControlAsync`, `SendControlAsync`.
  - Security: `HandshakeAsync` (X25519 key exchange).
  - Requests: `RequestAsync` with `RequestOptions` (timeout, retry, encrypt).
  - Subscriptions: `On`, `OnOnce`, `SubscribeTemp`, `CompositeSubscription`.

- Fluent `Control` builders and awaiters that use `PacketAwaiter` to avoid race conditions.
- Cryptographic handshake (`HandshakeAsync`) that performs an anonymous X25519 key exchange and enables AEAD encryption.
- Request/response helpers (`RequestAsync`, `RequestOptions`) that send, await, optionally encrypt, and retry safely.
- Subscription helpers (`On<T>`, `OnOnce<T>`, `SubscribeTemp`, `Subscribe`) that automatically dispose leases and log handler errors.

## Public members at a glance

| Type | Public members |
|---|---|
| `ControlExtensions` | `NewControl(...)`, `AwaitPacketAsync(...)`, `AwaitControlAsync(...)`, `SendControlAsync(...)`, `ControlBuilder` |
| `TcpSessionX25519Extensions` | `HandshakeAsync(...)` |
| `RequestExtensions` | `RequestAsync<TResponse>(...)` |
| `TcpSessionSubscriptions` | `On<TPacket>(...)`, `On(...)`, `OnOnce<TPacket>(...)`, `SubscribeTemp<TPacket>(...)`, `Subscribe(...)` |
| `CompositeSubscription` | `Add(...)`, `Dispose()` |

---

## Control helpers (ControlExtensions)

- `NewControl(opCode, ControlType, ProtocolType)` starts a fluent builder that stamps `MonoTicks`/`Timestamp` and lets you chain `.WithSeq()`, `.WithReason()`, `.WithTransport()`, then `.Build()`.
- `AwaitPacketAsync<TPkt>` / `AwaitControlAsync` waits for a matching packet/control with timeout and cancellation.
- `SendControlAsync` materializes the builder, applies any extra configuration, and transmits the CONTROL frame.
- All helpers use `PACKET_AWAITER` to avoid races and to ensure timeouts/reconnects are handled uniformly.

### Common pitfalls

- using `AwaitControlAsync(...)` without a prior send when you actually need a correlated request/response flow
- forgetting that `ControlBuilder` is a `ref struct`, so it cannot be captured in lambdas
- assuming `SendControlAsync(...)` is a separate transport layer instead of a convenience wrapper around `TcpSession.SendAsync(...)`

---

## Cryptographic Handshake (TcpSessionX25519Extensions)

- `HandshakeAsync` performs an anonymous Elliptic Curve Diffie-Hellman (ECDH) handshake using Curve25519.
- It generates an ephemeral key pair, exchanges public keys with the server, verifies server proofs, and derives a 32-byte shared session key.
- Upon success, it automatically updates the session's encryption settings (`Secret`, `Algorithm`, and `EncryptionEnabled`) to enable `ChaCha20Poly1305` encryption for subsequent traffic.

### Common pitfalls

- calling `HandshakeAsync` before the session is connected
- neglecting the cancellation token during the multi-step handshake process
- assuming the handshake is required for all connections; some servers may allow unencrypted signaling

### Example

```csharp
await session.ConnectAsync("127.0.0.1", 5000);
await session.HandshakeAsync(ct);

// All subsequent sends are now encrypted
await session.SendControlAsync(0, ControlType.NOTICE);
```

---

## Request/response helpers (RequestExtensions)

- `RequestAsync<TRequest, TResponse>` combines send+await into a single, race-free operation.
- Overload with `RequestOptions` controls timeout, retry count, and optional encryption (`Encrypt = true` requires `TcpSession`).
- `RequestOptions.Default` ships with 5s timeout, no retries, and no encryption; fluent builders (`WithTimeout`, `WithRetry`, `WithEncrypt`) make tweaks easy.
- Only `TimeoutException` is retried; other fatal errors (disconnects, invalid packets) propagate immediately.
- `RequestAsync<TResponse>(IPacket request, RequestOptions? options = null, Func<TResponse, bool>? predicate = null)` handles both wildcard and filtered waits.
- The helpers log retry attempts via `ILogger` when `InstanceManager` provides one.

### Common pitfalls

- using `Encrypt = true` on a client that is not `TcpSession`
- assuming non-timeout failures should be retried
- forgetting to supply a predicate when you need to correlate a specific reply

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

### Common pitfalls

- forgetting to dispose the returned subscription
- expecting subscriber exceptions to bubble out of the receive loop
- using `On(...)` when `OnOnce<TPacket>(...)` or `SubscribeTemp<TPacket>(...)` would better match the request shape

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

Flow: connect session → perform handshake → use `RequestAsync` with awaiters → dispose subscriptions.

- Always dispose the `IDisposable` returned by subscription helpers (use `using var`), especially before issuing `RequestAsync` calls.
- Use `RequestOptions.WithEncrypt()` only on `TcpSession`; the concrete client exposes `SendAsync(packet, encrypt: true)` for encryption-aware transport sends.

### Quick flow

1. connect the session
2. perform cryptographic handshake
3. create a control helper or request helper
4. subscribe only for the duration you need
5. dispose subscriptions when the flow is done

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
- [Handshake Protocol](../security/handshake.md)
- [Built-in Frames](../framework/packets/built-in-frames.md)
