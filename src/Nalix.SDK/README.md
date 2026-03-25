# Nalix.SDK

> Client-side transport sessions, request helpers, and bootstrap defaults for Nalix applications.

## Key Features

| Feature | Source | Description |
| :--- | :--- | :--- |
| 🔗 **Transport Sessions** | `Transport/TransportSession.cs`, `TcpSession.cs`, `UdpSession.cs` | Shared session abstraction plus TCP and UDP client transports. |
| 🔄 **Request / Response** | `Transport/Extensions/RequestExtensions.cs` | Race-safe typed request/response with timeout and retry options. |
| 🤝 **Handshake / Resume** | `Transport/Extensions/HandshakeExtensions.cs`, `ResumeExtensions.cs` | X25519 handshake and optional session resume helpers. |
| 🔐 **Cipher Updates** | `Transport/Extensions/CipherExtensions.cs` | Runtime cipher switching for TCP sessions. |
| 📡 **Typed Subscriptions** | `Transport/Extensions/TcpSessionSubscriptions.cs` | `On<TPacket>()` and `OnExact<TPacket>()` packet subscription helpers. |
| ⚙️ **Client Bootstrap** | `Bootstrap.cs` | Uses `client.ini`, disables packet pooling, initializes `TransportOptions`, and flushes defaults. |

## Installation

```bash
dotnet add package Nalix.SDK
```

## Bootstrap Defaults

Loading the assembly invokes `Bootstrap.Initialize()` automatically through a module initializer. Source defaults include:

- configuration file: `client.ini`
- `PacketOptions.EnablePooling = false`
- `TransportOptions.Address = "127.0.0.1"`
- `TransportOptions.Port = 57206`
- `TransportOptions.EncryptionEnabled = true`
- `TransportOptions.CompressionEnabled = true`

## Quick Example: Sending a Request

```csharp
using Nalix.SDK.Options;
using Nalix.SDK.Transport.Extensions;

await using TcpSession session = new(options, registry);
await session.ConnectAsync(options.Address, options.Port);

MyResponse response = await session.RequestAsync<MyResponse>(
    new MyRequest { Id = 1 },
    RequestOptions.Default.WithTimeout(5_000));

Console.WriteLine(response.Data);
```

## Documentation

See [Nalix.SDK](https://ppn-systems.me/packages/nalix-sdk/) for the source-mapped package reference.
