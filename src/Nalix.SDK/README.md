# Nalix.SDK

> Client-side transport sessions and request helpers for Nalix applications.

## Key Features

| Feature | Source | Description |
| :--- | :--- | :--- |
| 🔗 **Transport Sessions** | `Transport/TransportSession.cs`, `TcpSession.cs`, `UdpSession.cs` | Shared session abstraction plus TCP and UDP client transports. |
| 🔄 **Request / Response** | `Transport/Extensions/RequestExtensions.cs` | Race-safe typed request/response with timeout and retry options. |
| 🤝 **Handshake / Resume** | `Transport/Extensions/HandshakeExtensions.cs`, `ResumeExtensions.cs` | X25519 handshake and optional session resume helpers. |
| 🔐 **Cipher Updates** | `Transport/Extensions/CipherExtensions.cs` | Runtime cipher switching for TCP sessions. |
| 📡 **Typed Subscriptions** | `Transport/Extensions/TcpSessionSubscriptions.cs` | `On<TPacket>()` and `OnExact<TPacket>()` packet subscription helpers. |

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

See [Nalix.SDK](https://ppn-system.me/packages/nalix-sdk/) for the source-mapped package reference.
