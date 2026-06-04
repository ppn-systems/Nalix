# WebSocket Connection

The `WebSocketConnection` class represents an active, full-duplex WebSocket connection between the server and a client. It wraps the standard `System.Net.WebSockets.WebSocket` type and implements `IConnection` to unify it with the Nalix transport abstractions.

## Overview

A `WebSocketConnection` provides reliable transport stream adapter implementation (`WebSocketTransport`) while tracking connection metrics, handling lifecycle transitions, maintaining state attributes, and caching rate limits. It is designed to work in conjunction with the global connection registry and timing wheel systems.

### Key Features
* **Unified Connection Abstraction**: Implements `IConnection` allowing existing protocol pipelines and dispatch middleware to process WebSocket connections transparently.
* **TCP Transport Integration**: The `.TCP` property maps to a custom `WebSocketTransport` that wraps standard WebSocket framing.
* **No UDP Support**: Calling `.UDP` throws a `NotSupportedException`.
* **Zero-Allocation Context Pooling**: Utilizes internal pools (`LocalPool<T>`) for connection event arguments and processing contexts to optimize GC overhead under load.

## API Reference

### WebSocketConnection Class

```csharp
namespace Nalix.Network.Connections;

public sealed class WebSocketConnection :
    IConnection,
    IConnectionErrorTracked,
    TimingWheel.ITimeoutTrackedConnection,
    IPooledConnectContextPool
```

#### Constructors

* `public WebSocketConnection(WebSocket webSocket, IOpCodeExtractor packetClassifier, EndPoint remoteEndPoint, ILogger? logger = null)`
  Initializes a connection wrapping the specified `WebSocket` instance with the packet classifier and mapping to the provided remote IP endpoint.

#### Properties

* `public bool IsDisposed { get; }`
  Returns `true` if the connection has been closed and resources are released.

* `public bool IsUdpCreated { get => false; }`
  Returns `false` as UDP transport is unsupported over WebSockets.

* `public ISnowflake ID { get; }`
  Gets the globally unique snowflake identifier assigned to this connection session.

* `public IConnection.ITransport TCP { get; }`
  Gets the `WebSocketTransport` adapter for sending and receiving binary stream frames.

* `public IConnection.ITransport UDP { get; }`
  Throws `NotSupportedException`.

* `public INetworkEndpoint NetworkEndpoint { get; }`
  Gets the IP address and port structure representing the client remote endpoint.

* `public IObjectMap<string, object> Attributes { get; }`
  Custom key-value metadata container bound to the lifetime of the connection.

* `public ConcurrentDictionary<ushort, object> RateLimitCache { get; }`
  Per-route/per-directive cache used by rate limiting middlewares.

* `public int ErrorCount { get; }`
  Gets the cumulative count of protocol or validation errors encountered on this connection.

* `public long UpTime { get; }`
  Uptime of the connection in milliseconds.

* `public long BytesSent { get; }`
  Total bytes written to the connection stream.

* `public long BytesReceived { get; }`
  Total bytes read from the connection stream.

* `public long LastPingTime { get; set; }`
  Timestamp of the last received protocol heartbeat in milliseconds.

* `public PermissionLevel Level { get; set; }`
  The authorization/permission level assigned to this connection (e.g. standard user, admin).

* `public CipherSuiteType Algorithm { get; set; }`
  The current cryptographic transform used for secure frames.

* `public Bytes32 Secret { get; set; }`
  The shared symmetric key negotiated during the connection handshake.

#### Events

* `public event EventHandler<IConnectEventArgs> OnCloseEvent`
  Triggered when the connection disconnects or is explicitly closed.

* `public event EventHandler<IConnectEventArgs> OnProcessEvent`
  Triggered when a complete message frame is read and needs routing/handling.

* `public event EventHandler<IConnectEventArgs> OnPostProcessEvent`
  Triggered after processing is completed (used for statistics or post-send updates).

#### Public Methods

* `public void Disconnect(string? reason = null)`
  Closes the WebSocket connection gracefully and triggers cleanup.

* `public void IncrementErrorCount()`
  Increments the connection error counter. If the value exceeds the maximum threshold, the connection is aborted automatically.

* `public void Dispose()`
  Disposes underlying streams, locks, and returns pooled argument context classes.

## Internal Transport Details

WebSocket communication is frameless from the application's perspective, as the WebSocket protocol itself provides frame borders.

* **Synchronous Operations Unsupported**: Synchronous methods `Send(IPacket)` and `Send(ReadOnlySpan<byte>)` throw `NotSupportedException`. You must use the asynchronous equivalents (`SendAsync`).
* **Receive Loop Buffer**: The receive loop uses a rented buffer matching `FragmentOptions.MaxChunkSize`. For large frames exceeding this size, it switches to a slow path (`HANDLE_LARGE_MESSAGE_ASYNC`) which assembles the payload into a rented array up to `NetworkWebSocketOptions.MaxMessageSize` before firing `OnProcessEvent`.

## See Also

* [WebSocket Listener](websocket-listener.md)
* [Connection Registry](connection/connection-hub.md)
* [Timing Wheel](time/timing-wheel.md)
