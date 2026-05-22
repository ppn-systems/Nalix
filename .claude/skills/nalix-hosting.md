# Nalix.Hosting

## Triggers
- Bootstrapping a new Nalix server application
- Adding handlers, middleware, or custom protocols
- Configuring lifecycle (start/stop) behavior
- Modifying transport bindings or options

---

## Rules

### Builder API Surface
Key methods on `INetworkApplicationBuilder`:

| Method | Purpose |
| :--- | :--- |
| `Configure<TOptions>(Action<TOptions>)` | Bind config POCO (INI → options) |
| `ConfigureLogging(ILogger)` | Inject logger into the application |
| `ConfigureConnectionHub(IConnectionHub)` | Override default connection hub |
| `ConfigureSessionService(ISessionService)` | Override default session service |
| `ConfigureSessionStore(ISessionStore)` | Override default session store |
| `ConfigureSessionFactory(ISessionFactory)` | Override default session factory |
| `ConfigureDispatchOptions(Action<PacketDispatchOptions<IPacket>>)` | Wire middleware, tune dispatch |
| `AddHandler<THandler>()` | Register a packet controller |
| `ScanHandlers<TMarker>()` | Register all handlers in the assembly containing `TMarker` |
| `BindTcp<TProtocol>().Bind()` | Bind a TCP listener |
| `BindUdp<TProtocol>().Bind()` | Bind a UDP listener |
| `BindWebSocket<TProtocol>().Bind()` | Bind a WebSocket listener |
| `Build()` | Produce a `NetworkApplication` |

### Startup Sequence (Fixed Order)
1. `Configure<TOptions>()` — config loading/binding
2. `Configure*()` calls — service wiring (logging, hub, session, pool managers, etc.)
3. Handler registration via `AddHandler<T>()` / `ScanHandlers<T>()` — opcode-keyed, order does not matter
4. Middleware wiring via `ConfigureDispatchOptions(opts => opts.WithMiddleware(...))` — **execution order = registration order**
5. `Build()` — finalizes the builder
6. `await host.ActivateAsync()` or `await host.RunAsync()`

### Auto-Registered Handlers
`NetworkApplicationBuilder` always registers these four — do not register them again:
- `KeyExchangeHandlers`
- `HandshakeHandlers`
- `SessionHandlers`
- `SystemControlHandlers`

### Lifecycle
- `ActivateAsync()` starts packet dispatch — does not directly manage socket open/close
- `DeactivateAsync()` stops dispatch — listener lifecycle is separate
- `RunAsync()` = `ActivateAsync()` + await cancellation + `DeactivateAsync()`
- Start/stop is guarded by a `SemaphoreSlim` — not reentrant; calling `ActivateAsync()` twice without `DeactivateAsync()` will deadlock

### Resources
- `Resource.Designer.cs` is auto-generated from `Resource.resx` on every build
- **Never edit `Resource.Designer.cs` directly** — changes are silently overwritten on next build

---

## Checklists

### Bootstrap a new server
```csharp
using var host = NetworkApplication.CreateBuilder()
    .BindTcp<MyProtocol>().Bind()
    .ConfigureLogging(NLogix.Host.Instance)
    .Configure<NetworkSocketOptions>(opt => opt.Port = 8080)
    .AddHandler<MyPacketHandler>()
    .ScanHandlers<MyMarkerType>()           // scan all handlers in that assembly
    .ConfigureDispatchOptions(opts => {
        opts.WithMiddleware(new AuthMiddleware());       // order = execution order
        opts.WithMiddleware(new RateLimitMiddleware());
    })
    .Build();

await host.RunAsync();                      // ActivateAsync → wait → DeactivateAsync
```

### Add a custom protocol
1. Implement `IProtocol` — use `DefaultProtocol` as reference
2. `builder.BindTcp<MyProtocol>().Bind()` or `builder.BindUdp<MyProtocol>().Bind()`
3. Protocol receives the parsed packet before dispatch — inject pre-processing here if needed

### Add error/status strings
1. Edit `Resource.resx` — add key/value pair
2. Build — `Resource.Designer.cs` regenerates automatically
3. Access via `Resource.MyNewKey`

### Graceful shutdown
1. Call `await host.DeactivateAsync()` — stops new packet processing
2. Allow in-flight handler tasks to complete (await TaskManager workers if needed)
3. Close listeners
4. Call `NLogix.Host.Instance.Dispose()` — flushes the log channel before exit

---

## Gotchas

- **Middleware registration order is execution order**: Middleware is wired via `ConfigureDispatchOptions(opts => opts.WithMiddleware(...))`. The first call to `WithMiddleware` runs first. If you register business logic before an auth middleware, unauthenticated requests will reach business logic.

- **`DeactivateAsync()` does not flush in-flight packets**: Stopping dispatch immediately stops processing. Packets queued in the channel but not yet handled are dropped. If graceful drain is required, wait for the dispatch channel to empty before calling `DeactivateAsync()`.

- **`SemaphoreSlim` gate is not reentrant**: `ActivateAsync()` acquires the gate; calling it again without `DeactivateAsync()` will block forever. This includes any code path that calls `ActivateAsync()` conditionally.

- **`Resource.Designer.cs` overwrites manual edits silently**: The `ResXFileCodeGenerator` runs on build. Any edits directly to `.Designer.cs` are lost on next `dotnet build`.

- **Handler auto-registration cannot be suppressed**: The four system handlers are always added by `NetworkApplicationBuilder`. You cannot remove or replace them via the builder API — modifying them requires forking the builder.
