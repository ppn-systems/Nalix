# TcpListenerBase — TCP acceptor, limiter, and dispatch bootstrapper

`TcpListenerBase` is the main TCP server foundation in Nalix.Network. It owns the listening socket, accept workers, connection limiter, object-pool setup, process-channel backpressure, timing-wheel integration, and the handoff from newly accepted sockets into `Protocol.OnAccept(...)`.

## Mapped sources

- `src/Nalix.Network/Listeners/TcpListener/TcpListener.Core.cs`
- `src/Nalix.Network/Listeners/TcpListener/TcpListener.PublicMethods.cs`
- `src/Nalix.Network/Listeners/TcpListener/TcpListener.Handle.cs`
- `src/Nalix.Network/Listeners/TcpListener/TcpListener.ProcessChannel.cs`
- `src/Nalix.Network/Listeners/TcpListener/TcpListener.SocketConfig.cs`
- `src/Nalix.Network/Listeners/TcpListener/TcpListener.Metrics.cs`

## Main responsibilities

- validate and load `NetworkSocketOptions`
- tune thread-pool minima on Windows when `TuneThreadPool` is enabled
- create / configure the listen socket
- accept incoming sockets in parallel
- reject abusive endpoints via `ConnectionLimiter`
- initialize `Connection` objects and wire their events
- queue accepted connections into a bounded process channel
- invoke `Protocol.OnAccept(...)` on the dedicated process thread
- manage `TimingWheel` activation when idle timeout tracking is enabled

## Startup flow

`Activate(ct)` currently does the following:

1. refuses startup if `MaxParallel < 1`
2. transitions `STOPPED -> STARTING -> RUNNING`
3. creates a linked cancellation source and registers `SCHEDULE_STOP()`
4. initializes the listen socket when needed
5. optionally activates `TimingWheel`
6. schedules `MaxParallel` accept workers via `TaskManager`
7. starts the process channel thread with `START_PROCESS_CHANNEL(...)`

## Accept path

Each accept worker runs `AcceptConnectionsAsync(...)`:

- accepts one socket via `CreateConnectionAsync(...)`
- enforces `ConnectionLimiter`
- creates a `Connection`
- wires `OnCloseEvent`, `OnProcessEvent`, and `OnPostProcessEvent`
- registers the connection in `TimingWheel` if timeout support is enabled
- dispatches the connection to the process channel

The code also keeps a synchronous `AcceptNext(...)` / `HandleAccept(...)` path using pooled `SocketAsyncEventArgs`, but the async worker loop is the normal flow described by the current public lifecycle.

## Process channel

Accepted connections are not processed directly on the accept worker. Instead:

- producers write `IConnection` into a bounded channel
- one dedicated background thread drains that channel at `BelowNormal` priority
- `DISPATCH_CONNECTION(...)` drops new writes when the channel is full
- dropped connections increment rejected metrics and are closed immediately

This design protects packet-processing callbacks from being starved by bursts of new accepts.

## Shutdown flow

`Deactivate(ct)`:

- transitions `RUNNING/STARTING -> STOPPING`
- cancels the linked CTS
- closes the listen socket
- stops the process channel
- cancels the listener worker group
- closes all active connections through `ConnectionHub`
- deactivates `TimingWheel` when enabled
- returns the state to `STOPPED`

`Dispose()` calls `Deactivate()`, unsubscribes from `TimeSynchronizer`, closes the socket, and disposes the internal semaphore / cancellation resources.

## Diagnostics

`GenerateReport()` prints:

- port, state, and disposal flag
- socket configuration values
- accept/reject/error metrics
- bound protocol name
- active connection count from `ConnectionHub`
- current thread-pool minima
- whether time sync is enabled

## Notes

- `IsTimeSyncEnabled` can only be changed while the listener is stopped.
- `ConnectionLimiter` is created in the constructor and attached to every accepted connection close path.
- Pool capacities for accept contexts, listener contexts, and socket args are configured from `PoolingOptions` during construction.

## See also

- [Protocol README](../Protocol/README.md)
- [ConnectionLimiter](../Throttling/ConnectionLimiter.md)
- [NetworkSocketOptions](../Configuration/NetworkSocketOptions.md)
