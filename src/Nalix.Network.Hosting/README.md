# Nalix.Network.Hosting

`Nalix.Network.Hosting` adds Microsoft-style host and builder APIs on top of `Nalix.Network`.

## What it includes

- `NetworkHost.CreateBuilder()`
- fluent `NetworkBuilder` configuration
- automatic packet registry discovery
- packet dispatch bootstrap and runtime lifecycle
- TCP server startup via `StartAsync`, `StopAsync`, and `RunAsync`

## Typical use

```csharp
NetworkHost host = NetworkHost.CreateBuilder()
                              .UseLogger(logger)
                              .Configure<NetworkSocketOptions>(options => options.Port = 57206)
                              .AddPacketHandlersFromAssemblyContaining<PacketCommandHandler>()
                              .AddPacketsFromAssemblyContaining<HandshakePacket>()
                              .AddPacketMetadataProvider<PacketTagMetadataProvider>()
                              .AddTcpServer<ExamplePacketProtocol>()
                              .Build();

await host.RunAsync();
```

The hosting package bridges into Nalix's current `InstanceManager` and `ConfigurationManager` runtime so existing network components can keep working without manual bootstrap code.
