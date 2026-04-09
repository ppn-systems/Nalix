# Nalix.Network.Hosting

`Nalix.Network.Hosting` adds Microsoft-style host and builder APIs on top of `Nalix.Network`.

## What it includes

- `NetworkApplication.CreateBuilder()`
- fluent `NetworkApplicationBuilder` configuration
- automatic packet registry discovery
- packet dispatch bootstrap and runtime lifecycle
- TCP server startup via `ActivateAsync`, `DeactivateAsync`, and `RunAsync`

## Typical use

```csharp
NetworkApplication host = NetworkApplication.CreateBuilder()
                                            .ConfigureLogging(logger)
                                            .Configure<NetworkSocketOptions>(options => options.Port = 57206)
                                            .AddHandlers<PacketCommandHandler>()
                                            .AddPacketAssemblies<HandshakePacket>()
                                            .AddMetadataProvider<PacketTagMetadataProvider>()
                                            .AddTcp<ExamplePacketProtocol>()
                                            .AddUdp<ExamplePacketProtocol>()
                                            .Build();

await host.RunAsync();
```

The hosting package bridges into Nalix's current `InstanceManager` and `ConfigurationManager` runtime so existing network components can keep working without manual bootstrap code.
