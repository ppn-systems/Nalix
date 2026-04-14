# Nalix.Framework

> High-performance serialization, memory management, cryptography, and shared data structures — the engine room of Nalix.

## Key Features

| Feature | Description |
| :--- | :--- |
| 📦 **LiteSerializer** | Zero-allocation serialization for POCOs and packets. |
| 🧠 **BufferPoolManager** | Shard-aware buffer pooling for LOH and stack-friendly allocations. |
| ♻️ **ObjectPoolManager** | High-throughput object pooling with periodic scrubbing. |
| 📐 **DataFrames** | Base abstractions for packet models and framing. |
| 🆔 **Identifiers** | 56-bit Snowflake-style unique ID generation. |
| 🔐 **Cryptography** | AEAD ciphers (ChaCha20-Poly1305, Salsa20-Poly1305) and X25519 key exchange. |

## Installation

```bash
dotnet add package Nalix.Framework
```

## Quick Example: Buffer Pooling

```csharp
using Nalix.Framework.Memory.Buffers;

// Rent a buffer from the global pool
using IBufferLease lease = BufferPoolManager.Rent(1024);
Span<byte> data = lease.Span;

// No need to manually return — lease.Dispose() handles it.
```

## Quick Example: Serialization

```csharp
using Nalix.Framework.Serialization;

[SerializePackable(SerializeLayout.Explicit)]
public class MyData
{
    [SerializeOrder(0)] public int Id { get; set; }
}

byte[] encoded = LiteSerializer.Serialize(new MyData { Id = 1 });
```

## Documentation

For deep dives into memory management, serialization, and cryptography, see the [official documentation](https://ppn-systems.me/concepts/packet-system).
