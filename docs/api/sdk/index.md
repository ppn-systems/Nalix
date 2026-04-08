# Nalix.SDK API Overview

`Nalix.SDK` is the client transport layer for Nalix-based applications. The current source tree includes an abstract transport contract, a reliable TCP session, a high-performance UDP session, and helper extensions for control packets, requests, subscriptions, thread dispatching, and protocol strings.

!!! tip "Start with TcpSession unless you have a reason not to"
    `TcpSession` is the best default for most clients because it already carries reconnect, monitoring, and helper flow that teams usually need.

## Client runtime shape

```mermaid
flowchart LR
    A["TransportOptions"] --> B["TransportSession / TcpSession"]
    B --> C["Connect / reconnect logic"]
    C --> D["Extensions or packet send path"]
    D --> E["Request matching / subscriptions"]
```

## Source mapping

- `src/Nalix.SDK/Transport/TransportSession.cs`
- `src/Nalix.SDK/Transport/TcpSession.cs`
- `src/Nalix.SDK/Transport/UdpSession.cs`
- `src/Nalix.SDK/Options/TransportOptions.cs`
- `src/Nalix.SDK/Options/RequestOptions.cs`
- `src/Nalix.SDK/Transport/Extensions/ControlExtensions.cs`
- `src/Nalix.SDK/Transport/Extensions/RequestExtensions.cs`
- `src/Nalix.SDK/Transport/Extensions/TcpSessionSubscriptions.cs`
- `src/Nalix.SDK/IThreadDispatcher.cs`
- `src/Nalix.SDK/InlineDispatcher.cs`
- `src/Nalix.SDK/Extensions/ProtocolStringExtensions.cs`

## Module summary

| Component | Description |
| --- | --- |
| `TransportSession`, `TcpSession`, `UdpSession` | Shared transport contract plus concrete TCP and UDP client implementations. |
| `TransportOptions` | Client transport configuration loaded through `ConfigurationManager`. |
| `RequestOptions` | Timeout, retry, and encryption controls for `RequestAsync`. |
| `Transport.Extensions` | Control, request, and subscription helpers. |
| `IThreadDispatcher`, `InlineDispatcher` | Minimal thread dispatch abstraction and inline implementation. |
| `ProtocolStringExtensions` | Friendly display text for `ProtocolAdvice` and `ProtocolReason`. |

## Quick start

Checklist:

- register an `IPacketRegistry`
- load or construct `TransportOptions`
- create `TransportSession`-derived `TcpSession`
- hook events you need
- connect, send, await responses, disconnect

```csharp
InstanceManager.Instance.Register<IPacketRegistry>(catalog);

TransportOptions options = ConfigurationManager.Instance.Get<TransportOptions>();

var client = new TcpSession();
client.OnConnected += (_, _) => { };
client.OnDisconnected += (_, ex) => { };

await client.ConnectAsync(options.Address, options.Port);
await client.SendAsync(myPacket);
await client.DisconnectAsync();
client.Dispose();
```

## What changed in the current runtime

Compared with older docs/examples, the current SDK shape is:

- TCP-first and centered on a single concrete client transport
- reconnect-aware through `TransportOptions`
- request-safe through `PACKET_AWAITER`-backed helpers
- able to handle control and request flows without hand-written boilerplate

## ProtocolStringExtensions

`ProtocolStringExtensions` converts low-level protocol enums into short user-facing strings.

## Source mapping

- `src/Nalix.SDK/Extensions/ProtocolStringExtensions.cs`

It currently adds:

- `ProtocolAdvice.ToString()`
- `ProtocolReason.ToString()`

This API is mainly useful for:

- client UI messages
- toast/error text
- logs that should stay readable without raw enum names

## Example

```csharp
string message = ProtocolReason.RATE_LIMITED.ToString();
string action = ProtocolAdvice.BACKOFF_RETRY.ToString();
```

Use the detail pages next:

- [TCP Session](./tcp-session.md)
- [UDP Session](./udp-session.md)
- [Transport Session](./transport-session.md)
- [Frame Reader and Sender](./frame-reader-and-sender.md)
- [TCP Session Extensions](./tcp-session-extensions.md)
- [Session Diagnostics](./diagnostics.md)
- [Thread Dispatching](./thread-dispatching.md)
- [Protocol String Extensions](./protocol-string-extensions.md)

## Related APIs

- [TCP Session](./tcp-session.md)
- [UDP Session](./udp-session.md)
- [Transport Session](./transport-session.md)
- [Frame Reader and Sender](./frame-reader-and-sender.md)
- [TCP Session Extensions](./tcp-session-extensions.md)
- [Session Diagnostics](./diagnostics.md)
- [Thread Dispatching](./thread-dispatching.md)
- [Protocol String Extensions](./protocol-string-extensions.md)
- [Subscriptions](./subscriptions.md)
- [Transport Options](./options/transport-options.md)
- [Request Options](./options/request-options.md)
