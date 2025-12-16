# PacketDispatchChannel — Lease-based packet dispatcher

`PacketDispatchChannel` is the main asynchronous dispatch loop for raw network frames. It accepts `IBufferLease` payloads, queues them by connection and packet priority, runs buffer middleware, deserializes `IPacket`, and then invokes the compiled handler pipeline from `PacketDispatcherBase<IPacket>`.

## Mapped sources

- `src/Nalix.Network/Routing/PacketDispatchChannel.cs`
- `src/Nalix.Network/Routing/PacketDispatcherBase.cs`
- `src/Nalix.Network/Routing/Options/PacketDispatchOptions.cs`
- `src/Nalix.Network/Routing/Options/PacketDispatchOptions.PublicMethods.cs`
- `src/Nalix.Network/Routing/Options/PacketDispatchOptions.Execution.cs`

## Runtime model

- `_dispatch` is a priority-aware `DispatchChannel<IPacket>`.
- `_semaphore` signals worker loops when leases are available.
- `Activate(...)` starts `DispatchLoopCount` workers, or defaults to `clamp(Environment.ProcessorCount / 2, 1, 12)`.
- `Deactivate(...)` cancels the workers, cancels the shared CTS, and releases the semaphore so waiting loops can exit.

## Input paths

`HandlePacket(IBufferLease, IConnection)`:

- rejects empty leases
- pushes the lease into `_dispatch`
- releases the semaphore once

`HandlePacket(IPacket, IConnection)`:

- bypasses the queue and directly executes the compiled handler pipeline

## Worker loop

Each worker executes this sequence:

1. wait on `_semaphore`
2. pull the next `(connection, lease)` pair from `_dispatch`
3. run `Options.NetworkPipeline.ExecuteAsync(...)`
4. deserialize through `IPacketRegistry.TryDeserialize(...)`
5. call `ExecutePacketHandlerAsync(packet, connection)`
6. dispose the lease

If middleware returns `null`, the packet is dropped before deserialization. If deserialization fails, the dispatcher logs the packet head in hex and drops the lease.

## Diagnostic report

`GenerateReport()` includes:

- running state
- dispatch loop count
- total pending packets
- total / ready connection counts
- pending ready connections per priority
- top connections by pending packet count
- semaphore count and CTS status
- packet registry type

## Notes

- The dispatcher depends on a registered `IPacketRegistry` in `InstanceManager`; construction fails without it.
- Buffer middleware runs before deserialization, which is where frame decryption / decompression belongs.
- Actual handler execution, `PacketContext<TPacket>` creation, middleware, exception mapping, and return-type handling are implemented in `PacketDispatcherBase<IPacket>` and `PacketDispatchOptions<TPacket>`.

## See also

- [PacketContext](./PacketContext.md)
- [Middleware README](../Middleware/README.md)
- [DispatchOptions](../Configuration/DispatchOptions.md)
