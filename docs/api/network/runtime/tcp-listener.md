# Tcp Listener

`TcpListenerBase` is the main TCP server foundation in Nalix.Network. It owns the listen socket, accept workers, connection limiter, object-pool setup, process-channel backpressure, timing-wheel integration, and the handoff from newly accepted sockets into `Protocol.OnAccept(...)`.

!!! note "Think of this as transport infrastructure"
    `TcpListenerBase` should stay focused on socket acceptance, connection admission, and runtime safety.
    Business logic should still live in dispatch handlers and middleware, not in the listener itself.

## Runtime flow

```mermaid
flowchart LR
    A["Accept worker"] --> B["ConnectionGuard"]
    B --> C["Connection"]
    C --> D["Bounded process channel"]
    D --> E["Protocol.OnAccept(...)"]
    E --> F["BeginReceive / packet flow"]
```

## Source mapping

- `src/Nalix.Network/Listeners/TcpListener/TcpListener.Core.cs`
- `src/Nalix.Network/Listeners/TcpListener/TcpListener.PublicMethods.cs`
- `src/Nalix.Network/Listeners/TcpListener/TcpListener.Handle.cs`
- `src/Nalix.Network/Listeners/TcpListener/TcpListener.ProcessChannel.cs`
- `src/Nalix.Network/Listeners/TcpListener/TcpListener.SocketConfig.cs`
- `src/Nalix.Network/Listeners/TcpListener/TcpListener.Metrics.cs`

## Main responsibilities

- validate and load `NetworkSocketOptions`
- tune thread-pool minima on Windows when `TuneThreadPool` is enabled
- create and configure the listen socket
- accept incoming sockets in parallel
- reject abusive endpoints via `ConnectionGuard`
- initialize `Connection` objects and wire their events
- queue accepted connections into a bounded process channel
- invoke `Protocol.OnAccept(...)` on the dedicated process thread
- manage `TimingWheel` activation when idle timeout tracking is enabled

## Public members at a glance

| Type | Public members |
|---|---|
| `TcpListenerBase` | `Activate(...)`, `Deactivate(...)`, `Dispose()`, `GenerateReport()`, `State`, `Port`, `Protocol`, `ConnectionHub` |

## Startup flow

`Activate(ct)` currently:

1. validates `MaxParallel >= 1`
2. transitions `STOPPED -> STARTING -> RUNNING`
3. creates a linked cancellation source and registers `SCHEDULE_STOP()`
4. initializes the listen socket when needed
5. optionally activates `TimingWheel`
6. schedules `MaxParallel` accept workers via `TaskManager`
7. starts the bounded process channel thread

### Common pitfalls

- treating the listener like an application layer instead of transport infrastructure
- leaving `TimingWheel` enabled without matching timeout expectations
- running with too small a `MaxParallel` value and then mistaking backlog for packet loss

## Accept path

Each accept worker runs `AcceptConnectionsAsync(...)`:

- accepts one socket via `CreateConnectionAsync(...)`
- enforces `ConnectionGuard`
- creates a `Connection`
- wires `OnCloseEvent`, `OnProcessEvent`, and `OnPostProcessEvent`
- registers the connection in `TimingWheel` if timeout support is enabled
- dispatches the connection to the process channel

## Process channel backpressure

Accepted connections are not processed directly on accept workers. Instead:

- producers write `IConnection` into a bounded channel
- one dedicated background thread drains that channel at `BelowNormal` priority
- `DISPATCH_CONNECTION(...)` drops new writes when the channel is full
- dropped connections increment rejected metrics and are closed immediately

This keeps new-connection setup from starving packet-processing callbacks.

!!! warning "Dropped channel writes are intentional protection"
    When the bounded process channel is full, new accepted connections can be closed immediately.
    Treat that as a signal to review backlog, dispatch pressure, and connection limits instead of trying to bypass the backpressure path.

### Failure modes worth knowing

- connection admission can fail before protocol code runs if `ConnectionGuard` rejects the endpoint
- a full process channel closes accepted sockets rather than letting the listener stall
- timeout tracking is optional, but if enabled it can close idle connections independently of dispatch health

## Shutdown flow

`Deactivate(ct)`:

- transitions into `STOPPING`
- cancels the linked CTS
- closes the listen socket
- stops the process channel
- cancels the listener worker group
- closes all active connections through `ConnectionHub`
- deactivates `TimingWheel` when enabled
- returns to `STOPPED`

### Disposal notes

- after `Dispose()`, accept workers and the process channel should be considered dead
- active connections are closed during shutdown, so do not keep references expecting them to remain usable

## Diagnostics

`GenerateReport()` prints:

- port, state, and disposal flag
- socket configuration values
- accept/reject/error metrics
- bound protocol name
- active connection count from `ConnectionHub`
- current thread-pool minima
- whether time sync is enabled

## Basic usage

```csharp
var protocol = new SampleProtocol();
var listener = new SampleTcpListener(protocol);

await listener.Activate(ct);

string report = listener.GenerateReport();
Console.WriteLine(report);
```

Typical flow:

1. configure sockets and timeouts
2. activate the listener
3. accept connections into the process channel
4. inspect `GenerateReport()` when traffic or backlog looks wrong

## Related APIs

- [Protocol](./protocol.md)
- [Connection Limiter](../../middleware/connection-limiter.md)
- [Connection](../connection/connection.md)
