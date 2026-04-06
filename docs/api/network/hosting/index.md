# Nalix.Network.Hosting

`Nalix.Network.Hosting` provides a fluent, Microsoft-style builder API for configuring and running Nalix network hosts. It simplifies the setup of TCP and UDP listeners, packet dispatchers, and dependency injection.

## Source mapping

- `src/Nalix.Network.Hosting`

## Core Components

- **[NetworkHost](./network-host.md)**: The primary activatable host that manages the lifecycle of listeners and protocols.
- **[NetworkBuilder](./network-builder.md)**: The fluent builder used to configure the host.

## Getting Started

To create and run a Nalix host:

```csharp
var host = NetworkHost.CreateBuilder()
    .AddTcpServer<MyTcpProtocol>()
    .AddUdpServer<MyUdpProtocol>() // UDP Support
    .Build();

await host.RunAsync();
```

## Features

- **Fluent Configuration**: Easily add packets, handlers, and listeners.
- **Protocol Agnostic**: Host any `IProtocol` implementation.
- **Lifecycle Management**: Clean startup and shutdown of all network components.
- **Integrated Logging**: Seamlessly connects the Nalix runtime to the host logger.

## Related APIs

- [TCP Listener](../runtime/tcp-listener.md)
- [UDP Listener](../runtime/udp-listener.md)
- [Packet Dispatcher](../routing/packet-dispatcher.md)
