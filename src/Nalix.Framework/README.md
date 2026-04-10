# Nalix.Framework

The engine room of Nalix. Provides high-performance serialization, memory management, and shared data structures.

## Features

- **LiteSerializer**: Resource-efficient, zero-allocation serialization for POCOs and packets.
- **BufferPoolManager**: Advanced, shard-aware buffer pooling for LOH and stack-friendly allocations.
- **ObjectPoolManager**: High-throughput object pooling with periodic scrubbing.
- **DataFrames**: Base abstractions for packet models and framing.
- **Identifiers**: High-performance 56-bit Snowflake-style unique ID generation.

## Installation

```bash
dotnet add package Nalix.Framework
```

## Quick Example: Pooling

```csharp
using Nalix.Framework.Memory.Buffers;

// Rent a buffer from the global pool
using IBufferLease lease = BufferPoolManager.Rent(1024);
Span<byte> data = lease.Span;

// No need to manually return; lease.Dispose() handles it
```

## Quick Example: Serialization

```csharp
[SerializePackable(SerializeLayout.Explicit)]
public class MyData {
    [SerializeOrder(0)] public int Id { get; set; }
}

byte[] encoded = LiteSerializer.Serialize(new MyData { Id = 1 });
```

## Documentation

For deep dives into memory management and serialization, see the [official documentation](https://ppn-systems.me/concepts/packet-system).
