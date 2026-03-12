# TcpListenerBase Library — Extensible High-Performance TCP Listener Framework for .NET

`TcpListenerBase` is an extensible .NET library for building production-grade TCP servers.  
It provides a thread-safe, event-driven, protocol-agnostic base with built-in features for resource pooling, parallelism, throttling, error handling, time sync, and diagnostics.  
**You inherit from this base to quickly build new custom TCP network listeners for any protocol or business domain**.

---

## Key Features

- **Protocol-Agnostic**: Plug in any `IProtocol` implementation; not tied to a specific wire/protocol format.
- **High Performance**: Supports max-parallel accept workers (scales with multicore CPUs).
- **Object Pooling**: Uses object pools for all async accept/event arg contexts (zero-copy, low-latency).
- **Connection Limiting/Throttling**: With `ConnectionLimiter` for DDoS control and resource fairness.
- **Time Sync Integration**: Optionally integrates with external/process-wide time sync systems for distributed time correctness.
- **Advanced Socket Tuning**: Supports IPv4/IPv6 dual mode, configurable socket options (KeepAlive, buffer sizes, backlog, reuse address).
- **Graceful Shutdown**: Clean resource teardown, robust error propagation, never leaks sockets.
- **Diagnostics and Reporting**: Full runtime state/metrics dump via `GenerateReport()`.
- **Windows IOCP Thread Pool Tuning**: Auto-tune minimum thread count for high-concurrency scenarios (optional).

---

## Usage Example

```csharp
// Define your custom protocol handler implementing IProtocol
public class EchoProtocol : IProtocol
{
    public void OnAccept(IConnection connection, CancellationToken ct) { ... }
    public void ProcessMessage(IConnection connection, IMessage msg) { ... }
    // ... more protocol methods
}

// Inherit from TcpListenerBase for your specific server/service
public class DemoListener : TcpListenerBase
{
    public DemoListener(IProtocol protocol) : base(protocol) { }
    // Optionally override methods or add hooks/events
}

// Setup, start, and stop your listener
var listener = new DemoListener(new EchoProtocol());
listener.Activate();                  // Start accept loop(s)
Console.WriteLine(listener.GenerateReport());
listener.Deactivate();                // Graceful shutdown
listener.Dispose();
```

---

## Configuration

Configure via the library's options classes:

- **NetworkSocketOptions**: Parallelism, buffer, keep-alive, reuse, backlog, enable timeouts, IPv6
- **PoolingOptions**: Max/preallocation for async event and context objects  
Configure through your DI container, config manager, or code.

---

## Technical Highlights

- Synchronous and asynchronous accept logic (wait, handle, process)
- Connection initialization is safe, separated from socket event reuse
- Robust close/teardown sequence ensures no event leaks or double-dispose
- Fully supports both IPv4 and IPv6
- Pooling and parallelization are always safe for concurrent loads
- Custom protocol instances are injected per service (no inheritance lock-in)
- Time synchronization event (`IsTimeSyncEnabled`) available for tightly-timed distributed systems
- Diagnostics ("report") include config, live connections, protocol, thread pool

---

## Diagnostics Example

Call `listener.GenerateReport()` for an instant snapshot:

```log
[2026-03-12 14:15:00] TcpListenerBase Status:
Port                : 8090
StateWrapper        : RUNNING
Disposed            : 0

Configuration:
--------------------------------------------
EnableTimeout       : True
MaxParallelAccepts  : 4
BufferSize          : 8192
...
```

---

## Extending & Customization

- Override protected methods (`Dispose`, `SynchronizeTime`, etc.) for custom lifecycle or monitoring.
- You can further extend your inherited class for metrics/admin endpoints, connection tracking, etc.
- Works seamlessly with any dependency injection system via `IProtocol`/DI containers.

---

## When should you use this lib?

- New .NET system/service needs custom TCP server logic — and you want production-grade connection handling, pooling, error recovery out of the box.
- You want to separate business protocol from the low-level socket accept/bind cycle (composition, not inheritance lock-in).
- You need live diagnostics, resource safety, or distributed time sync features in your TCP server.

---

## License

Licensed under the Apache License, Version 2.0.  
Copyright (c) 2025-2026 PPN Corporation.

---

## See Also

- [Protocol documentation](../Protocol/README.md)
- [PoolingOptions reference](../Configuration/PoolingOptions.md)
- [NetworkSocketOptions reference](../Configuration/NetworkSocketOptions.md)
