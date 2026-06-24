# Nalix.Network

> Low-level networking primitives for high-concurrency TCP, UDP, and WebSocket applications.

## Key Features

| Feature | Description | Key Concept / Type |
| :--- | :--- | :--- |
| 📡 **TCP Listeners** | High-throughput asynchronous TCP listener foundation for custom transports. | `TcpListenerBase`, `TcpServerListener` |
| 🚀 **UDP Listeners** | Session-aware UDP listener loop with endpoint pinning and sliding window anti-replay protection. | `UdpListenerBase`, `UdpServerListener` |
| 🔗 **Connection Hub** | Central thread-safe management for active client connections with shard-aware lookups and evictions. | `ConnectionHub`, `ConnectionTerminator` |
| 🛡️ **Admission Control** | Concurrency safety gates, flood protection datagram counters, and IP blacklists. | `ConnectionGuard`, `DatagramGuard` |
| 🔌 **Protocol Base** | Pluggable protocol base classes translating low-level socket actions to high-level protocol messages. | `Protocol` |

## Key Namespaces

| Namespace | Purpose | Key Types |
| :--- | :--- | :--- |
| `Nalix.Network` | Root namespace containing core network interfaces and events | `NetworkEndpoint`, `IConnectEventArgs` |
| `Nalix.Network.Listeners` | Concurrent socket listening loops and server hosts | `TcpServerListener`, `UdpServerListener`, `WebSocketServerListener` |
| `Nalix.Network.Connections` | Centralized registry for all active TCP/UDP/WS connection sessions | `ConnectionHub`, `ConnectionTerminator` |
| `Nalix.Network.Protocols` | Base abstract handlers for managing transport protocol lifecycles and states | `Protocol` |
| `Nalix.Network.RateLimiting` | Security emission guards, socket flood limits, and IP address blacklists | `ConnectionGuard`, `DatagramGuard` |
| `Nalix.Network.Options` | Core network, socket acceptor, TIMING wheel, and ban store options | `NetworkSocketOptions`, `ConnectionGuardOptions`, `DatagramGuardOptions`, `ConnectionHubOptions` |

## Installation

```bash
dotnet add package Nalix.Network
```

## Quick Example: Implementing a Custom Listener

`Nalix.Network` provides high-concurrency abstract listener types (`TcpListenerBase` and `UdpListenerBase`) that manage system socket loops, connection limits, and timing wheels. To write a custom transport listener, simply extend the base class:

```csharp
using System;
using Nalix.Abstractions.Networking;
using Nalix.Network.Listeners.Tcp;

public sealed class CustomTcpListener : TcpListenerBase
{
    public CustomTcpListener(ushort port, IProtocol protocol, IConnectionHub hub)
        : base(port, protocol, hub)
    {
    }

    // Must implement the abstract ProcessFrame method to handle incoming frames
    public override void ProcessFrame(object? sender, IConnectEventArgs args)
    {
        // Custom logic to process raw connection frames before protocol dispatch
    }
}
```

## Quick Example: Managing Connections with ConnectionHub

The `ConnectionHub` manages all concurrent active client connections. You can use it to fetch connections, monitor active counts, force evictions, or broadcast real-time payloads asynchronously:

```csharp
using System;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking;
using Nalix.Network.Connections;

// Initialize the ConnectionHub
IConnectionHub hub = new ConnectionHub();

// Check the active connection count
int activeCount = hub.Count;
Console.WriteLine($"Active connections: {activeCount}");

// Retrieve a connection by its 64-bit Snowflake ID
IConnection? client = hub.GetConnection(1234567890UL);
if (client is not null)
{
    Console.WriteLine($"Found client: {client.ID} (IP: {client.NetworkEndpoint.Address})");
}

// Broadcast a system-wide broadcast message to all active TCP clients
await hub.BroadcastAsync(
    new SystemNotice { Text = "Server is undergoing maintenance in 5 minutes." },
    async (conn, msg) => await conn.TCP.SendAsync(msg));

// Evict/force close connections originating from a malicious IP address
IConnectionTerminator terminator = new ConnectionTerminator(hub);
int closedConnections = terminator.CloseByEndpoint(new NetworkEndpoint("192.168.1.100", 0));
Console.WriteLine($"Force closed {closedConnections} connections.");
```

## Documentation

For deep technical details on listeners, session persistence, and admission guard limits, see the [Transport & Networking Guide](https://ppn.io.vn/api/network/index).
