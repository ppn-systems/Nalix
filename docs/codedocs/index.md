---
title: "Getting Started"
description: "Build your first Nalix server and client, understand what the framework solves, and find the next pages to read."
---

Nalix is a modular .NET 10 networking framework for building real-time TCP and UDP services with packet routing, middleware, packet registries, secure handshakes, and client transport sessions.

## The Problem

- Real-time .NET services usually force you to stitch together socket code, serialization, lifecycle control, and packet routing by hand.
- Low-level networking code becomes fragile when you need backpressure, queue fairness, pooling, and graceful shutdown under load.
- Binary protocol systems often drift because server and client packet definitions are duplicated or registered inconsistently.
- Secure session establishment, resumable sessions, and typed request/reply helpers are easy to get wrong and hard to retrofit later.

## The Solution

Nalix splits the stack into focused packages. `Nalix.Network.Hosting` builds the server, `Nalix.Runtime` dispatches packets through handlers and middleware, `Nalix.Framework` supplies packet definitions and the packet registry, and `Nalix.SDK` gives clients managed TCP and UDP sessions.

```csharp
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Serialization;
using Nalix.Framework.DataFrames;
using Nalix.Network.Hosting;
using Nalix.Network.Options;
using Nalix.Network.Protocols;
using Nalix.Runtime.Dispatching;

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

using var app = NetworkApplication.CreateBuilder()
    .AddPacket<PingRequest>()
    .AddHandler<PingHandlers>()
    .Configure<NetworkSocketOptions>(options => options.Port = 5000)
    .AddTcp<PingProtocol>()
    .Build();

await app.RunAsync();
```

## Installation

<Callout type="info">Nalix is distributed as NuGet packages, not JavaScript packages. The tabs below show the most common package combinations for server and client projects.</Callout>

" "Framework"]}>
<Tab value="Hosting">

```bash
dotnet add package Nalix.Network.Hosting
```

</Tab>
<Tab value="SDK">

```bash
dotnet add package Nalix.SDK
```

</Tab>
<Tab value="Logging">

```bash
dotnet add package Nalix.Logging
```

</Tab>
<Tab value="Framework">

```bash
dotnet add package Nalix.Framework
```

</Tab>
</Tabs>

Supported environments from the source tree and package metadata: .NET 10, C# 14, Windows, Linux, and macOS.

## Quick Start

This is the minimum viable server path from the source model in [README.md](/workspace/home/nalix/README.md), [NetworkApplication.cs](/workspace/home/nalix/src/Nalix.Network.Hosting/NetworkApplication.cs), and [Program.cs](/workspace/home/nalix/example/Nalix.Network.Examples/Program.cs).

```csharp
using Nalix.Common.Networking;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Network.Hosting;
using Nalix.Network.Options;
using Nalix.Network.Protocols;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Handlers;

public sealed class EchoProtocol : Protocol
{
    private readonly IPacketDispatch _dispatch;

    public EchoProtocol(IPacketDispatch dispatch) => _dispatch = dispatch;

    public override void ProcessMessage(object? sender, IConnectEventArgs args)
        => _dispatch.HandlePacket(args.Lease!, args.Connection);
}

using var app = NetworkApplication.CreateBuilder()
    .Configure<NetworkSocketOptions>(options => options.Port = 57206)
    .AddPacket<Handshake>()
    .AddHandler<SessionHandlers>()
    .AddTcp<EchoProtocol>()
    .Build();

await app.RunAsync();
```

Expected output:

```text
Nalix server is listening on tcp://127.0.0.1:57206
```

In a real application you normally add your own packet contracts, controllers marked with `[PacketController]`, and a concrete `Protocol` that forwards incoming frames to `IPacketDispatch`.

## Key Features

- Fluent server bootstrap through `NetworkApplication.CreateBuilder()` in `src/Nalix.Network.Hosting/NetworkApplication.cs`
- Immutable packet catalogs through `PacketRegistryFactory` and `PacketRegistry` in `src/Nalix.Framework/DataFrames`
- Shard-aware packet dispatch and middleware orchestration through `PacketDispatchChannel` in `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs`
- Managed client transports through `TcpSession` and `UdpSession` in `src/Nalix.SDK/Transport`
- Built-in handshake, resume, ping, control, and typed request/reply helpers in `src/Nalix.SDK/Transport/Extensions`
- High-throughput batched logging through `NLogix`, `BatchConsoleLogTarget`, and `BatchFileLogTarget` in `src/Nalix.Logging`

<Cards>
  <Card title="Architecture" href="/docs/architecture">See how hosting, transport, runtime, framework, and SDK layers fit together.</Card>
  <Card title="Core Concepts" href="/docs/packet-model">Learn the packet model, dispatch pipeline, sessions, and hosting bootstrap.</Card>
  <Card title="API Reference" href="/docs/api-reference/network-application">Jump to the source-mapped public APIs.</Card>
</Cards>
