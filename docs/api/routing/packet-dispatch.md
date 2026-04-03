# Packet Dispatch

`PacketDispatchChannel` is the main asynchronous dispatch loop for raw network frames. It accepts `IBufferLease` payloads, queues them by connection and packet priority, runs buffer middleware, deserializes `IPacket`, and then invokes the compiled handler pipeline from `PacketDispatcherBase<IPacket>`.

## Source mapping

- `src/Nalix.Network/Routing/PacketDispatchChannel.cs`
- `src/Nalix.Network/Routing/PacketDispatcherBase.cs`
- `src/Nalix.Network/Routing/PacketDispatchOptions.cs`
- `src/Nalix.Network/Routing/PacketDispatchOptions.Execution.cs`
- `src/Nalix.Network/Routing/PacketDispatchOptions.PublicMethods.cs`

## Runtime model

- `_dispatch` is a priority-aware `DispatchChannel<IPacket>`
- `_semaphore` signals worker loops when leases are available
- `Activate(...)` starts `DispatchLoopCount` workers, or defaults to `clamp(Environment.ProcessorCount / 2, 1, 12)`
- `Deactivate(...)` cancels workers and releases the semaphore so blocked loops can exit

## Public members at a glance

| Type | Public members |
|---|---|
| `PacketDispatchChannel` | `Activate(...)`, `Deactivate(...)`, `HandlePacket(...)` overloads, `GenerateReport()`, `Dispose()` |
| `PacketDispatcherBase<TPacket>` | compiled handler and middleware execution helpers used by the channel |

## Input paths

`HandlePacket(IBufferLease, IConnection)`:

- rejects empty leases
- pushes the lease into `_dispatch`
- releases the semaphore once

`HandlePacket(IPacket, IConnection)`:

- is a typed fast-path for internal callers and directly executes the compiled handler pipeline
- should be treated as an exception to the queue-based runtime flow, not the primary ingress path

## Worker loop

Each worker:

1. waits on `_semaphore`
2. pulls the next `(connection, lease)` pair from `_dispatch`
3. runs `Options.NetworkPipeline.ExecuteAsync(...)`
4. deserializes through `IPacketRegistry.TryDeserialize(...)`
5. calls `ExecutePacketHandlerAsync(packet, connection)`
6. disposes the lease

If middleware returns `null`, the packet is dropped before deserialization. If deserialization fails, the dispatcher logs the packet head in hex and drops the lease.

### Failure modes worth knowing

- a full queue can delay or drop incoming work before it reaches handlers
- middleware returning `null` is an intentional drop path, not a silent failure
- deserialization failures mean the packet registry or packet bytes are out of sync

## Diagnostics

`GenerateReport()` includes:

- running state
- dispatch loop count
- total pending packets
- total and ready connection counts
- pending ready connections per priority
- top connections by pending packet count
- semaphore count and cancellation status
- packet registry type

### Common pitfalls

- calling the typed `HandlePacket(IPacket, IConnection)` path as if it were the normal ingress path
- assuming a packet made it to middleware just because the socket accepted the bytes
- forgetting to inspect queue pressure when handler latency grows

## Basic usage

```csharp
dispatch.Activate(ct);

dispatch.HandlePacket(lease, connection);

string report = dispatch.GenerateReport();
Console.WriteLine(report);
```

Typical flow:

1. accept a raw buffer lease from the connection
2. queue it into the dispatcher
3. run middleware and deserialization in the worker loop
4. invoke handlers and dispose the lease

## Related APIs

- [Packet Context](./packet-context.md)
- [Packet Metadata](./packet-metadata.md)
- [Handler Results](./handler-results.md)
- [Middleware Pipeline](../middleware/pipeline.md)
- [Protocol](../network/runtime/protocol.md)
