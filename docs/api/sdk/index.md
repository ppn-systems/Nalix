# SDK Overview

`Nalix.SDK` is the client transport package for Nalix. It provides session lifecycle APIs (`TransportSession`, `TcpSession`, `UdpSession`), request/response helpers, handshake and resume extensions, and subscription helpers.

!!! important "Client-side package"
    `Nalix.SDK` is for client applications only. Server projects should not create `TcpSession`, `UdpSession`, or SDK extension helpers. Server-side code should use `Nalix.Network`, `Nalix.Network.Hosting`, and `Nalix.Runtime` listener/runtime APIs instead.

## Source Mapping

- `src/Nalix.SDK/Transport`
- `src/Nalix.SDK/Transport/Extensions`
- `src/Nalix.SDK/Extensions`
- `src/Nalix.SDK/Options`
- `src/Nalix.SDK/InlineDispatcher.cs`
- `src/Nalix.SDK/IThreadDispatcher.cs`

## Why This Package Exists

Client concerns differ from server runtime concerns. `Nalix.SDK` gives applications a stable client-facing API while keeping server execution details in `Nalix.Runtime` and `Nalix.Network`.

## Core API Areas

### High-level APIs

Use these pages when writing application/client code:

- [TcpSession](./tcp-session.md): stream transport over TCP.
- [UdpSession](./udp-session.md): datagram transport over UDP.
- [Handshake Extensions](./handshake-extensions.md): `HandshakeAsync` for secure session setup.
- [Resume Extensions](./resume-extensions.md): session resume flow.
- [Session Extensions](./tcp-session-extensions.md): aggregate entry point for request, control, cipher, and subscription helpers.
- [Cipher Extensions](./cipher-extensions.md): live cipher update helpers.
- [Control Utilities](./control-utilities.md): `PingAsync`, `DisconnectGracefullyAsync`, and `SyncTimeAsync` convenience helpers.
- [Session Diagnostics](./diagnostics.md): session diagnostics and observable runtime state.
- [Subscriptions](./subscriptions.md): typed packet subscription APIs.

### Low-level primitives

Use these pages when working on SDK internals, custom transports, or protocol tooling:

- [Thread Dispatching](./thread-dispatching.md): `IThreadDispatcher` and `InlineDispatcher`.
- [Frame Reader and Sender](./frame-reader-and-sender.md): TCP frame parsing, send serialization, and fragmentation internals.
- [TransportSession](./transport-session.md): abstract base contract behind concrete sessions.
- [Protocol String Extensions](./protocol-string-extensions.md): protocol-to-string helpers from `src/Nalix.SDK/Extensions`.

### Options

- [TransportOptions](./options/transport-options.md)
- [RequestOptions](./options/request-options.md)

## Mental Model

1. Configure transport with `TransportOptions` and packet registry.
2. Connect with `TcpSession` or `UdpSession`.
3. Optionally perform `HandshakeAsync`.
4. Send packets directly or use `RequestAsync<TResponse>`.
5. Receive via event/subscription APIs.

## Practical Example (From Current API)

```csharp
TransportOptions options = new();
IPacketRegistry catalog = /* resolve registry */;

using TcpSession session = new(options, catalog);
await session.ConnectAsync();

await session.HandshakeAsync();

MyResponse response = await session.RequestAsync<MyResponse>(
    new MyRequest(),
    RequestOptions.Default.WithTimeout(3_000).WithRetry(1));
```

## Best Practices

- Prefer `RequestAsync<TResponse>` over manual subscribe/send/wait to avoid response race windows.
- Handle `OnError` and `OnDisconnected` for production resilience.
- Keep packet registry consistent between client and server packet contracts.

## Related APIs

- [Packet Contracts](../common/packet-contracts.md)
- [Runtime Routing](../runtime/routing/index.md)
- [Network Protocol](../network/protocol.md)
