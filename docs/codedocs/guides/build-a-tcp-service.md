---
title: "Build a TCP Service"
description: "Create a production-style Nalix TCP server with shared packets, a protocol bridge, and packet controllers."
---

This guide shows the standard server path for Nalix: shared packet contracts, one protocol bridge, one packet controller, and a `NetworkApplication` host.

## Problem

You need a TCP server that can accept framed binary packets, route them to typed handlers, and return typed responses without manually managing sockets, registries, or controller dispatch.

## Solution

Use `Nalix.Network.Hosting` for startup, `PacketBase<TSelf>` for shared contracts, and a small `Protocol` subclass to forward transport events into `IPacketDispatch`.

<Steps>
<Step>
### Define shared packet contracts

Create a shared contracts project referenced by both the server and the client.

```csharp
using Nalix.Framework.DataFrames;
using Nalix.Common.Serialization;

[SerializePackable]
public sealed class PingRequest : PacketBase<PingRequest>
{
    public const ushort Code = 0x1001;
    public string Message { get; set; } = string.Empty;

    public PingRequest() => OpCode = Code;
}

[SerializePackable]
public sealed class PingResponse : PacketBase<PingResponse>
{
    public const ushort Code = 0x1002;
    public string Message { get; set; } = string.Empty;

    public PingResponse() => OpCode = Code;
}
```

</Step>
<Step>
### Implement the handler and protocol

The controller holds your application logic. The protocol is the transport bridge that forwards raw frames into the runtime.

```csharp
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Protocols;
using Nalix.Runtime.Dispatching;

[PacketController("PingHandlers")]
public sealed class PingHandlers
{
    [PacketOpcode(PingRequest.Code)]
    public PingResponse Handle(IPacketContext<PingRequest> context)
        => new() { Message = $"Pong: {context.Packet.Message}" };
}

public sealed class PingProtocol : Protocol
{
    private readonly IPacketDispatch _dispatch;

    public PingProtocol(IPacketDispatch dispatch) => _dispatch = dispatch;

    public override void ProcessMessage(object? sender, IConnectEventArgs args)
        => _dispatch.HandlePacket(args.Lease!, args.Connection);
}
```

</Step>
<Step>
### Build and run the host

`NetworkApplicationBuilder` will create the packet registry, compile the handlers, and activate the listener lifecycle for you.

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Logging;
using Nalix.Network.Hosting;
using Nalix.Network.Options;

ILogger logger = NLogix.Host.Instance;

using NetworkApplication app = NetworkApplication.CreateBuilder()
    .ConfigureLogging(logger)
    .Configure<NetworkSocketOptions>(options =>
    {
        options.Port = 5000;
        options.BufferSize = 64 * 1024;
        options.Backlog = 1024;
    })
    .AddPacket<PingRequest>()
    .AddHandler<PingHandlers>()
    .AddTcp<PingProtocol>()
    .Build();

await app.RunAsync();
```

</Step>
</Steps>

## Complete Runnable Layout

```text
Contracts/
  PingRequest.cs
  PingResponse.cs
Server/
  PingHandlers.cs
  PingProtocol.cs
  Program.cs
```

## Why This Works

`AddPacket<PingRequest>()` scans the contracts assembly for packet types. `AddHandler<PingHandlers>()` registers the controller so `PacketDispatchOptions<TPacket>` can compile handler delegates during startup. `AddTcp<PingProtocol>()` supplies the transport bridge. When the listener receives a frame, `PingProtocol.ProcessMessage(...)` forwards it to `IPacketDispatch`, which deserializes the packet, executes the handler, and sends the `PingResponse`.

## Real-World Pattern

In a real service you usually add middleware and tighter connection limits:

```csharp
using Nalix.Network.Options;
using Nalix.Runtime.Options;

using NetworkApplication app = NetworkApplication.CreateBuilder()
    .Configure<ConnectionLimitOptions>(options =>
    {
        options.MaxConnectionsPerIpAddress = 100;
        options.MaxPacketPerSecond = 512;
    })
    .Configure<DispatchOptions>(options =>
    {
        options.MaxPerConnectionQueue = 4096;
    })
    .ConfigureDispatch(options =>
    {
        options.WithDispatchLoopCount(8)
               .WithErrorHandling((exception, opcode) =>
               {
                   logger.LogError(exception, "Handler failed for opcode 0x{Opcode:X4}", opcode);
               });
    })
    .AddPacket<PingRequest>()
    .AddHandler<PingHandlers>()
    .AddTcp<PingProtocol>()
    .Build();
```

That matches the configuration patterns used in [example/Nalix.Network.Examples/Program.cs](/workspace/home/nalix/example/Nalix.Network.Examples/Program.cs).
