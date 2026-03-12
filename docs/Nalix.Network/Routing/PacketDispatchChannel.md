# PacketDispatchChannel & PacketDispatchOptions — High-Performance Async Packet Dispatcher for .NET Servers

The `PacketDispatchChannel` and `PacketDispatchOptions<TPacket>` system is a robust, asynchronous, multi-core event dispatch/handler engine designed for modern .NET network servers (TCP, RELIABLE, IoT, game backends, etc.).  
It supports DI, full error handling, middleware/microservice-style handler registration, and high-concurrency, zero-leak packet queueing.

---

## Key Features

- **Queue-based async dispatch:**  
  Incoming data/leases are queued for processing, enabling scalable, backpressure-friendly handling (ideal for many-core servers).

- **Multi-worker, parallel execution:**  
  Auto-scales worker loops according to server core count, maxing out at 12 for fairness and cache locality.

- **Handler/Controller registry:**  
  Simple, attribute-based registration of packet handler methods (controller-style: annotate methods with packet opcodes).

- **Middleware pipeline:**  
  Integrates with `MiddlewarePipeline<TPacket>` for inbound/outbound pre/post-processing (validation, security, transform...).

- **Full error mapping & reporting:**  
  Handler exceptions are mapped to protocol-correct codes/actions; all uncaught exceptions are logged and responded to.

- **Diagnostic reporting:**  
  Provides live, introspectable report string exposing queue depth/state, semaphore counts, handler registry, and more.

---

## Usage Example

### Basic Registration and Activation

```csharp
var dispatcher = new PacketDispatchChannel(opts =>
{
    opts.WithLogging(myLogger)
        .WithMiddleware(new MyInboundMiddleware())
        .WithHandler<MyPacketController>();
});

dispatcher.Activate();
// Now dispatcher.HandlePacket(...) can be called safely from concurrent threads
```

#### Register Custom Handlers/Controllers

```csharp
public class MyPacketController
{
    [PacketOpcode(0x1000)]
    public async Task OnMove(MyMovePacket packet, IConnection conn) { ... }

    [PacketOpcode(0x1001)]
    public async Task OnChat(MyChatPacket packet, IConnection conn) { ... }
}

// Registration:
opts.WithHandler<MyPacketController>();
```

#### Pushing Packets

```csharp
dispatcher.HandlePacket(dataLease, connection); // Queues for worker(s)
// or, if you have a decoded packet:
dispatcher.HandlePacket(myPacket, connection);  // Executes handler directly
```

---

## Middleware, Error Handling & Logging

- **Add middleware:** `opts.WithMiddleware(new MySecurityMiddleware());`
- **Control error handling granularity:**  
  - Use `WithErrorHandling((ex, opcode) => Logger.Error(...))` for handler-level errors
  - Use `WithErrorHandlingMiddleware(continueOnError, errorHandler)` for pipeline middleware
- **Activate logging:**  
  - `WithLogging(logger)`

---

## Handler Resolution / Routing

You can directly resolve handlers:

```csharp
if (opts.TryResolveHandler(0x1000, out var handler))
    await handler(packet, connection);
```

---

## Diagnostic Reporting Example

Call `dispatcher.GenerateReport()` for a human-friendly state dump:

```log
[2026-03-12 13:37:00] PacketDispatchChannel:
Running: yes | DispatchLoops: 8 | PendingPackets: 179
------------------------------------------------------------------------------------------------------------------------
Semaphore.CurrentCount: 2 | CTS.Cancelled: False

DispatchChannel diagnostics (best-effort via reflection):
  Ready queues (per-priority) - approximate queued connections:
    NORMAL   :   110
    HIGH     :    69
...
PacketRegistry: MyServer.PacketRegistry
...
Notes:
 - semaphore = semaphore (synchronization counter)
 - CTS = CancellationTokenSource
 - pending packets = packets waiting inside dispatch channel
```

---

## Tuning/Scaling

- The channel auto-adjusts worker loop count based on server hardware (no manual tuning required).
- Each packet type/controller is compiled and cached for handler lookup performance.
- Use large pools and increase backing queue size for very high-throughput scenarios (see PoolingOptions.md).

---

## Best Practices

- Always register handlers **before activating** the dispatcher.
- For proper shutdown, call `.Deactivate()` and `.Dispose()` cleanly.
- Use middleware for security (authz, anti-spam, throttling) and cross-cutting (audit, decompress/decrypt, etc).
- Use `.HandlePacket(lease, conn)` for high-throughput (lease-based) applications; direct packet dispatch only for small/test use.

---

## License

Licensed under the Apache License, Version 2.0.  
Copyright (c) 2025 PPN Corporation.

---

## See Also

- [Middleware documentation](../Middleware/README.md)
- [NetworkSocketOptions.md](../Configurations/NetworkSocketOptions.md)
- [PoolingOptions.md](../Configurations/PoolingOptions.md)
