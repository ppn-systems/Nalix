# Nalix.SDK

A high-level client SDK for building Nalix-compatible client applications with ease.

## Features

- **TcpSession / UdpSession**: Managed session life cycles with automatic reconnection.
- **Request / Response**: Correlated request-response pattern out-of-the-box via `RequestAsync<TResponse>()`.
- **Buffer Management**: Transparently handles buffer pooling for outgoing and incoming packets.
- **Encryption Support**: Built-in support for encrypted packet flows.
- **Session Extensions**: Handshake helpers, control helpers, cipher switching, and resume helpers on live TCP sessions.
- **Typed Subscriptions**: Safe `On<T>()` subscriptions ignore non-matching packets; use `OnExact<T>()` only when you truly expect one packet type on the channel.

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

- `await session.HandshakeAsync()` for the initial secure handshake.
- `await session.ResumeSessionAsync()` to attempt session resumption before a fresh handshake.
- `await session.UpdateCipherAsync(...)` to rotate the cipher mid-connection.
- `session.On<TPacket>(handler)` for long-lived packet listeners.
- `session.OnExact<TPacket>(handler)` only for debugging or single-type channels.

## Documentation

Check the [SDK API docs](https://ppn-systems.me/api/sdk/index) for session configuration, request options, and error handling.
