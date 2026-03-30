# Production Server Example

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Advanced
    - :fontawesome-solid-clock: **Time**: 20–30 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Quickstart](../../quickstart.md), [Architecture](../../concepts/fundamentals/architecture.md)

This guide describes how to build a production-ready Nalix application using best practices for logging, middleware, error handling, and structured contracts.

## 1. Project Setup

A production Nalix project typically splits into at least three assemblies:

1. **Contracts**: Shared POCOs with serialization attributes.
2. **Server**: The host application.
3. **Client**: The integration SDK or test suite.

```bash
dotnet new classlib -n MyNet.Contracts
dotnet new console -n MyNet.Server
dotnet new console -n MyNet.Client
```

## 2. Shared Contracts (`MyNet.Contracts`)

Use `[SerializePackable]` to define your packets. This ensures the source generator can create allocation-free deserializers.

```csharp
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;

namespace MyNet.Contracts;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class DataRequest : PacketBase<DataRequest>
{
    public const ushort OpCodeValue = 0x2001;

    [SerializeOrder(0)]
    public long RequestId { get; set; }

    [SerializeDynamicSize(128)]
    [SerializeOrder(1)]
    public string Payload { get; set; } = string.Empty;

    public DataRequest() => OpCode = OpCodeValue;
}

[SerializePackable(SerializeLayout.Explicit)]
public sealed class DataResponse : PacketBase<DataResponse>
{
    public const ushort OpCodeValue = 0x2002;

    [SerializeOrder(0)]
    public long RequestId { get; set; }

    [SerializeDynamicSize(128)]
    [SerializeOrder(1)]
    public string Message { get; set; } = string.Empty;

    public DataResponse() => OpCode = OpCodeValue;
}
```

## 3. Server Implementation (`MyNet.Server`)

In production, use `NetworkApplication.CreateBuilder()` to orchestrate services and protection policies.
The builder auto-registers the built-in handshake, session, and system-control handlers, so your guide only needs to add the domain-specific packets and controllers.

### Handler

```csharp
[PacketController("DataHandlers")]
public sealed class DataHandlers
{
    private readonly ILogger _logger;

    public DataHandlers(ILogger logger) => _logger = logger;

    [PacketOpcode(DataRequest.OpCodeValue)]
    public DataResponse OnRequest(IPacketContext<DataRequest> context)
    {
        _logger.LogInformation("Received request {Id}: {Payload}", 
            context.Packet.RequestId, context.Packet.Payload);

        return new DataResponse 
        { 
            RequestId = context.Packet.RequestId, 
            Message = "Success" 
        };
    }
}
```

### Server Host

```csharp
using Nalix.Hosting;
using Nalix.Logging;
using Nalix.Network.Options;
using Nalix.Runtime.Middleware.Standard;
using Nalix.Runtime.Dispatching;

var logger = NLogix.Host.Instance;

using var app = NetworkApplication.CreateBuilder()
    .ConfigureLogging(logger)
    .Configure<NetworkSocketOptions>(opt => {
        opt.Port = 5000;
        opt.Backlog = 1024;
    })
    // 1. Register contracts assembly for auto-discovery
    .AddPacket<DataRequest>() 
    // 2. Add Handlers (resolved via InstanceManager)
    .AddHandlers<DataHandlers>()
    // 3. Configure Dispatch Middleware
    .ConfigureDispatch(dispatch => {
        dispatch.WithLogging(logger)
                .WithMiddleware(new ConcurrencyMiddleware())
                .WithErrorHandling((ex, cmd) => logger.Error("Unhandled!", ex));
    })
    // 4. Bind Transport
    .AddTcp<ProductionProtocol>()
    .Build();

await app.RunAsync();

public sealed class ProductionProtocol : IProtocol
{
    private readonly IPacketDispatch _dispatch;

    public ProductionProtocol(IPacketDispatch dispatch) => _dispatch = dispatch;

    public void ProcessMessage(object sender, IConnectEventArgs args)
        => _dispatch.HandlePacket(args.Lease, args.Connection);
}
```

## 4. Client Integration (`MyNet.Client`)

The client uses the `Nalix.SDK` for high-level session management.

```csharp
using Nalix.SDK.Transport;
using MyNet.Contracts;

// The client needs the same registry as the server
IPacketRegistry catalog = new PacketRegistryFactory()
    .RegisterPacket<DataRequest>()
    .RegisterPacket<DataResponse>()
    .CreateCatalog();

await using var session = new TcpSession(new TransportOptions { Address = "127.0.0.1", Port = 5000 }, catalog);
await session.ConnectAsync();

var response = await session.RequestAsync<DataResponse>(new DataRequest 
{ 
    RequestId = 123, 
    Payload = "Hello Production" 
});

Console.WriteLine($"Server said: {response.Message}");
```

## 5. Production Checklist

- [ ] **Logging**: Ensure `NLogix` is configured with a high-performance sink (e.g., `BatchConsoleLogTarget`).
- [ ] **Timeouts**: Set `TimeoutMs` on all client calls via `RequestOptions`.
- [ ] **Backpressure**: Configure `NetworkSocketOptions.Backlog` and `DispatchOptions` (`MaxPerConnectionQueue`, `DropPolicy`).
- [ ] **Health Checks**: Use `IConnectionHub` to monitor live sessions.
- [ ] **Resource Cleanup**: Ensure all `IBufferLease` objects are disposed (handled automatically if using `IPacketContext<T>`).

## Next Steps

- [Middleware Concept](../../concepts/runtime/middleware-pipeline.md)
- [UDP Security Guide](../networking/udp-security.md)
- [Performance Optimizations](../../concepts/internals/performance-optimizations.md)
