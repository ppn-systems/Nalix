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

## Usage Guidance

`Nalix.Network` is the low-level package. Its listener types are abstract bases
that require an `IProtocol` and an `IConnectionHub`:

```csharp
public sealed class MyTcpListener : TcpListenerBase
{
    public MyTcpListener(ushort port, IProtocol protocol, IConnectionHub hub)
        : base(port, protocol, hub) { }
}

IConnectionHub hub = new ConnectionHub();
using var listener = new MyTcpListener(5000, new MyProtocol(), hub);
listener.Activate();
```

For normal server applications, prefer `Nalix.Network.Hosting`. The hosting
builder creates the concrete internal listeners, connection hub, packet dispatch,
and lifecycle orchestration for you:

```csharp
using var app = NetworkApplication.CreateBuilder()
    .AddTcp<MyProtocol>(5000)
    .Build();

await app.RunAsync();
```

## Documentation

See [Transport & Networking](https://ppn-systems.me/api/network/index) for detailed configuration options and listener lifecycles.
