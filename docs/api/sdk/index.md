# SDK Overview

`Nalix.SDK` is the client transport package for Nalix. It provides session lifecycle APIs (`TransportSession`, `TcpSession`, `UdpSession`), request/response helpers, handshake and resume extensions, and subscription helpers.

## Why This Package Exists

Client concerns differ from server runtime concerns. `Nalix.SDK` gives applications a stable client-facing API while keeping server execution details in `Nalix.Runtime` and `Nalix.Network`.

## Core API Areas

### Session Types

- [TransportSession](./transport-session.md): abstract base contract.
- [TcpSession](./tcp-session.md): stream transport over TCP.
- [UdpSession](./udp-session.md): datagram transport over UDP.

### Extensions

- [Handshake Extensions](./handshake-extensions.md): `HandshakeAsync` for secure session setup.
- [Resume Extensions](./resume-extensions.md): session resume flow.
- [Session Extensions](./tcp-session-extensions.md): control/request/session helpers.
- [Cipher Extensions](./cipher-extensions.md): cipher update helpers.
- [Protocol String Extensions](./protocol-string-extensions.md): protocol-to-string helpers.
- [Subscriptions](./subscriptions.md): typed packet subscription APIs.

### Options

- [TransportOptions](./options/transport-options.md)
- [RequestOptions](./options/request-options.md)

### Dispatch Integration

- [Thread Dispatching](./thread-dispatching.md): `IThreadDispatcher` and `InlineDispatcher`.
- [Frame Reader and Sender](./frame-reader-and-sender.md): frame I/O internals exposed by SDK docs.

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
