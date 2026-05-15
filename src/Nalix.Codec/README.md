# Nalix.Codec

> High-performance data transformation and framing engine.

**Nalix.Codec** provides the unified pipeline for data handling in Nalix. It orchestrates serialization, LZ4 compression, AEAD cryptography, and protocol framing into a cohesive, high-throughput transform chain.

## Core Components

| Feature | Description |
| :--- | :--- |
| 🛡️ **Security Engine** | ChaCha20-Poly1305 and X25519 based crypto stack. |
| 📦 **Packet System** | `PacketBase<T>` primitives with integrated pooling. |
| 🗜️ **Compression** | Custom pool-backed LZ4 block compression. |
| ⛓️ **Frame Pipeline** | Orchestrated transformation chains (Compress → Encrypt). |

## Installation

```bash
dotnet add package Nalix.Codec
```

## Quick Example: Packet Definition

```csharp
[GenerateFormatter]
[SerializeHeader(Opcode = 101)]
public partial class MyPacket : PacketBase<MyPacket>
{
    [SerializeOrder(0)] public string Message { get; set; } = string.Empty;
}
```

## Performance Principles

- **Zero-Allocation:** All hot paths use `Span<byte>` and pooled buffers.
- **No Reflection:** Serialization is handled via compile-time source generation.
- **Memory Efficient:** Uses `BufferLease` and reference counting for large data payloads.

## Documentation

For technical details on the transform pipeline and packet schema, see the [Codec API Reference](https://ppn-system.me/api/Codec/index).
