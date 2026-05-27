# WebSocket Transport Options (SDK)

The `WebSocketTransportOptions` class configures the client-side WebSocket transport used by the `WebSocketSession`.

## Source Mapping

- `src/Nalix.SDK/Options/WebSocketTransportOptions.cs`

## Overview

These settings manage the endpoint path, subprotocol handshakes, TLS usage, and message size constraints on the client.

## Configuration Table

The options map to INI configuration sections under client properties:

| Property | Type | Default Value | Description |
|----------|------|---------------|-------------|
| `Path` | `string` | `"/ws/"` | The HTTP path prefix used for the WebSocket connection endpoint. |
| `SubProtocol` | `string` | `"nalix.v1"` | The WebSocket subprotocol header negotiated with the server. |
| `UseTls` | `bool` | `false` | If set to `true`, the connection scheme is set to `wss://` (WebSocket over TLS); otherwise it is `ws://`. |
| `MaxMessageSize` | `int` | `1,048_576` (1 MB) | The maximum allowed size (in bytes) of an incoming WebSocket message payload before triggering a size violation. |

## Usage Example

### Mutating Options Programmatically

```csharp
using Nalix.SDK.Options;
using Nalix.SDK.Transport;

var transportOptions = new TransportOptions
{
    Address = "127.0.0.1",
    Port = 8080
};

var wsOptions = new WebSocketTransportOptions
{
    Path = "/custom-ws",
    SubProtocol = "myprotocol.v1",
    UseTls = true,
    MaxMessageSize = 512_000 // 512 KB limit
};

wsOptions.Validate();

using var session = new WebSocketSession(transportOptions, wsOptions);
```

### INI Configuration Format

```ini
[WebSocketTransport]
; Client WebSocket transport configuration
Path = /ws/
SubProtocol = nalix.v1
UseTls = false
MaxMessageSize = 1048576
```

## See Also

* [WebSocket Session (SDK)](../../sdk/websocket-session.md)
