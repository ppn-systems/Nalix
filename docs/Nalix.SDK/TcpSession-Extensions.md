# Nalix.SDK.Transport.Extensions — TcpSession Helpers for Control, Directive, and Request Flows

The `Nalix.SDK.Transport.Extensions` namespace enriches `IClientConnection`/`TcpSession` with helpers for protocol control packets, directives, secure handshakes, request/response coordination, throttling safety, and subscription management. The helpers keep receive loops resilient by owning leases and catching handler faults.

---

## Key capabilities

- Fluent `Control` builders, PING/PONG helpers, and awaiters that use `PacketAwaiter` to avoid race conditions.
- Secure X25519 handshake + `Keccak256` key derivation that installs the shared secret on `TransportOptions.Secret`.
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

---

## Handshake (HandshakeExtensions)

- `HandshakeAsync` performs a full X25519 Diffie-Hellman exchange using a helper `Handshake` packet (op code default 1).
- The server response is validated via an optional `validateServerPublicKey` callback; if accepted, the derived secret is hashed with SHA3 (`Keccak256`) and stored in `TransportOptions.Secret`.
- Sensitive material (private key + shared secret) is zeroed when the handshake completes or fails.
- A temporary subscription (`SubscribeTemp`) ensures the handshake response is captured without leaking listeners or leases.

---

## Directive handling (DirectiveClientExtensions)

- `TryHandleDirectiveAsync` inspects an incoming `Directive` packet and handles the four protocol control types:
  - `THROTTLE`: records the throttle window in monotonic ticks and triggers `OnThrottle` callbacks.
  - `REDIRECT`: optionally delegates to a callback, otherwise resolves `(host, port)` from the directive args, updates `TransportOptions`, disconnects, and reconnects.
  - `NACK` / `NOTICE`: forwards to callbacks and logs the reason.
- `IsThrottled(out TimeSpan remaining)` reports active throttle windows based on monotonic clocks.
- `SendWithThrottleAsync` waits for the active throttle window before sending a packet, keeping the client protocol-compliant.
- `ClearThrottle` resets any stored throttle state for the client.

---

## Request/response helpers (RequestExtensions)

- `RequestAsync<TRequest, TResponse>` combines send+await into a single, race-free operation.
- Overload with `RequestOptions` controls timeout, retry count, and optional encryption (`Encrypt = true` requires `TcpSessionBase`).
- `RequestOptions.Default` ships with 5s timeout, no retries, and no encryption; fluent builders (`WithTimeout`, `WithRetry`, `WithEncrypt`) make tweaks easy.
- Only `TimeoutException` is retried; other fatal errors (disconnects, invalid packets) propagate immediately.
- `RequestAsync<TResponse>(IPacket request, RequestOptions? options = null, Func<TResponse, bool>? predicate = null)` handles both wildcard and filtered waits.
- The helpers log retry attempts via `ILogger` when `InstanceManager` provides one.

---

## Subscription helpers (TcpSessionSubscriptions)

- `On<TPacket>` / `On(predicate, handler)` register handlers that own the `IBufferLease` and dispose it inside a `finally` block.
- `OnOnce<TPacket>` fires exactly once, auto-unsubscribing even under concurrent arrivals.
- `SubscribeTemp<TPacket>` combines a temporary message handler with an optional `OnDisconnected` hook for request/response scenarios.
- `Subscribe` op encodes multiple subscriptions into a `CompositeSubscription` for easy disposal.
- Exceptions thrown by subscribers are caught and logged so that the receive loop never faults.

---

## Best practices

- Always dispose the `IDisposable` returned by subscription helpers (use `using var`), especially before issuing `RequestAsync` calls.
- When sending throttled traffic, wrap `SendWithThrottleAsync` around your packets so you never violate server directives.
- Pin server public keys via `HandshakeAsync(... validateServerPublicKey: ...)` to harden clients against MITM.
- Use `RequestOptions.WithEncrypt()` only on `TcpSession`/`IoTTcpSession` instances; the base class exposes `SendAsync(packet, encrypt: true)` for encryption-aware transports.

---

## See also

- [TcpSession](./TcpSession.md)
- [Nalix.SDK README](./README.md)
- [Nalix.Network Architecture](../Nalix.Network/Architecture.md)
