# WebSocket Session (SDK)

The `WebSocketSession` class provides a client-side SDK abstraction for establishing, managing, and exchanging messages over a WebSocket connection. It inherits from the SDK base `TransportSession` class.

## Overview

`WebSocketSession` manages the connection lifecycle for client applications, providing full-duplex messaging with support for both synchronous and asynchronous message processing. Under the hood, it utilizes `WsFrameReader` and `WsFrameSender` for stream reading and writing.

### Key Features
* **Native Framing**: Relies on WebSocket protocol framing.
* **Automatic Registry Loading**: Automatically ensures the global `PacketRegistry` is built upon connection.
* **Asynchronous Message Dispatch**: Exposes a callback interface `OnMessageAsync` to handle inbound packet memory ranges asynchronously without extra allocations.
* **Configuration Flexibility**: Backed by SDK `TransportOptions` and WebSocket-specific configurations through `WebSocketTransportOptions`.

## API Reference

### WebSocketSession Class

```csharp
namespace Nalix.SDK.Transport;

public class WebSocketSession : TransportSession
```

#### Constructors

* `public WebSocketSession(TransportOptions options, WebSocketTransportOptions? webSocketOptions = null)`  
  Initializes a new session instance with global transport options and optional WebSocket-specific overrides.

#### Properties

* `public override TransportOptions Options { get; }`  
  Gets the general transport options assigned to this session.

* `public WebSocketTransportOptions WebSocketOptions { get; }`  
  Gets the WebSocket-specific options (e.g. SubProtocol, Path, TLS setting).

* `public override bool IsConnected { get; }`  
  Returns `true` if the underlying socket is open and the session has not been disposed.

#### Events

* `public override event EventHandler? OnConnected`  
  Triggered when the WebSocket connection is successfully established.

* `public override event EventHandler<Exception>? OnDisconnected`  
  Triggered when the session is disconnected.

* `public override event EventHandler<IBufferLease>? OnMessageReceived`  
  Triggered when a raw message buffer lease is received.

* `public override event EventHandler<Exception>? OnError`  
  Triggered when a transport, serialization, or connection exception is encountered.

* `public event Func<ReadOnlyMemory<byte>, Task>? OnMessageAsync`  
  Asynchronous handler invoked when a complete message is received. Useful for zero-allocation asynchronous pipelines.

#### Public Methods

* `public override async Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default)`  
  Asynchronously connects to the remote WebSocket server (e.g. `ws://` or `wss://` URI depending on options). Automatically builds the `PacketRegistry` if not already initialized.

* `public override async Task DisconnectAsync()`  
  Initiates a graceful close handshake and releases the client socket.

* `public void Send(ReadOnlySpan<byte> data, bool encrypt = true)`  
  Sends raw binary data synchronously over the WebSocket.

* `public override async Task SendAsync(IPacket packet, CancellationToken ct = default)`  
  Asynchronously serializes and sends a packet, defaulting to encryption flags specified in `Options`.

* `public override async Task SendAsync(IPacket packet, bool? encrypt = null, CancellationToken ct = default)`  
  Asynchronously serializes and sends a packet with explicit encryption behavior. Automatically enforces the `PacketFlags.RELIABLE` flag on the packet header.

* `public override async Task SendAsync(ReadOnlyMemory<byte> payload, bool? encrypt = null, CancellationToken ct = default)`  
  Asynchronously sends raw binary payload memory.

## Usage Example

```csharp
using System;
using System.Text;
using System.Threading.Tasks;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;

var options = new TransportOptions
{
    Address = "127.0.0.1",
    Port = 8080,
    ConnectTimeoutMillis = 5000,
    EncryptionEnabled = true
};

var wsOptions = new WebSocketTransportOptions
{
    Path = "/ws",
    UseTls = false
};

using var session = new WebSocketSession(options, wsOptions);

session.OnConnected += (s, e) => Console.WriteLine("Connected!");
session.OnDisconnected += (s, ex) => Console.WriteLine($"Disconnected: {ex.Message}");
session.OnError += (s, ex) => Console.WriteLine($"Error: {ex.Message}");

session.OnMessageAsync += async (memory) =>
{
    string text = Encoding.UTF8.GetString(memory.Span);
    Console.WriteLine($"Received message: {text}");
    await Task.CompletedTask;
};

await session.ConnectAsync();

// Send an asynchronous payload
byte[] data = Encoding.UTF8.GetBytes("Hello, Server!");
await session.SendAsync(data);

await Task.Delay(1000);
await session.DisconnectAsync();
```

## See Also

* [TCP Session](tcp-session.md)
* [UDP Session](udp-session.md)
* [WebSocket Transport Options](../options/sdk/websocket-transport-options.md)
