# Server Boilerplate Template

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Beginner to Intermediate
    - :fontawesome-solid-clock: **Time**: 5 minutes (Copy-Paste)
    - :fontawesome-solid-book: **Prerequisites**: [Quickstart](../../quickstart.md)

This page provides a robust, production-ready starting point for any Nalix server. It is structured to be easy to copy into a new project while allowing for deep customization as your needs grow.

---

## 1. The Simplest Entry Point (Hosting Builder)

For 99% of applications, the **Hosting Builder** is the standard way to bootstrap. It handles dependency injection, service orchestration, and lifecycle management automatically.

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Logging;
using Nalix.Hosting;
using Nalix.Network.Options;
using Nalix.Environment.Configuration;

// 1. Load configuration from environment or .ini files
var socketOpts = ConfigurationManager.Instance.Get<NetworkSocketOptions>();

// 2. Build the application
using var app = NetworkApplication.CreateBuilder()
    .ConfigureLogging(NLogix.Host.Instance)
    .Configure<NetworkSocketOptions>(opt => 
    {
        opt.Port = socketOpts.Port;
        opt.Backlog = 1024;
        opt.EnableTimeout = true; // Enabled by default
    })
    .Configure<TimingWheelOptions>(opt => 
    {
        opt.IdleTimeoutMs = 60_000; // 60 second idle timeout
    })
    // 2.5. Configure Zero-Allocation Buffer Pooling
    .ConfigureBufferPoolManager(new BufferPoolManager(NLogix.Host.Instance))
    // Register your logic controllers
    .AddHandler<MyPingHandler>()
    // Attach the transport protocol
    .BindTcp<MyProtocol>().Bind()
    .Build();

// 3. Start the event loops
Console.WriteLine($"Server listening on port {socketOpts.Port}...");
await app.RunAsync();
```

---

## 2. Standard Application Components

In a real project, you should split these into separate files. This boilerplate shows them together for easy reference.

### The Protocol (Network Bridge)

The protocol translates raw frames into clean objects. Keep this thin; its only job is to forward data to the dispatcher.

!!! tip "Built-in Option"
    For 99% of use cases, you can skip writing a custom protocol class and use `DefaultProtocol` from the `Nalix.Hosting` namespace:
    ```csharp
    builder.BindTcp<DefaultProtocol>().Bind();
    ```

### The Handler (Business Logic)

Handlers are where your application logic lives. Use `IPacketContext<T>` to access the packet and the connection safely.

```csharp
using Nalix.Abstractions.Networking.Packets;

[PacketController("SystemHandlers")]
public sealed class MyPingHandler
{
    [PacketOpcode(0x1001)]
    public MyPongPacket OnPing(IPacketContext<MyPingPacket> context)
    {
        return new MyPongPacket { Message = "Pong!" };
    }
}
```

---

## 3. Advanced Configuration (Middleware & Policy)

Add these to your `CreateBuilder()` chain to harden your server for production traffic.

```csharp
builder.ConfigureDispatchOptions(options =>
{
    options.WithLogging(NLogix.Host.Instance)
           // Add security layers
           .WithMiddleware(new ConcurrencyMiddleware())
           .WithMiddleware(new RateLimitMiddleware())
           // Handle global failures
           .WithErrorHandling((ex, opcode) => 
           {
                Console.WriteLine($"Error in opcode 0x{opcode:X4}: {ex.Message}");
           });
});
```

---

## 4. Low-Level Manual Composition (Direct Path)

!!! danger "Advanced Only"
    Use this path only if you are building specialized transport libraries or need to bypass the Hosting layer for extreme performance tuning.

```csharp
// Manual setup of all components without the Hosting builder.
// Prefer the Hosting Builder above unless you are writing transport-level code.

PacketDispatchChannel dispatch = new(options =>
{
    options.WithHandler(() => new MyPingHandler());
});

IConnectionHub hub = new ConnectionHub();
DefaultProtocol protocol = new(dispatch);
TcpServerListener listener = new(5000, protocol, hub);

dispatch.Activate();
listener.Activate();

// ... run ...

listener.Deactivate();
hub.Dispose();
dispatch.Dispose();
```

!!! warning "Manual dependency wiring"
    `TcpServerListener` and its variants require an `IConnectionHub`. The Hosting builder creates the concrete internal listener and hub automatically; manual composition must provide both explicitly.

---

## Best Practices Checklist

- [x] **Contracts**: Keep packet POCOs in a separate project shared with the client.
- [x] **Logging**: Always use `NLogix` or a production-ready `ILogger`.
- [x] **Validation**: Call `.Validate()` on all Options objects before booting.
- [x] **Protocols**: Use `ValidateConnection(...)`, `IsAccepting`, and `SetConnectionAcceptance(bool)` intentionally. `DefaultProtocol` already enables acceptance for the common dispatch-forwarding path.

## Read this next

- [Server Blueprint](./server-blueprint.md)
- [Production End-to-End](../deployment/production-example.md)
- [TCP Request/Response](../networking/tcp-patterns.md)
- [Quickstart](../../quickstart.md)
