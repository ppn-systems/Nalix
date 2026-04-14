# Nalix.SDK

> High-level client SDK for building Nalix-compatible client applications with ease.

## Key Features

| Feature | Description |
| :--- | :--- |
| 🔗 **TcpSession / UdpSession** | Managed session lifecycles with automatic reconnection. |
| 🔄 **Request / Response** | Correlated request-response pattern via `RequestAsync<TResponse>()`. |
| 🧠 **Buffer Management** | Transparent buffer pooling for outgoing and incoming packets. |
| 🔐 **Encryption Support** | Built-in support for encrypted packet flows. |
| 🤝 **Session Extensions** | Handshake, control, cipher switching, and resume helpers on live TCP sessions. |
| 📡 **Typed Subscriptions** | Safe `On<T>()` subscriptions; use `OnExact<T>()` only for single-type channels. |

## Installation

```bash
dotnet add package Nalix.SDK
```

## Quick Example: Sending a Request

```csharp
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

await using var session = new TcpSession(options, registry);
await session.ConnectAsync();

var response = await session.RequestAsync<MyResponse>(
    new MyRequest { Id = 1 },
    options: RequestOptions.Default.WithTimeout(5_000));

Console.WriteLine(response.Data);
```

## Common Patterns

| Pattern | Usage |
| :--- | :--- |
| Initial handshake | `await session.HandshakeAsync()` |
| Session resumption | `await session.ResumeSessionAsync()` |
| Cipher rotation | `await session.UpdateCipherAsync(...)` |
| Packet listener | `session.On<TPacket>(handler)` |
| Debug-only listener | `session.OnExact<TPacket>(handler)` |

## Documentation

Check the [SDK API docs](https://ppn-systems.me/api/sdk/index) for session configuration, request options, and error handling.
