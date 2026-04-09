# Starter Template

This page gives you a copy-paste server template for a new Nalix project.

Use it when you want one working server shape first and plan to split it into cleaner files after the runtime is already alive.

!!! note "Copy first, refactor second"
    The template is intentionally compact.
    Get one working server online first, then split startup, handlers, middleware, and metadata into cleaner files.

It is intentionally opinionated:

- one startup file
- one protocol
- one listener
- one middleware
- one metadata provider
- one sample handler group

Use it as your first working skeleton, then split it into more files later.

## Recommended folder structure

```text
SampleServer/
  Program.cs
  Protocols/
    SampleProtocol.cs
  Listeners/
    SampleTcpListener.cs
  Middleware/
    SampleAuditMiddleware.cs
  Metadata/
    SampleTenantMetadataProvider.cs
  Handlers/
    SamplePingHandlers.cs
```

## `Program.cs`

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Logging;
using Nalix.Network.Options;
using Nalix.Network.Listeners.Tcp;
using Nalix.Network.Protocols;
using Nalix.Network.Routing;

NetworkSocketOptions socket = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
socket.Validate();

DispatchOptions dispatchOptions = ConfigurationManager.Instance.Get<DispatchOptions>();
dispatchOptions.Validate();

ConnectionLimitOptions connectionLimitOptions =
    ConfigurationManager.Instance.Get<ConnectionLimitOptions>();
connectionLimitOptions.Validate();

ConnectionHubOptions hubOptions = ConfigurationManager.Instance.Get<ConnectionHubOptions>();
hubOptions.Validate();

PoolingOptions pooling = ConfigurationManager.Instance.Get<PoolingOptions>();
pooling.Validate();

ILogger logger = BuildLogger();
IPacketRegistry packetRegistry = BuildPacketRegistry();

InstanceManager.Instance.Register(logger);
InstanceManager.Instance.Register(packetRegistry);

PacketMetadataProviders.Register(new SampleTenantMetadataProvider());

PacketDispatchChannel dispatch = new(options =>
{
    options.WithLogging(logger)
           .WithErrorHandling((ex, opcode) =>
           {
               logger.Error($"dispatch-error opcode=0x{opcode:X4}", ex);
           })
           .WithMiddleware(new SampleAuditMiddleware<IPacket>())
           .WithHandler(() => new SamplePingHandlers());
});

SampleProtocol protocol = new(dispatch);
SampleTcpListener listener = new(socket.Port, protocol);

dispatch.Activate();
listener.Activate();

Console.WriteLine("Sample server started. Press ENTER to stop.");
Console.ReadLine();

listener.Deactivate();
dispatch.Dispose();
listener.Dispose();

static ILogger BuildLogger()
{
    return NLogix.Host.Instance;
}

static IPacketRegistry BuildPacketRegistry()
{
    // Replace with your packet catalog registration
    throw new NotImplementedException();
}
```

## `Protocols/SampleProtocol.cs`

```csharp
using Nalix.Common.Networking;
using Nalix.Network.Protocols;
using Nalix.Network.Routing;

public sealed class SampleProtocol : Protocol
{
    private readonly PacketDispatchChannel _dispatch;

    public SampleProtocol(PacketDispatchChannel dispatch)
    {
        _dispatch = dispatch;
        this.SetConnectionAcceptance(true);
    }

    public override void ProcessMessage(object sender, IConnectEventArgs args)
        => _dispatch.HandlePacket(args.Lease, args.Connection);

    protected override bool ValidateConnection(IConnection connection)
    {
        // Add IP or session admission checks here if needed
        return true;
    }
}
```

## `Listeners/SampleTcpListener.cs`

```csharp
using Nalix.Common.Networking;
using Nalix.Network.Listeners.Tcp;

public sealed class SampleTcpListener : TcpListenerBase
{
    public SampleTcpListener(ushort port, IProtocol protocol) : base(port, protocol) { }
}
```

## `Middleware/SampleAuditMiddleware.cs`

```csharp
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Middleware;
using Nalix.Network.Routing;

[MiddlewareOrder(-20)]
[MiddlewareStage(MiddlewareStage.Inbound)]
public sealed class SampleAuditMiddleware<TPacket> : IPacketMiddleware<TPacket>
    where TPacket : IPacket
{
    public async Task InvokeAsync(
        PacketContext<TPacket> context,
        Func<CancellationToken, Task> next)
    {
        ushort opcode = context.Attributes.PacketOpcode.OpCode;

        Console.WriteLine(
            $"opcode=0x{opcode:X4} endpoint={context.Connection.NetworkEndpoint}");

        await next(context.CancellationToken);
    }
}
```

## `Metadata/SampleTenantMetadataProvider.cs`

```csharp
using System.Reflection;
using Nalix.Network.Routing;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PacketTenantAttribute : Attribute
{
    public string Tenant { get; }

    public PacketTenantAttribute(string tenant) => Tenant = tenant;
}

public sealed class SampleTenantMetadataProvider : IPacketMetadataProvider
{
    public void Populate(MethodInfo method, PacketMetadataBuilder builder)
    {
        PacketTenantAttribute? attr = method.GetCustomAttribute<PacketTenantAttribute>();
        if (attr is not null)
            builder.Add(attr);
    }
}
```

## `Handlers/SamplePingHandlers.cs`

```csharp
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;

[PacketController("SamplePingHandlers")]
public sealed class SamplePingHandlers
{
    [PacketOpcode(0x1001)]
    [PacketTenant("core")]
    public ValueTask<Control> Handle(Control request, IConnection connection)
    {
        request.Type = ControlType.PONG;
        return ValueTask.FromResult(request);
    }
}
```

The same template works for custom packet types by swapping `Control` for your own packet contract and keeping the generic middleware/context shape aligned.
The listener bridge still goes through `ProcessFrame(...)` before your protocol override handles `ProcessMessage(...)`.

## How to extend this template

### Add more handlers

Create more handler classes and register them in one place:

```csharp
options.WithHandler(() => new SamplePingHandlers())
       .WithHandler(() => new SampleAccountHandlers())
       .WithHandler(() => new SampleAdminHandlers());
```

### Add stronger middleware

Common next additions:

- permission middleware
- timeout middleware
- rate limiting
- concurrency limiting

### Add diagnostics

At minimum, keep access to:

- `listener.GenerateReport()`
- `protocol.GenerateReport()`
- `dispatch.GenerateReport()`

## First things to replace

When you copy this template into a real project, replace:

- `BuildPacketRegistry()`
- `Control`
- `PacketTenantAttribute`
- the sample middleware logic

Those placeholders are there only to give your team a stable starting shape.

!!! caution "Do not ship the placeholders"
    Replace `BuildPacketRegistry()` and the sample packet and metadata types before treating this as production code.

## Related pages

- [Server Blueprint](./server-blueprint.md)
- [Production Checklist](./production-checklist.md)
- [TCP Request/Response](./tcp-request-response.md)
