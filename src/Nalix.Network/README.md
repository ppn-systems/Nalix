# Nalix.Network

> Low-level networking primitives for high-concurrency TCP and UDP applications.

## Key Features

| Feature | Description |
| :--- | :--- |
| 📡 **TCP listener base** | High-throughput asynchronous TCP listener foundation for custom transports. |
| 🚀 **UDP listener base** | Session-aware UDP listener foundation with token lookup, endpoint pinning, and replay checks. |
| 🔗 **ConnectionHub** | Central management for active sessions with shard-aware lookup and reporting. |
| 💾 **Session Store** | Built-in in-memory session persistence with support for custom stores. |
| 🛡️ **Admission Control** | Native support for connection limits, datagram guards, and IP-based protection. |
| 🔌 **Protocols** | Pluggable protocol bridge that translates transport events into packet dispatch. |

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

For deep technical details on listeners, session persistence, and admission guard limits, see the [Transport & Networking Guide](https://ppn-system.me/api/network/index).
