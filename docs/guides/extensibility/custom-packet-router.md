# Custom Packet Routing & Shard Affinity

This guide provides actionable instructions for implementing a custom packet router that leverages Nalix's shard-aware dispatch system to maintain strict sequential processing for logically related connections (e.g., User, Room, or GameSession).

## 1. Core Concept: Shard-Awareness

Nalix uses **Hashed Connection Affinity**. The `DispatchChannel<T>` (internal to `PacketDispatchChannel`) assigns each `IConnection` instance to a specific internal shard (Worker Loop) using `RuntimeHelpers.GetHashCode(connection)`.

To ensure that different physical connections (e.g., a player connected from both a Phone and Tablet) are processed sequentially by the same worker, you must use a **Shared Shard Proxy**.

## 2. Step 1: Implement a Shard Proxy

A Shard Proxy is a wrapper for `IConnection` that provides a stable object identity for the dispatcher while delegating transmission back to the physical socket.

!!! essential "Object Identity is Key"
    The dispatcher uses the **Reference Identity** of the `IConnection` object. You **must** return the exact same instance of your proxy for all connections that belong to the same shard.

```csharp
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Common.Primitives;
using Nalix.Common.Security;
using Nalix.Common.Abstractions;

public sealed class UserShardProxy : IConnection
{
    private readonly IConnection _physical;
    public long UserID { get; }

    public UserShardProxy(long userId, IConnection physical)
    {
        UserID = userId;
        _physical = physical;
    }

    // --- IDENTITY MEMBERS ---
    // These are what the Dispatcher uses for sharding and identification.
    public ISnowflake ID => _physical.ID;
    public INetworkEndpoint NetworkEndpoint => _physical.NetworkEndpoint;

    // --- TRANSMISSION DELEGATION ---
    // Ensure that handlers can still send data back to the physical connection.
    public IConnection.ITransport TCP => _physical.TCP;
    public IConnection.ITransport UDP => _physical.UDP;

    // --- STATE DELEGATION ---
    public IObjectMap<string, object> Attributes => _physical.Attributes;
    public PermissionLevel Level { get => _physical.Level; set => _physical.Level = value; }
    public CipherSuiteType Algorithm { get => _physical.Algorithm; set => _physical.Algorithm = value; }
    public Bytes32 Secret { get => _physical.Secret; set => _physical.Secret = value; }
    
    // --- ERROR TRACKING ---
    public int ErrorCount => _physical.ErrorCount;
    public void IncrementErrorCount() => _physical.IncrementErrorCount();
    public void ResetErrorCount() => _physical.ResetErrorCount();

    // --- LIFECYCLE ---
    public bool IsDisposed => _physical.IsDisposed;
    public void Close(bool force = false) => _physical.Close(force);
    public void Disconnect(string? reason = null) => _physical.Disconnect(reason);
    public void Dispose() => _physical.Dispose();

    // Event delegation...
    public event EventHandler<IConnectEventArgs> OnCloseEvent { add => _physical.OnCloseEvent += value; remove => _physical.OnCloseEvent -= value; }
    // ... (delegate other events similarly)
}
```

## 3. Step 2: Implement the Router Middleware

Implement the `IPacketDispatch` interface to intercept incoming packets and resolve the Shard Proxy before passing them to the standard dispatch channel.

```csharp
using Nalix.Runtime.Dispatching;
using Nalix.Common.Networking;
using Nalix.Framework.Memory.Buffers;

public class UserBasedRouter : IPacketDispatch
{
    private readonly IPacketDispatch _inner; // The default PacketDispatchChannel

    public UserBasedRouter(IPacketDispatch inner)
    {
        _inner = inner;
    }

    public void HandlePacket(IBufferLease packet, IConnection connection)
    {
        // 1. Resolve logical identity (e.g., UserID stored in connection attributes)
        if (connection.Attributes.TryGet("UserID", out long userId))
        {
            // 2. Retrieve the shared proxy instance from your session manager.
            // MUST return the same instance for the same UserID.
            IConnection shardKey = GlobalSessionManager.GetOrCreateUserProxy(userId, connection);
            
            // 3. Hand-off to the internal shard-aware channel using the proxy as the key.
            _inner.HandlePacket(packet, shardKey);
        }
        else
        {
            // Fallback to standard connection-based sharding for unauthenticated packets.
            _inner.HandlePacket(packet, connection);
        }
    }

    // Delegate reporting and lifecycle to inner dispatcher
    public void Activate(CancellationToken ct = default) => _inner.Activate(ct);
    public void Deactivate(CancellationToken ct = default) => _inner.Deactivate(ct);
    public string GenerateReport() => _inner.GenerateReport();
    public IDictionary<string, object> GetReportData() => _inner.GetReportData();
}
```

## 4. Step 3: Register the Router

Inject your custom router into the protocol during the application bootstrapping process.

```csharp
var builder = NetworkApplication.CreateBuilder();

// The builder provides the default PacketDispatchChannel (shard-aware engine)
// as the 'dispatch' argument in the AddTcp/AddUdp factory.
builder.AddTcp<GameProtocol>(dispatch => 
{
    // Wrap the default shard engine with our custom UserBasedRouter
    var userRouter = new UserBasedRouter(dispatch);
    
    return new GameProtocol(userRouter);
});

var app = builder.Build();
app.Run();
```

## Configuration Parameters

The shard-aware system behavior is influenced by `DispatchOptions`. Ensure these are tuned to handle the expected number of logical shards:

| Option | Recommendation |
|---|---|
| `BucketCountMultiplier` | Increase if you have many logical shards (e.g., 100,000+ users) to reduce hash collisions in the dispatch buckets. |
| `MaxPerConnectionQueue` | Limits how many packets can be queued for a single shard (Proxy instance) before the `DropPolicy` kicks in. |

## Why this works

By passing the **Proxy Instance** to `HandlePacket`, the `DispatchChannel<T>` uses that proxy's identity to select a bucket. This effectively groups all physical connections mapped to that proxy into a single serial queue, ensuring that user state updates remain perfectly ordered regardless of which device the packet originated from.

## Related APIs

- [IPacketDispatch](../runtime/routing/packet-dispatch.md)
- [Dispatch Options](../options/dispatch-options.md)
- [IConnection](../../common/connection-contracts.md)
