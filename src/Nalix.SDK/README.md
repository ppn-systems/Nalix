# Nalix.SDK

A high-level client SDK for building Nalix-compatible client applications with ease.

## Features

- **TcpSession / UdpSession**: Managed session life cycles with automatic reconnection.
- **Request / Response**: Correlated request-response pattern out-of-the-box.
- **Buffer Management**: Transparently handles buffer pooling for outgoing and incoming packets.
- **Encryption Support**: Built-in support for encrypted packet flows.

## Installation

```bash
dotnet add package Nalix.SDK
```

## Quick Example: Sending a Request

```csharp
await using var session = new TcpSession(options, registry);
await session.ConnectAsync();

var response = await session.RequestAsync<MyResponse>(new MyRequest { Id = 1 });
Console.WriteLine(response.Data);
```

## Documentation

Check the [Client Guide](https://ppn-systems.me/api/sdk/index) for session configuration and error handling.
