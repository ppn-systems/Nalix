# Nalix.SDK.Transport.Extensions — ReliableClient & Protocol Extension API

The `Nalix.SDK.Transport.Extensions` namespace provides high-level extension methods for `IClientConnection` (e.g., `ReliableClient`). It enables robust TCP/UDP protocol development with advanced event subscriptions, secure handshake/key exchange, directive (server-side control) processing, time synchronization, throttling management, and fluent control/message construction.

---

## Key Capabilities

- **Fluent `Control`/`Directive`/Handshake builders for protocol framing**
- Event-driven message processing, with safe subscription APIs (`On`, `OnOnce`, composites)
- High-precision ping/pong (RTT measurement), optional clock auto-sync
- Client-server handshake via secure X25519 ECDH key exchange and SHA3-256 key derivation
- Time synchronization with server (one-shot drift-corrected)
- Server-side protocol directive handling: throttle, redirect, nack, notice
- Throttle window state tracking (auto-delays message sending if throttled)
- All APIs async/await and cancellation-token friendly

---

## API Quick Reference Table

| Extension Method                                           | Purpose/Description                                             |
|------------------------------------------------------------|-----------------------------------------------------------------|
| `NewControl` / Fluent builder                              | Create and configure a `Control` packet (ping, disconnect…)     |
| `AwaitPacketAsync<T>` / `AwaitControlAsync`                | Wait for a matching packet/control, with timeout                |
| `PingAsync`                                                | Send ping + await pong, measures RTT, option to auto-sync clock |
| `SendControlAsync`                                         | Fluent send for protocol control frames                         |
| `SendDisconnectAsync`                                      | Send protocol-compliant disconnect command                      |
| `TryHandleDirectiveAsync`                                  | Parse/handle DIRECTIVE frames (throttle, redirect, etc)         |
| `IsThrottled` / `SendWithThrottleAsync`                    | Check/apply throttle; auto-wait before sending if throttled     |
| `HandshakeAsync`                                           | X25519 ECDH key exchange + session key derivation               |
| `TimeSyncAsync`                                            | One-shot drift-corrected time synchronization with server       |
| `On<T>`, `OnOnce<T>`, `Subscribe`, `CompositeSubscription` | Strongly-typed, unsubscribe-able event hookups                  |

---

## Usage Examples

### 1. Ping & Await Pong with RTT Measurement

```csharp
var (rtt, pong) = await client.PingAsync(
    opCode: 3,
    timeoutMs: 1500,
    syncClock: true,   // Optionally synchronize client wall clock
    ct: cancellationToken
);
Console.WriteLine($"RTT: {rtt} ms, Server Device Time: {pong.Timestamp}");
```

### 2. Secure Handshake — ECDH Session Key Exchange

```csharp
bool ok = await client.HandshakeAsync(
    opCode: 1,
    timeoutMs: 5000,
    validateServerPublicKey: serverPk => PinOrAccept(serverPk), // optional
    ct: ct
);
if (!ok) throw new SecurityException("Crypto handshake failed");
```

### 3. Handling Received DIRECTIVE

```csharp
await client.On<Directive>(d => {
    if (d.Type == ControlType.THROTTLE)
        Console.WriteLine("Throttled by server...");
    // Handle REDIRECT/NOTICE/NACK as needed
});
```

### 4. Send respecting throttling (auto-delay if server said THROTTLE)

```csharp
await client.SendWithThrottleAsync(packet, ct);
```

---

## Event/Subscription System

- `On<T>(Action<T>)`: Subscribe to messages of a given type (lease handled for you)
- `OnOnce<T>(predicate, handler)`: Subscribe for only the next match (then auto-unsubscribed)
- `Subscribe`: Bundle multiple subscriptions into one disposable object

```csharp
// One-shot handler example
using var sub = client.OnOnce<Control>(ctrl => ctrl.Type == ControlType.PONG, handler);

// Composite example
var composite = client.Subscribe(
    client.On<Control>(...),
    client.On<Directive>(...)
);
```

---

## DIRECTIVE Processing (`TryHandleDirectiveAsync`)

```csharp
await client.TryHandleDirectiveAsync(
    packet,
    new DirectiveClientExtensions.DirectiveCallbacks {
        OnNotice = d => Console.WriteLine($"Server notice: {d.Action}"),
        OnNack = d => Logger.Warn($"Nack: Reason {d.Reason}")
    },
    resolveRedirect: (arg0,arg1,arg2) => ("redirect.example.com", (ushort)arg2),
    ct: ct
);
```

---

## Time Synchronization

```csharp
bool ok = await client.TimeSyncAsync(opCode: 2, timeoutMs: 2000, ct: ct);
if (!ok)
    Console.WriteLine("Time sync with server failed!");
```

---

## Best Practices & Notes

- All message event handlers automatically dispose receive buffer leases (no leaks)
- All handler exceptions are caught and logged so message receive loop remains robust
- Throttling window logic ensures no protocol-flood; always respect server throttle indications
- Use `HandshakeAsync` + `validateServerPublicKey` for best security (pinning/attestation)
- Use `CompositeSubscription` to manage lifecycle of several handlers together

---

## Reference

- [RFC 8439 — ChaCha20, Poly1305, AEAD, Secure Protocols](https://datatracker.ietf.org/doc/html/rfc8439)
- [NIST SP 800-56A — Key Establishment](https://csrc.nist.gov/publications/detail/sp/800-56a/rev-3/final)
- [Microsoft C# Events and Event Handling](https://learn.microsoft.com/en-us/dotnet/standard/events/)

---

## License

Licensed under the Apache License, Version 2.0.
