# WebSocket Options

The `NetworkWebSocketOptions` class represents configuration settings for the hosted server-side WebSocket listeners.

## Overview

These settings manage binding properties (port, host, path), subprotocol handshakes, channel queues, timeouts, and maximum payload constraints.

## Configuration Table

The options map to INI configuration sections:

| Property | Type | Default Value | Description |
|----------|------|---------------|-------------|
| `Port` | `ushort` | `57207` | The local port number the WebSocket listener binds to. Range: 1 to 65535. |
| `Path` | `string` | `"/ws/"` | The URL path prefix mapped to the WebSocket upgrade handler. |
| `Host` | `string` | `"*"` | The host prefix address to listen on (e.g. `*` for all interfaces, `+`, or `localhost`). |
| `SubProtocol` | `string` | `"nalix.v1"` | The server-side subprotocol verified during WebSocket negotiation. |
| `EnableTimeout` | `bool` | `true` | If `true`, enables connection idle timeout tracking inside the timing wheel. |
| `ProcessChannelDrainTimeout` | `int` | `5000` (ms) | Time in milliseconds to wait for the internal processing channel queue to drain during listener deactivation. |
| `ProcessChannelCapacity` | `int` | `256` | Bounded capacity of the pending connection channel. Reaching this capacity will drop/throttle new handshakes. |
| `MaxMessageSize` | `int` | `1,048_576` (1 MB) | The maximum permitted size of a single inbound WebSocket frame payload in bytes. |

## Usage Example

### Mutating Options Programmatically

```csharp
using Nalix.Hosting;
using Nalix.Network.Options;

INetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();

builder.Configure<NetworkWebSocketOptions>(options =>
{
    options.Port = 8080;
    options.Path = "/socket";
    options.Host = "127.0.0.1";
    options.EnableTimeout = true;
    options.ProcessChannelCapacity = 512;
    options.MaxMessageSize = 2_097_152; // 2 MB
});
```

### INI Configuration Format

```ini
[NetworkWebSocket]
; Network WebSocket configuration — controls endpoint, subprotocol, and behavior
Port = 57207
Path = /ws/
Host = *
SubProtocol = nalix.v1
EnableTimeout = true
ProcessChannelDrainTimeout = 5000
ProcessChannelCapacity = 256
MaxMessageSize = 1048576
```

## See Also

* [WebSocket Listener](../../network/websocket-listener.md)
* [WebSocket Connection](../../network/websocket-connection.md)
