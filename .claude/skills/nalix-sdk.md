# Nalix.SDK

## Triggers
- Implementing a client application connecting to a Nalix server
- Setting up request-response or event-driven packet handling
- Handling reconnection or session resume from the client side
- Integrating Nalix with Unity or other main-thread-bound runtimes

---

## Rules

### Session Lifecycle
- Always use `ConnectAsync()` / `DisconnectAsync()` — never create raw sockets manually
- **Always `await DisconnectAsync()` before dispose** — raw `Dispose()` does not flush pending sends; data in the send buffer is lost
- `TcpSession` supports auto-reconnect — it creates a **new TCP connection** internally; `On<T>()` subscriptions from the previous connection instance do **not** transfer automatically

### Request-Response vs Subscriptions
- `RequestAsync<TResponse>()`: one-shot request with correlated response matching via Snowflake correlation ID and configurable timeout — use for commands, queries
- `On<T>()`: event-driven subscription for server-pushed messages — use for broadcasts, notifications, live updates
- These are not interchangeable: `RequestAsync` blocks (async) until the matching response arrives; `On<T>()` fires whenever any packet of type `T` arrives

### Thread Dispatching
- `IThreadDispatcher` abstracts callback dispatch to a specific thread (e.g., Unity main thread)
- `InlineDispatcher` executes callbacks on the receiving thread — correct for server-side and non-UI applications
- Use `IThreadDispatcher` when packet callbacks must run on a specific thread (e.g., modifying Unity GameObjects)

### AOT Compatibility
- `TrimMode=partial`, `IlcOptimizationPreference=Speed` — SDK is AOT-ready
- All serialization is source-generated — no reflection on the hot path
- Do not introduce `System.Reflection` usage in SDK extensions

---

## Checklists

### Connect and subscribe
```csharp
var session = new TcpSession(options);

// Subscribe before connecting so no messages are missed
session.On<ChatMessage>(msg => HandleChat(msg));
session.On<ServerNotice>(notice => ShowNotice(notice));

await session.ConnectAsync();
// Handshake is performed automatically
```

### Send a request and await response
```csharp
var response = await session.RequestAsync<LoginResponse>(
    new LoginRequest { Username = "...", Password = "..." },
    timeout: TimeSpan.FromSeconds(5)
);
```

### Handle reconnect with Unity dispatcher
```csharp
var session = new TcpSession(options, dispatcher: new UnityDispatcher());
session.OnReconnected += () => {
    // Re-subscribe because reconnect creates a new connection
    session.On<PositionUpdate>(pos => UpdatePlayerPosition(pos));
};
await session.ConnectAsync();
```

### Graceful disconnect
```csharp
// Always await — flushes pending sends before closing
await session.DisconnectAsync();
session.Dispose();
```

---

## Gotchas

- **`On<T>()` subscriptions do not survive auto-reconnect**: `TcpSession` auto-reconnect establishes a new TCP connection internally. Subscriptions registered before reconnect are attached to the old connection state. Re-subscribe in an `OnReconnected` callback.

- **Raw `Dispose()` drops pending sends**: If there are unsent packets in the send buffer and you call `Dispose()` without `await DisconnectAsync()`, they are silently dropped. Always disconnect first.

- **`RequestAsync` timeout does not cancel the server operation**: Timing out on the client side means the client stops waiting, but the server may still process the request and send a response — which will be silently dropped if no handler is waiting. Design idempotent server operations for retryable requests.

- **`TimeSyncCalculator` requires multiple samples for accuracy**: NTP-style clock sync converges after several round-trip samples. A single sample gives a rough estimate. Collect at least 3–5 samples before trusting the offset.

- **`On<T>()` fires for every packet of type `T`**: If the same server sends multiple response types under a shared base type, all of them trigger the `On<T>()` callback. Use `RequestAsync` for correlated one-shot responses.
