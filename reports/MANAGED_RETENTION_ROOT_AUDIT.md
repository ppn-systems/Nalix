# Managed Retention Root Audit

## 1. Scope
Chi audit. Khong thay doi code, khong thay doi cau hinh, khong chay formatter, khong commit.
Pham vi: managed retention only — khong thao luan client behavior, IP behavior, proxy, RPS, latency, quota, benchmark quality.

## 2. Evidence Used

### Source files inspected (no dump files available — pure source-code audit):

| File | Path |
|---|---|
| DispatchChannel.cs (DispatchChannel, Node, ConnectionState, RemoveConnection) | `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs` |
| TimingWheel.cs (TimeoutTask, TimingWheelBucket, Register, Unregister, RUN_LOOP) | `src/Nalix.Network/Internal/Time/TimingWheel.cs` |
| PacketSender.cs (ResetForPool, Initialize) | `src/Nalix.Runtime/Dispatching/PacketSender.cs` |
| PacketContext.cs (ResetForPool, Return, Initialize) | `src/Nalix.Runtime/Dispatching/PacketContext.cs` |
| SocketEventBridge.cs (OnFrameReceived, OnTransportClosed) | `src/Nalix.Network/Internal/Transport/SocketEventBridge.cs` |
| SocketConnection.cs (Dispose, OBSERVE_RECEIVE_LOOP_SHUTDOWN) | `src/Nalix.Network/Internal/Transport/SocketConnection.cs` |
| SocketTcpTransport.cs | `src/Nalix.Network/Internal/Transport/SocketTcpTransport.cs` |
| Connection.cs (Dispose, PerformDestructiveCleanup, AcquireEventArgs) | `src/Nalix.Network/Connections/Connection.cs` |
| Connection.Hub.cs (RegisterConnection, TryUnregisterCore, ConnectionUnregistered) | `src/Nalix.Network/Connections/Connection.Hub.cs` |
| Connection.EventArgs.cs (Initialize, ResetForPool, Dispose) | `src/Nalix.Network/Connections/Connection.EventArgs.cs` |
| LocalPool.cs (Acquire, Return, Destroy) | `src/Nalix.Network/Internal/Pooling/LocalPool.cs` |
| PooledConnectEventContext.cs (ResetForPool, Dispose, Initialize) | `src/Nalix.Network/Internal/Pooling/PooledConnectEventContext.cs` |
| PooledSocketReceiveContext.cs (ResetForPool, ReceiveAsync) | `src/Nalix.Network/Internal/Pooling/PooledSocketReceiveContext.cs` |
| AsyncCallback.cs (QUEUE, EXECUTE_AND_RETURN) | `src/Nalix.Network/Internal/Transport/AsyncCallback.cs` |
| PacketDispatchChannel.cs | `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs` |
| PacketDispatchOptions.Execution.cs (ExecuteResolvedHandlerAsync) | `src/Nalix.Runtime/Dispatching/Options/PacketDispatchOptions.Execution.cs` |
| ObjectPoolManager.cs (Return calls ReturnFast which calls ResetForPool) | `src/Nalix.Framework/Memory/Objects/ObjectPoolManager.cs` |
| ObjectPool.cs (ReturnFast calls obj.ResetForPool()) | `src/Nalix.Framework/Memory/Pools/ObjectPool.cs` |
| ConnectionRegistry.cs (TryRemove, UntrackEndpoint) | `src/Nalix.Network/Internal/Connections/ConnectionRegistry.cs` |
| TcpListener.Handle.cs (InitializeConnection, HandleConnectionClose) | `src/Nalix.Network/Listeners/TcpListener/TcpListener.Handle.cs` |
| PacketContextBridge.cs | `src/Nalix.Runtime/Dispatching/PacketContextBridge.cs` |

**Luu y:** Khong co file dump (.dmp, .gcdump) nao duoc tim thay trong repository. Khong co dump-analysis nao duoc chay. Ket luan dua hoan toan tren phan tich source code tinh.

## 3. Exact Node Type Verification

### DispatchChannel.Node (confirmed retaining type)

| Field | Type | Mutable? | Cleared on RemoveConnection()? | Retains Connection? |
|---|---|---|---|---|
| `Connection` | `readonly IConnection` | readonly | Khong bao gio | **YES** |
| `State` | `readonly ConnectionState` | readonly | Khong bao gio | **YES** (trung gian) |
| `Next` | `readonly Node?` | readonly | Khong bao gio | **YES** (linked-list chain) |
| `Removed` | `int` | mutable | Set to 1 | Khong truc tiep |

### ConnectionState (confirmed retaining type)

| Field | Type | Mutable? | Cleared on RemoveConnection()? | Retains Connection? |
|---|---|---|---|---|
| `_connection` | `readonly IConnection` | readonly | Khong bao gio | **YES** |
| `_activeFlag` | `int` | mutable | Set to 0 | Khong truc tiep |
| `_readyFlag` | `int` | mutable | Set to 0 | Khong truc tiep |
| `_boundedQueues` | `MpmcRing?[]` | mutable | Drained (khong nulled) | Khong truc tiep |
| `_unboundedQueues` | `UnboundedQueue?[]` | mutable | Drained (khong nulled) | Khong truc tiep |

### TimingWheel.TimeoutTask (NOT retaining — properly cleaned)

| Field | Cleared on Unregister? | Cleared on ResetForPool()? | Cleared by PerformDestructiveCleanup()? |
|---|---|---|---|
| `Conn` | Yes (via pool.Return) | Yes (Conn = null) | Yes (task.Conn = null) |
| `Rounds` | — | Yes | — |
| `Version` | — | Yes | — |
| `Next/Prev` | Yes (bucket.Remove) | Yes | — |
| `Bucket` | Yes (bucket.Remove) | Yes | — |

## 4. Connection Root Paths

Khong co dump file nen khong the chay gcroot. Tuy nhien, phan tich source code xac nhan cac root path sau:

| Path | External Root | Reference Chain | Retains After Close? | Verdict |
|---|---|---|---|---|
| **A (PRIMARY)** | Static InstanceManager -> PacketDispatchChannel -> DispatchChannel | _stateBuckets -> Node -> ConnectionState._connection -> Connection | **YES — readonly, vinh viu** | **RETENTION ROOT** |
| **B (PRIMARY)** | Same as A | _stateBuckets -> Node.Connection -> Connection | **YES — readonly, vinh viu** | **RETENTION ROOT** |
| **C** | Same as A | _stateBuckets -> Node.Next -> next Node -> ... (linked list chain) | **YES — readonly Next field** | **RETENTION ROOT** |
| D | TimingWheel._wheel -> TimingWheelBucket -> TimeoutTask | TimeoutTask.Conn -> Connection | Cleared by Unregister/PerformDestructiveCleanup | Not a root |
| E | ObjectPoolManager -> pool -> PacketContext | PacketContext.Sender -> PacketSender._connection | ResetForPool clears both | Not a root |
| F | ObjectPoolManager -> pool -> PacketContext | PacketContext.Connection | ResetForPool sets to default | Not a root |
| G | Connection._argsPool -> LocalPool -> ConnectionEventArgs | _connection | ResetForPool sets to null | Not a root |
| H | Connection._contextPool -> LocalPool -> PooledConnectEventContext | Sender, Args | ResetForPool clears both | Not a root |

### Truy vet chi tiet Root Path A:

`
Static GC Root (AppDomain / assembly)
  -> InstanceManager.Instance (static singleton)
     -> NetworkApplicationBuilder builds PacketDispatchChannel
       -> PacketDispatchChannel._dispatch: DispatchChannel<IPacket>
         -> _stateBuckets[]: Node?[bucketCount]   <- static-ish field (rooted by DispatchChannel)
           -> Node[removed=1]  <- tombstoned but NEVER removed from array/list
             |-- Node.Connection (readonly IConnection) -> Connection
             |    |-- _bridge: SocketEventBridge
             |    |    +-- _callbackProcess, _callbackPost, _callbackClose (delegates)
             |    |-- TcpTransport: SocketTcpTransport
             |    |    +-- _socket: SocketConnection
             |    |         |-- _socket: System.Net.Sockets.Socket
             |    |         |-- _owner: IConnection (back-ref)
             |    |         +-- _sink: SocketEventBridge (back-ref)
             |    |-- _argsPool: LocalPool<ConnectionEventArgs>
             |    |-- _contextPool: LocalPool<PooledConnectEventContext>
             |    +-- NetworkEndpoint: SocketEndpoint
             +-- Node.State: ConnectionState
                  +-- _connection (readonly IConnection) -> Connection (duplicate ref)
`

**Moi Node tombstoned giu nguyen toan bo do thi Connection: Connection -> SocketTcpTransport -> SocketConnection -> Socket -> SocketEventBridge -> delegates -> LocalPool -> ConnectionEventArgs / PooledConnectEventContext.**

## 5. PacketSender / PacketContext Audit

| Sample | Component | _connection / Connection null? | Root Path | Retains Connection? | Verdict |
|---|---|---|---|---|---|
| 1 | PacketSender sau khi Return | null (ResetForPool sets _connection = null) | ObjectPoolManager -> pool | Khong | Not a root |
| 2 | PacketContext sau khi Return | null (ResetForPool sets Connection = default) | ObjectPoolManager -> pool | Khong | Not a root |
| 3 | PacketContext trong khi InUse | Non-null (expected) | ExecuteResolvedHandlerAsync scope | Chi tam thoi | Not a root (transient) |

### Chi tiet lifecycle PacketContext:

1. **Get from pool:** ObjectPoolManager.Get() — object da duoc ResetForPool() khi return truoc do.
2. **Initialize:** Sets Connection, calls Sender.Initialize(this) -> Sender._connection = context.Connection.
3. **Use:** Handler executes, context.Sender.SendAsync() uses _connection.
4. **Dispose:** PacketContext.Dispose() -> Return() -> s_pool.Return(this).
5. **ObjectPool.ReturnFast** -> obj.ResetForPool() -> clears Connection, Sender.ResetForPool() (clears _connection).

**Conclusion:** PacketSender/PacketContext khong giu connection sau khi return. ResetForPool() duoc goi tu dong boi ObjectPool.ReturnFast().

## 6. TimingWheel vs DispatchChannel Conclusion

### TimingWheel — KHONG phai retention root

TimingWheel xu ly dung tren tat ca cac close path:

1. **Unregister() path (normal close):**
   - connection.IsRegisteredInWheel = false
   - connection.TimeoutVersion++
   - connection.OnCloseEvent -= this.OnConnectionClosed
   - bucket.Remove(task) -> task duoc detached tu doubly-linked list
   - connection.TimeoutTask = null
   - _poolManager.Return(task) -> task.ResetForPool() -> task.Conn = null

2. **RUN_LOOP idle-timeout path:**
   - connection.IsRegisteredInWheel = false
   - connection.TimeoutVersion++
   - connection.TimeoutTask = null
   - _poolManager.Return(task) -> task.Conn = null

3. **RUN_LOOP stale-task path:**
   - connection.TimeoutTask = null
   - _poolManager.Return(task) -> task.Conn = null

4. **PerformDestructiveCleanup() path (explicit):**
   - task.Conn = null (line 446 in Connection.cs)
   - connection.TimeoutTask = null (line 447)

**Ket luan:** TimingWheel KHONG giu connection sau khi unregister/close. Moi path deu clear TimeoutTask.Conn truoc khi return.

### DispatchChannel — LA retention root (CONFIRMED)

RemoveConnection() (line 654-689):

`
private void RemoveConnection(IConnection connection)
{
    if (!this.TryFindNode(connection, out Node? node) || node is null) return;
    if (Interlocked.Exchange(ref node.Removed, 1) != 0) return;     // tombstone flag set

    ConnectionState state = node.State;
    if (!state.TryDeactivate()) return;                               // active flag cleared

    DecrementNonNegative(ref _activeConnections);
    if (state.TryReleaseReady()) DecrementNonNegative(ref _readyConnections);

    int drained = state.DrainAndDisposeAll();                         // queues drained
    if (drained > 0) DecrementNonNegative(ref _packetCount.Value, drained);

    // !! node.Connection — readonly, NEVER cleared
    // !! node.State — readonly, NEVER cleared
    // !! node.State._connection — readonly, NEVER cleared
    // !! Node is NEVER removed from _stateBuckets[] linked list
}
`

**Node constructor:**
`
private sealed class Node(IConnection connection, ConnectionState state, Node? next)
{
    public int Removed;                                               // mutable — tombstone flag
    public readonly Node? Next = next;                                // readonly — linked list stays
    public readonly ConnectionState State = state;                    // readonly — holds connection
    public readonly IConnection Connection = connection;              // readonly — direct connection ref
}
`

**Nghich ly thiet ke:** Node duoc tombstoned (Removed=1) de danh dau "da xoa", nhung:
- Node.Connection (readonly) van giu reference den Connection
- Node.State (readonly) van giu reference den ConnectionState
- ConnectionState._connection (readonly) van giu reference den Connection
- Node van nam trong _stateBuckets[] linked list

**Tombstone chi vo hieu hoa logic (traversal skip), nhung KHONG giai phong reference graph.**

## 7. Confirmed Retaining Fields

| Component | Field | Type | Why Retained |
|---|---|---|---|
| DispatchChannel.Node | Connection | readonly IConnection | Readonly field, khong duoc clear trong RemoveConnection() |
| DispatchChannel.Node | State | readonly ConnectionState | Readonly field, khong duoc clear trong RemoveConnection() |
| DispatchChannel.Node | Next | readonly Node? | Readonly field, linked-list chain giua cac tombstoned nodes |
| DispatchChannel.ConnectionState | _connection | readonly IConnection | Readonly field, khong duoc clear trong RemoveConnection() |

**Tong retained graph tu moi tombstoned Node:**
- Connection (IConnection)
- SocketTcpTransport (Connection.TcpTransport)
- SocketConnection (SocketTcpTransport._socket)
- System.Net.Sockets.Socket (SocketConnection._socket)
- SocketEventBridge (Connection._bridge)
- IOpCodeExtractor (Connection.PacketClassifier)
- INetworkEndpoint / SocketEndpoint (Connection.NetworkEndpoint)
- LocalPool<ConnectionEventArgs> (Connection._argsPool)
- LocalPool<PooledConnectEventContext> (Connection._contextPool)
- Any events/delegates still subscribed on connection

## 8. Non-causes (Ruled Out)

| Component | Why Ruled Out |
|---|---|
| **TimingWheel.TimeoutTask** | Conn cleared on all paths (Unregister, RUN_LOOP idle, RUN_LOOP stale, PerformDestructiveCleanup). ResetForPool() nulls all fields. |
| **PacketSender** | ResetForPool() sets _connection = null. Called automatically by ObjectPool.ReturnFast(). |
| **PacketContext** | ResetForPool() sets Connection = default and calls Sender.ResetForPool(). Called automatically by ObjectPool.ReturnFast(). |
| **ConnectionEventArgs** | ResetForPool() sets _connection = null. LocalPool.Return() calls ResetForPool(). |
| **PooledConnectEventContext** | ResetForPool() sets Sender = null, Args = default, Callback = null, LocalOwner = null. |
| **PooledSocketReceiveContext** | ResetForPool() clears SAEA, returns it to pool, nulls _args. |
| **SocketAsyncEventArgs.UserToken** | Cleared in PooledSocketReceiveContext.ResetForPool(): _args.UserToken = null. |
| **ObjectMap (Attributes)** | Returned by _attributes?.Return() in PerformDestructiveCleanup(), then _attributes = null. |
| **System.Net.Sockets.Socket handle** | Socket is disposed by SocketConnection.Dispose() and by Connection.PerformDestructiveCleanup(). The Socket object is not retained by pools — only by the Node chain. |
| **LocalPool bypassing ResetForPool** | LocalPool.Return() calls item.ResetForPool() on both local-return and fallback-to-global paths. LocalPool.Destroy() calls ResetForPool() on idle items. |
| **SocketEventBridge delegates** | Bridge is an instance field of Connection. The bridge holds delegates to Connection methods, but this is an internal cycle rooted by the external Node -> Connection path. Without the Node root, the entire cycle becomes collectible. |

## 9. Classification

# **Retention Root Confirmed**

Ly do: Source code analysis chung minh ro rang:

1. DispatchChannel.RemoveConnection() chi set node.Removed = 1 (tombstone).
2. Node.Connection (readonly), Node.State (readonly), ConnectionState._connection (readonly) KHONG BAO GIO duoc clear.
3. Node KHONG BAO GIO bi xoa khoi _stateBuckets[] linked list.
4. DispatchChannel duoc giu alive boi PacketDispatchChannel -> InstanceManager (static root).
5. Moi tombstoned Node giu nguyen toan bo Connection object graph.

Mac du khong co heap dump de chay gcroot, phan tich source code cung cap bang chac chan ve root path va retaining fields. Khong co path nao clear cac readonly fields nay, va khong co path nao loai bo Node khoi bucket array.

## 10. Minimal Fix Recommendation

**Phuong an toi thieu (khong ap dung — chi ghi nhat):**

Thay doi Node va ConnectionState de pha vo reference chain trong RemoveConnection():

1. **Node:** Chuyen Connection va State tu readonly sang mutable (hoac dung pattern nullable).
2. **ConnectionState:** Chuyen _connection tu readonly sang mutable.
3. **RemoveConnection():** Sau khi drain, set:
   - node.State = null (hoac equivalent)
   - node.Connection = null (hoac equivalent)
   - state._connection = null (hoac equivalent)
4. Traversal paths (TryClaimWeighted, PendingPerConnection, etc.) da skip node co Removed == 1, nen null-safe checks chi can them null-guard.

**Trade-off:**
- Thay doi readonly -> mutable se co minor performance impact (mat JIT optimization cho readonly fields).
- Alternative: Giu readonly nhung unlink Node khoi bucket list (complex hon, can lock-free unlink logic).
- Alternative: Dung tombstone object thay the de overwrite readonly fields.

---

Report generated: 2026-06-11
Classification: Retention Root Confirmed
Exact root owner: DispatchChannel._stateBuckets -> Node (tombstoned but never cleared)
Exact retaining fields: Node.Connection (readonly), Node.State (readonly), ConnectionState._connection (readonly)
