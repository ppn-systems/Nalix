# Nalix.SDK

> Client-side transport sessions and request helpers for Nalix applications.

## Key Features

| Feature | Description | Key Concept / Type |
| :--- | :--- | :--- |
| 🔗 **Transport Sessions** | Shared session abstraction plus high-performance TCP, UDP, and WebSocket client transports. | `TransportSession`, `TcpSession`, `UdpSession`, `WebSocketSession` |
| 🔄 **Request / Response** | Race-safe, correlated typed request/response matching with timeouts and retries. | `RequestAsync<TResponse>`, `RequestOptions` |
| 🤝 **Handshake / Resume** | High-security X25519 handshakes and fast session resumption helpers. | `ConnectAsync`, `ResumeAsync` |
| 🔐 **Cipher Updates** | Dynamic runtime symmetric encryption cipher switching and rotation. | `UpdateCipherAsync` |
| 📡 **Typed Subscriptions** | Highly performant typed packet subscription handlers with automatic dispatch. | `On<TPacket>()`, `OnExact<TPacket>()` |

## Key Namespaces

| Namespace | Purpose | Key Types |
| :--- | :--- | :--- |
| `Nalix.SDK` | Root namespace containing thread dispatchers and time synchronization calculators | `IThreadDispatcher`, `InlineDispatcher`, `TimeSyncCalculator` |
| `Nalix.SDK.Transport` | Core client transport sessions supporting TCP, UDP, and WebSockets | `TransportSession`, `TcpSession`, `UdpSession`, `WebSocketSession` |
| `Nalix.SDK.Transport.Extensions` | Fluent APIs for handshakes, request/response, session resumption, and ciphers | `RequestExtensions`, `HandshakeExtensions`, `ResumeExtensions`, `CipherExtensions` |
| `Nalix.SDK.Transport.Internal` | High-efficiency transport frame readers, frame senders, and packet correlation | `PacketAwaiter`, `TcpFrameReader`, `UdpFrameReader`, `WsFrameReader` |
| `Nalix.SDK.Options` | Client socket transport and request timeout settings configuration | `TransportOptions`, `WebSocketTransportOptions`, `RequestOptions` |
| `Nalix.SDK.Extensions` | General helper extensions and subscription utilities | `SubscriptionExtensions` |

## Installation

```bash
dotnet add package Nalix.SDK
```

## Quick Example: Sending a Request

```csharp
using System;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

// Initialize a TCP session using the client options
await using TcpSession session = new(options);
await session.ConnectAsync(options.Address, options.Port);

// Send a request and wait for a response of a specific type
MyResponse response = await session.RequestAsync<MyResponse>(
    new MyRequest { Id = 1 },
    RequestOptions.Default.WithTimeout(5_000));

Console.WriteLine(response.Data);
```

## Documentation

See [Nalix.SDK](https://ppn.io.vn/packages/nalix-sdk/) for the source-mapped package reference.
