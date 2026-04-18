# Server Boilerplate Template

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Beginner to Intermediate
    - :fontawesome-solid-clock: **Time**: 5 minutes (Copy-Paste)
    - :fontawesome-solid-book: **Prerequisites**: [Quickstart](../quickstart.md)

This page provides a robust, production-ready starting point for any Nalix server. It is structured to be easy to copy into a new project while allowing for deep customization as your needs grow.

---

## 1. The Simplest Entry Point (Hosting Builder)

For 99% of applications, the **Hosting Builder** is the standard way to bootstrap. It handles dependency injection, service orchestration, and lifecycle management automatically.

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Logging;
using Nalix.Network.Hosting;
using Nalix.Network.Options;
using Nalix.Framework.Configuration;

// 1. Load configuration from environment or .ini files
var socketOpts = ConfigurationManager.Instance.Get<NetworkSocketOptions>();

// 2. Build the application
using var app = NetworkApplication.CreateBuilder()
    .ConfigureLogging(NLogix.Host.Instance)
    .Configure<NetworkSocketOptions>(opt => 
    {
        opt.Port = socketOpts.Port;
        opt.Backlog = 1024;
    })
    // Add your packet contracts
    .AddPacket<MyPingPacket>()
    // Register your logic controllers
    .AddHandler<MyPingHandler>()
    // Attach the transport protocol
    .AddTcp<MyProtocol>()
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

```csharp
using Nalix.Common.Networking;
using Nalix.Network.Protocols;
using Nalix.Runtime.Dispatching;

public sealed class MyProtocol : Protocol
{
    private readonly IPacketDispatch _dispatch;

    // The hosting builder automatically injects the IPacketDispatch instance
    public MyProtocol(IPacketDispatch dispatch) => _dispatch = dispatch;

    public override void ProcessMessage(object? sender, IConnectEventArgs args)
        => _dispatch.HandlePacket(args.Lease, args.Connection);
}
```

### The Handler (Business Logic)

Handlers are where your application logic lives. Use `IPacketContext<T>` to access the packet and the connection safely.

```csharp
using Nalix.Common.Networking.Packets;

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
builder.ConfigureDispatch(options =>
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
// Manual setup of all components without the Hosting builder
PacketDispatchChannel dispatch = new(options =>
{
    options.WithHandler(() => new MyPingHandler());
});

MyProtocol protocol = new(dispatch);
TcpListenerBase listener = new(5000, protocol);

dispatch.Activate();
listener.Activate();

// ... run ...

listener.Deactivate();
dispatch.Dispose();
```

---

## Best Practices Checklist

- [x] **Contracts**: Keep packet POCOs in a separate project shared with the client.
- [x] **Logging**: Always use `NLogix` or a production-ready `ILogger`.
- [x] **Validation**: Call `.Validate()` on all Options objects before booting.
- [x] **Protocols**: Initialize `this.SetConnectionAcceptance(true)` in the constructor if you want to skip manual handshake.

## Read this next

- [Server Blueprint](./server-blueprint.md)
- [Production End-to-End](./production-end-to-end.md)
- [TCP Request/Response](./tcp-request-response.md)
- [Quickstart](../quickstart.md)
