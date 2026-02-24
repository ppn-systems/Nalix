# NetworkHost

`NetworkHost` represents a running Nalix server instance. It coordinates the startup, operation, and graceful shutdown of all listeners, protocols, and dispatchers.

## Source mapping

- `src/Nalix.Network.Hosting/NetworkHost.cs`

## Lifecycle

| Method | Role |
| --- | --- |
| `StartAsync` | Initializes all components, activates listeners, and starts protocol execution. |
| `StopAsync` | Deactivates listeners and disposes of protocols cleanly. |
| `RunAsync` | Starts the host and keeps the program running until cancellation. |

## Protocol Hosting

The host maintains a collection of `IProtocol` instances and `IListener` objects. When `StartAsync` is called:
1. TCP listeners (`TcpListenerBase`) are created for all TCP registrations.
2. UDP listeners (`UdpListenerBase`) are created for all UDP registrations.
3. Each listener is activated with its own background receive loop.

### TCP Server Status (Log Event 1000)
Indicates a TCP listener has successfully started.

### UDP Server Status (Log Event 1004)
Indicates a UDP listener has successfully started and is bound to the server's UDP port.

## Example Lifecycle Usage

```csharp
var host = NetworkHost.CreateBuilder()
    .AddTcp<GameProtocol>()
    .AddUdp<FastSyncProtocol>()
    .AddPackets<Program>()
    .AddHandlers<Program>()
    .Build();

// Run until CTRL+C or SIGTERM
await host.RunAsync();
```

## Related APIs

- [IActivatable](../../framework/activatable.md)
- [INetworkBuilder](./network-builder.md)
- [Add UDP Support](./network-builder.md#udp-support)
