# Nalix.Network

Low-level networking primitives for high-concurrency TCP and UDP applications.

## Features

- **TcpServerListener**: High-throughput TCP listener using asynchronous socket patterns.
- **UdpServerListener**: Session-aware UDP listener for low-latency communications.
- **Connection Hub**: Central management for thousands of active sessions.
- **Session Store**: Built-in session persistence and TTL-based resumable session retention.
- **Session Admission Control**: Native support for IP filtering and connection limits.
- **Protocols**: Pluggable protocol bridge to translate raw streams into packet contexts.

## Installation

```bash
dotnet add package Nalix.Network
```

## Quick Example: TCP Listener

```csharp
var protocol = new MyProtocol(); // Inherit from Protocol
var listener = new TcpServerListener(5000, protocol);
listener.Activate();
```

## Documentation

See [Transport & Networking](https://ppn-systems.me/api/network/index) for detailed configuration options and listener life cycles.
