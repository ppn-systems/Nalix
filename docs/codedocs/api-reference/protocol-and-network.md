---
title: "Protocol and Network"
description: "Reference for the core transport-side Nalix types: Protocol, Connection, ConnectionHub, listeners, and network option classes."
---

These types live in `Nalix.Network` and form the server-side transport layer that sits below the runtime dispatch pipeline.

## Import Paths

```csharp
using Nalix.Network.Protocols;
using Nalix.Network.Connections;
using Nalix.Network.Listeners.Tcp;
using Nalix.Network.Listeners.Udp;
using Nalix.Network.Options;
```

## Source

- [Protocols/Protocol.Core.cs](/workspace/home/nalix/src/Nalix.Network/Protocols/Protocol.Core.cs)
- [Protocols/Protocol.PublicMethods.cs](/workspace/home/nalix/src/Nalix.Network/Protocols/Protocol.PublicMethods.cs)
- [Connections/Connection.cs](/workspace/home/nalix/src/Nalix.Network/Connections/Connection.cs)
- [Connections/Connection.Hub.cs](/workspace/home/nalix/src/Nalix.Network/Connections/Connection.Hub.cs)
- [Listeners/TcpListener/TcpListener.PublicMethods.cs](/workspace/home/nalix/src/Nalix.Network/Listeners/TcpListener/TcpListener.PublicMethods.cs)
- [Listeners/UdpListener/UdpListener.PublicMethods.cs](/workspace/home/nalix/src/Nalix.Network/Listeners/UdpListener/UdpListener.PublicMethods.cs)

## `Protocol`

```csharp
public abstract partial class Protocol : IProtocol
{
    public bool IsAccepting { get; protected set; }
    public virtual bool KeepConnectionOpen { get; protected set; }
    public abstract void ProcessMessage(object? sender, IConnectEventArgs args);
    public void PostProcessMessage(object? sender, IConnectEventArgs args);
    public void SetConnectionAcceptance(bool isEnabled);
    public virtual void OnAccept(IConnection connection, CancellationToken cancellationToken = default);
    public void Dispose();
}
```

`Protocol` is the transport bridge. In almost every real implementation, `ProcessMessage(...)` forwards the inbound `args.Lease` and `args.Connection` to `IPacketDispatch`.

Example:

```csharp
public sealed class GameProtocol : Protocol
{
    private readonly IPacketDispatch _dispatch;

    public GameProtocol(IPacketDispatch dispatch) => _dispatch = dispatch;

    public override void ProcessMessage(object? sender, IConnectEventArgs args)
        => _dispatch.HandlePacket(args.Lease!, args.Connection);
}
```

## `Connection`

```csharp
public sealed partial class Connection : IConnection, IConnectionErrorTracked
{
    public Connection(Socket socket, ILogger? logger = null);
    public bool IsDisposed { get; }
    public ISnowflake ID { get; }
    public IConnection.ITransport TCP { get; }
    public IConnection.ITransport UDP { get; }
    public INetworkEndpoint NetworkEndpoint { get; }
    public IObjectMap<string, object> Attributes { get; }
    public int ErrorCount { get; }
    public long UpTime { get; }
    public long LastPingTime { get; }
    public PermissionLevel Level { get; set; }
    public CipherSuiteType Algorithm { get; set; }
    public Bytes32 Secret { get; set; }
    public int TimeoutVersion { get; set; }
    public bool IsRegisteredInWheel { get; set; }
    public long BytesSent { get; }
    public void IncrementErrorCount();
    public event EventHandler<IConnectEventArgs> OnCloseEvent;
    public event EventHandler<IConnectEventArgs> OnProcessEvent;
    public event EventHandler<IConnectEventArgs> OnPostProcessEvent;
    public void Close(bool force = false);
    public void Disconnect(string? reason = null);
    public void Dispose();
}
```

Use `Attributes` for per-connection state and `Secret` plus `Algorithm` for connection-level encryption state.

## `ConnectionHub`

```csharp
public sealed class ConnectionHub : IConnectionHub
{
    public ConnectionHub(ISessionStore? sessionStore = null, ILogger? logger = null);
    public int Count { get; }
    public ISessionStore SessionStore { get; }
    public event Action<IConnection>? ConnectionUnregistered;
    public event CapacityLimitReachedHandler? CapacityLimitReached;
    public void RegisterConnection(IConnection connection);
    public void UnregisterConnection(IConnection connection);
    public IConnection? GetConnection(ISnowflake id);
    public IConnection? GetConnection(UInt56 id);
    public IConnection? GetConnection(ReadOnlySpan<byte> id);
    public IReadOnlyCollection<IConnection> ListConnections();
    public Task BroadcastAsync<T>(T message, Func<IConnection, T, CancellationToken, Task> sendFunc, CancellationToken cancellationToken = default);
    public Task BroadcastWhereAsync<T>(T message, Func<IConnection, bool> predicate, Func<IConnection, T, CancellationToken, Task> sendFunc, CancellationToken cancellationToken = default);
    public int ForceClose(INetworkEndpoint networkEndpoint);
    public void CloseAllConnections(string? reason = null);
    public string GenerateReport();
    public IDictionary<string, object> GetReportData();
    public void Dispose();
}
```

`ConnectionHub` is where active connections, sharding, broadcast, and session persistence come together.

Example:

```csharp
ConnectionHub hub = new();
IReadOnlyCollection<IConnection> connections = hub.ListConnections();
```

## Listener Bases

```csharp
public abstract partial class TcpListenerBase
{
    public void Activate(CancellationToken cancellationToken = default);
    public void Deactivate(CancellationToken cancellationToken = default);
    public virtual string GenerateReport();
    public virtual IDictionary<string, object> GetReportData();
}

public abstract partial class UdpListenerBase : IListener
{
    public void Activate(CancellationToken cancellationToken = default);
    public void Deactivate(CancellationToken cancellationToken = default);
    public string GenerateReport();
    public IDictionary<string, object> GetReportData();
}
```

Most application code does not instantiate these directly because `Nalix.Network.Hosting` creates internal concrete listeners. They are still useful when you need complete control over low-level transport assembly.

## Network Option Types

### `NetworkSocketOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `Port` | `ushort` | `57206` | Listener port. |
| `Backlog` | `int` | `512` | Pending connection queue length. |
| `EnableTimeout` | `bool` | `true` | Enables idle timeout behavior. |
| `EnableIPv6` | `bool` | `false` | Uses IPv6 sockets. |
| `NoDelay` | `bool` | `true` | Disables Nagle's algorithm. |
| `MaxParallel` | `int` | `5` | Parallel TCP accept workers. |
| `MaxParallelUDP` | `int` | `2` | Parallel UDP receive workers. |
| `BufferSize` | `int` | `65536` | Send and receive buffer size. |
| `KeepAlive` | `bool` | `true` | Enables TCP keepalive. |
| `ReuseAddress` | `bool` | `true` | Reuses the local address. |
| `MaxGroupConcurrency` | `int` | `8` | Socket operation group limit. |
| `DualMode` | `bool` | `true` | IPv4 plus IPv6 dual mode for IPv6 sockets. |
| `ProcessChannelCapacity` | `int` | `256` | Accepted connection queue capacity. |

### `ConnectionHubOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `MaxConnections` | `int` | `-1` | Global connection cap; `-1` means unlimited. |
| `DropPolicy` | `DropPolicy` | `DropNewest` | Behavior when capacity is reached. |
| `ParallelDisconnectDegree` | `int` | `-1` | Degree of parallelism for bulk disconnects. |
| `BroadcastBatchSize` | `int` | `0` | Broadcast batch size; `0` disables batching. |
| `ShardCount` | `int` | `Environment.ProcessorCount` | Shard count for connection lookup dictionaries. |
| `IsEnableLatency` | `bool` | `true` | Enables latency measurement. |

### `ConnectionLimitOptions`

The important properties are `MaxConnectionsPerIpAddress`, `MaxConnectionsPerWindow`, `BanDuration`, `ConnectionRateWindow`, `MaxUdpDatagramSize`, `MaxErrorThreshold`, `UdpReplayWindowSize`, and `MaxPacketPerSecond`.

### `NetworkCallbackOptions`

The important properties are `MaxPerConnectionPendingPackets`, `MaxPerConnectionOpenFragmentStreams`, `MaxPendingNormalCallbacks`, `CallbackWarningThreshold`, `MaxPendingPerIp`, `MaxPooledCallbackStates`, and `FairnessMapSize`.

## Related Types

- [Network Builder](/docs/api-reference/network-builder)
- [Dispatch Runtime](/docs/api-reference/dispatch-runtime)
