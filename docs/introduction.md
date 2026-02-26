# Introduction

The easiest way to think about Nalix is:

- `Nalix.Network` runs the server side
- `Nalix.Network.Hosting` can wrap the server bootstrap in a fluent host builder
- `Nalix.SDK` runs the client side
- both sides share packet contracts and metadata through `Nalix.Common` and `Nalix.Framework`

## What stays shared

Across the stack, Nalix tries to keep these pieces aligned:

- packet types and opcodes
- middleware-related metadata
- configuration-driven runtime options
- logging and service registration patterns

That is why a typical setup registers shared services early:

## Minimal example

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Injection;
using Nalix.Logging;

ILogger logger = NLogix.Host.Instance;

InstanceManager.Instance.Register<ILogger>(logger);
InstanceManager.Instance.Register<IPacketRegistry>(packetRegistry);
```

## If you are here for

| Goal | Start with |
|---|---|
| Build a TCP or UDP server | [Quickstart](./quickstart.md) |
| Build a server with a fluent host builder | [Nalix.Network.Hosting](./packages/nalix-network-hosting.md) |
| Build a TCP client | [Nalix.SDK](./packages/nalix-sdk.md) |
| Understand package layout | [Packages Overview](./packages/index.md) |
| Understand packet metadata and dispatch | [Nalix.Network](./packages/nalix-network.md) |

## Server mental model

A server usually looks like this:

1. load `NetworkSocketOptions`, `DispatchOptions`, and related options
2. register `ILogger` and `IPacketRegistry`
3. build `PacketDispatchChannel`
4. create a `Protocol`
5. start `TcpListenerBase` or `UdpListenerBase`

With `Nalix.Network.Hosting`, those same steps can be wrapped behind `NetworkApplication.CreateBuilder()` and `RunAsync()`.

## Client mental model

A client usually looks like this:

1. load or create `TransportOptions`
2. create `TransportSession`-derived `TcpSession`
3. connect
4. perform handshake or control flow if needed
5. use `RequestAsync` or direct send helpers

## Recommended first reading

- [Installation](./installation.md)
- [Quickstart](./quickstart.md)
- [Nalix.Network](./packages/nalix-network.md)
- [Nalix.Network.Hosting](./packages/nalix-network-hosting.md)
- [Nalix.SDK](./packages/nalix-sdk.md)

## Version note

Latest verified stable package version on 2026-03-24:

- `Nalix.Network`: `11.8.0`
- `Nalix.SDK`: `11.8.0`
