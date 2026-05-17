# Nalix.Framework

> High-performance serialization, memory management, cryptography, and shared data structures — the engine room of Nalix.

## Key Features

| Feature | Description |
| :--- | :--- |
| 📦 **LiteSerializer** | Zero-allocation serialization for POCOs and packets. |
| 🧠 **BufferPoolManager** | Shard-aware buffer pooling for LOH and stack-friendly allocations. |
| ♻️ **ObjectPoolManager** | High-throughput object pooling with periodic scrubbing. |
| 📐 **DataFrames** | Base abstractions for packet models and framing. |
| 🆔 **Identifiers** | 64-bit Snowflake-style unique ID generation. |
| 🔐 **Cryptography** | AEAD ciphers (ChaCha20-Poly1305, Salsa20-Poly1305) and X25519 key exchange. |

## Installation

```bash
dotnet add package Nalix.Framework
```

## Quick Example: Buffer Pooling

```csharp
using System;
using Nalix.Environment.Memory;

// Rent a buffer lease of at least 1024 bytes
using BufferLease lease = BufferLease.Rent(1024);

// Write data into the full writable span of the lease
Span<byte> buffer = lease.SpanFull;
buffer[0] = 0x12;
buffer[1] = 0x34;

// Commit the written length
lease.CommitLength(2);

// Access the active payload span
Span<byte> payload = lease.Span;
```

## Quick Example: Unique ID Generation (Snowflake)

```csharp
using System;
using Nalix.Abstractions.Identity;
using Nalix.Framework.Identifiers;

// Generate a new 64-bit unique Snowflake ID for a Session entity
Snowflake sessionId = Snowflake.NewId(SnowflakeType.System);

Console.WriteLine($"Generated ID: {sessionId}");
Console.WriteLine($"Timestamp Component: {sessionId.Value}");
Console.WriteLine($"Machine ID Component: {sessionId.MachineId}");
```

## Documentation

For deep dives into memory management, serialization, and cryptography, see the [official documentation](https://ppn-system.me/concepts/packet-system).

