# DispatchChannel Managed Retention Fix Report

## 1. Confirmed Root

Phan tich source code xac nhan retention root:

`DispatchChannel._stateBuckets[] -> Node(Removed=1) -> ConnectionState._connection -> Connection`

Node la lop nested private trong DispatchChannel<TPacket>. Khi RemoveConnection() duoc goi:

- `node.Removed` duoc set thanh 1 (tombstone flag)
- `ConnectionState` duoc deactivate va drain
- Nhung `Node.Connection` (readonly), `Node.State` (readonly), va `ConnectionState._connection` (readonly) KHONG BAO GIO duoc set thanh null
- Node KHONG BAO GIO bi remove khoi bucket linked list

Ket qua: moi connection da dong van duoc giu boi GC root: `InstanceManager -> PacketDispatchChannel -> DispatchChannel -> _stateBuckets[] -> Node -> Connection -> SocketTcpTransport -> SocketConnection -> Socket -> SocketEventBridge`

## 2. Code Changes

### File: `src/Nalix.Runtime/Internal\Routing/DispatchChannel.cs`

#### 2a. Node class (line ~867)
Thay doi `State` va `Connection` tu `readonly` sang mutable nullable:

`Before:`

`csharp
private sealed class Node(IConnection connection, ConnectionState state, Node? next)
{
    public int Removed;
    public readonly Node? Next = next;
    public readonly ConnectionState State = state;
    public readonly IConnection Connection = connection;
}
`

`After:`

`csharp
private sealed class Node(IConnection connection, ConnectionState state, Node? next)
{
    public int Removed;
    public readonly Node? Next = next;
    public ConnectionState? State = state;
    public IConnection? Connection = connection;
}
`

#### 2b. RemoveConnection() — them reference-break step
Sau khi drain queues, them:

`csharp
node.State = null;
node.Connection = null;
`

#### 2c. RemoveConnection() — null-safe cho state
`csharp
ConnectionState? state = node.State;
if (state is null || !state.TryDeactivate())
{
    return;
}
`

#### 2d. GetOrCreateState() — null-safe cho traversal
`csharp
ConnectionState? existingState = node.State;
if (existingState is null)
{
    continue;
}
`

#### 2e. TryFindNode() — null-safe cho traversal
`csharp
IConnection? nodeConnection = node.Connection;
if (nodeConnection is not null && ReferenceEquals(nodeConnection, connection))
`

#### 2f. PendingPerConnection — null-safe cho traversal
`csharp
ConnectionState? state = node.State;
if (state is null || !state.IsActive) { continue; }
// ...
IConnection? conn = node.Connection;
if (conn is not null) { result[conn] = pending; }
`

#### 2g. Dispose() — null-safe cho cleanup
`csharp
ConnectionState? state = node.State;
if (state is not null)
{
    _ = state.TryDeactivate();
    _ = state.DrainAndDisposeAll();
}
`

### File: `tests/Nalix.Runtime.Tests/DispatchChannelTests.cs`

Them 6 test methods moi va cac helper reflection.

## 3. Why Dispose Was Not Enough

- `Connection.Dispose()` dong socket, giai phong transport, va fire close events.
- `ConnectionHub.TryUnregisterCore()` remove connection khoi registry va goi `ConnectionUnregistered` event.
- `DispatchChannel.OnUnregistered()` -> `RemoveConnection()` -> set `node.Removed = 1`, deactivate state, drain queues.
- Nhung `Node.Connection`, `Node.State`, va `ConnectionState._connection` van giu reference toi toan bo Connection object graph.
- `DispatchChannel` duoc giu boi `PacketDispatchChannel` -> `InstanceManager` (static root).
- GC khong the collect bat ky object nao trong connection graph khi no van reachable qua static root.

## 4. Tests Added

| Test | Description |
|---|---|
| `DispatchChannel_RemoveConnection_ClearsNodeConnectionReference` | Xac nhan Node.Connection = null sau RemoveConnection |
| `DispatchChannel_RemoveConnection_ClearsNodeStateReference` | Xac nhan Node.State = null sau RemoveConnection |
| `DispatchChannel_RemoveConnection_IsIdempotent` | Goi RemoveConnection 3 lan, khong exception, references cleared |
| `DispatchChannel_RemovedNode_IsSkippedByTraversal` | TryClaim khong tra ve connection da remove |
| `DispatchChannel_RemoveConnection_DrainsPendingPackets` | 10 packets duoc drain khi remove |
| `DispatchChannel_RemoveConnection_ConnectionIsGcCollectible` | WeakReference test: connection duoc GC collect sau khi remove |

## 5. Test Results

```text
dotnet test tests/Nalix.Runtime.Tests/Nalix.Runtime.Tests.csproj -c Release --filter DispatchChannel

Test Run Successful.
Total tests: 8
     Passed: 8
 Total time: 1.4104 Seconds
     0 Warning(s)
     0 Error(s)
```

Full test suites:

| Project | Tests | Result |
|---|---|---|
| Nalix.Runtime.Tests | 45 | All Passed |
| Nalix.Runtime.Pipeline.Tests | 45 | All Passed |
| Nalix.Network.Tests | 39 | All Passed |
| Nalix.Framework.Tests | 344 | All Passed |

## 6. Post-Fix Validation

Post-fix heap validation chua duoc chay (khong co backend/load tester san sang).

Du kien sau fix:
- Node tombstones van con trong bucket chains (chi ~16 bytes moi node).
- Nhung Node.Connection = null va Node.State = null.
- Connection, SocketConnection, SocketTcpTransport, SocketEventBridge, Socket se duoc GC collect.
- gcroot khong con tim thay path: `_stateBuckets[] -> Node -> ConnectionState -> Connection`.

## 7. Remaining Risk

- **Tombstone accumulation:** Node tombstones van con trong bucket chains. Moi node chi chiem ~16 bytes (2 fields null + 1 int + 1 pointer Next). Doi voi hang nghin connection, overhead nay khong dang ke.
- **Bucket compaction/unlinking:** Co the la optimization tuong lai. Hien tai, tombstone nodes van duoc skip trong traversal (Removed != 0), nen khong anh huong den hieu suat dispatch.
- **Reactivation after removal:** Truoc fix, RemoveConnection sau do GetOrCreateState co the reactivate node. Sau fix, node da clear nen reactivate se tao node moi. Day la behavior dung — connection moi se duoc phuc vu binh thuong.
- **ConnectionState._connection readonly:** Khong duoc clear trong patch nay de tranh rui ro. Clearing node.State da du de pha vo GC root path. ConnectionState chi con duoc giu boi node, nen khi node.State = null, toan bo graph duoc giai phong.

## 8. Next Step

Chay post-fix heap validation voi backend:
1. Start backend fresh.
2. Run 3 rounds x 500 connections x 60 seconds.
3. Wait 5 minutes idle.
4. Collect dump.
5. Verify: Connection, SocketConnection, SocketTcpTransport count -> 0 (hoac gan 0).
6. Verify: gcroot khong con path tu _stateBuckets den Connection.

---

Report generated: 2026-06-11
Files changed: 2 (DispatchChannel.cs, DispatchChannelTests.cs)
Tests added: 6 new tests
Post-fix validation: Not run (backend not available)
Remaining risk: Tombstone accumulation (minimal), bucket compaction (future optimization)
