# Security Basics

Nalix encrypts a connection through a handshake, not through configuration you write by hand. Turn it on with one call and the framework negotiates a shared key per connection.

## Turning it on

```csharp
// samples/SecureMultiTransportHelloWorld/SecureMultiTransportHelloWorld.Server/Program.cs
await using NetworkApplication app = NetworkApplication.CreateBuilder()
    .UseLogger(logger)
    .UseSecureConnections()
    .MapHandlers(typeof(HelloHandlers))
    .ListenTcp<DefaultProtocol>().OnPort(TcpPort).Bind()
    .Build();
```

`UseSecureConnections()` sets up the handshake handlers and generates a certificate the first time you run the server, if one doesn't already exist. Every request is rejected until the client completes the handshake.

Full source: `samples/SecureMultiTransportHelloWorld/SecureMultiTransportHelloWorld.Server/Program.cs`

## The client handshakes once

```csharp
// samples/SecureMultiTransportHelloWorld/SecureMultiTransportHelloWorld.Client/Program.cs
using TcpSession tcpSession = new(tcpOptions, sharedState);
await tcpSession.ConnectAsync(Host, TcpPort);
await tcpSession.HandshakeAsync();
```

`HandshakeAsync()` negotiates a shared secret and issues a session token. From this point, TCP traffic on this connection is encrypted automatically — you don't call any encrypt/decrypt function yourself.

Full source: `samples/SecureMultiTransportHelloWorld/SecureMultiTransportHelloWorld.Client/Program.cs`

## Restricting who can call a handler

Declare permission, timeout, and rate-limit rules directly on a handler method:

```csharp
[PacketOpcode(0x2001)]
[PacketPermission(PermissionLevel.USER)]
[PacketTimeout(5000)]
[PacketRateLimit(requestsPerSecond: 10)]
public ValueTask<AccountResponse> GetProfile(IPacketContext<ProfileRequest> context)
{
    // Only runs if permission, timeout, and rate limit checks pass
}
```

These are enforced by built-in middleware before your handler body runs.

## UDP reuses the TCP handshake

UDP never runs its own handshake. It trusts the session token and secret negotiated over TCP, and authenticates every datagram against them — see [Securing Your Server](../guides/securing-your-server.md) for the full flow.

## Next steps

- [Securing Your Server](../guides/securing-your-server.md) — the full walkthrough with TCP, UDP, and WebSocket
- [Handlers and Middleware](./handlers-and-middleware.md) — how permission/rate-limit attributes get enforced
- For the handshake protocol, encryption model, and session resume details: [Security Architecture](./internals/security-architecture.md) (Internals)
