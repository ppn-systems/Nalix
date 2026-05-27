# Nalix.Codec

> High-performance data transformation and framing engine.

**Nalix.Codec** provides the unified pipeline for data handling in Nalix. It orchestrates serialization, LZ4 compression, AEAD cryptography, and protocol framing into a cohesive, high-throughput transform chain.

## Core Features

| Feature | Description |
| :--- | :--- |
| 🛡️ **Security Engine** | Hardened AEAD cryptography (Chacha20Poly1305, Salsa20Poly1305) and secure X25519 handshake. |
| 📦 **Packet System** | Highly optimized `PacketBase<TPacket>` and `FrameBase` primitives with integrated buffer pooling. |
| 🗜️ **Compression** | High-speed, zero-allocation custom pool-backed LZ4 block compression. |
| ⛓️ **Frame Pipeline** | Multi-layered orchestrated transformation pipelines (e.g., Serialize → Compress → Encrypt). |

## Key Namespaces

| Namespace | Purpose | Key Types |
| :--- | :--- | :--- |
| `Nalix.Codec.DataFrames` | High-performance data framing and runtime packet registry | `PacketBase<TPacket>`, `FrameBase`, `PacketRegistry`, `PacketSchema` |
| `Nalix.Codec.ProtocolFrames` | Specialized low-level framing for control, directives, handshakes, and key exchanges | `Control`, `Directive`, `Handshake`, `KeyExchange`, `SessionResume` |
| `Nalix.Codec.Transforms` | Orchestrated pipelines and transformers sequencing compression and encryption | `FrameTransformer`, `FramePipeline`, `FrameCompression`, `FrameCipher` |
| `Nalix.Codec.Serialization` | Zero-allocation source-generated binary serialization and formatters | `LiteSerializer`, `FormatterProvider`, `IFormatter<T>`, `IFillableFormatter<T>` |
| `Nalix.Codec.Security` | Cryptographic algorithms, envelope ciphers, and key exchange layers | `HandshakeX25519`, `EnvelopeCipher` |
| `Nalix.Codec.Security.Symmetric` | Optimized C# stream cipher implementations | `ChaCha20`, `Salsa20` |
| `Nalix.Codec.Security.Hashing` | Custom high-performance hash functions, message authenticators, and PBKDF2 | `Poly1305`, `Keccak256`, `Pbkdf2`, `HmacKeccak256` |
| `Nalix.Codec.LZ4` & `.Engine` | High-performance stream/block compression and encoder pools | `LZ4BlockEncoder`, `LZ4Codec`, `LZ4Encoder`, `LZ4Decoder`, `LZ4HashTablePool` |
| `Nalix.Codec.Pooling` | Scoped allocation lifecycle guards and factories | `PacketScope`, `PacketFactory` |

## Installation

```bash
dotnet add package Nalix.Codec
```

## Quick Example: Packet Definition

```csharp
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

[Packet]
[GenerateFormatter]
[SerializePackable(SerializeLayout.Explicit)]
public partial class MyPacket : PacketBase<MyPacket>
{
    [SerializeOrder(0)] 
    public string Message { get; set; } = string.Empty;

    public MyPacket()
    {
        this.OpCode = 101; // Assign custom opcode
    }
}
```

## Quick Example: Using LiteSerializer

```csharp
using System;
using Nalix.Codec.Serialization;

// Create a custom serializable packet instance
MyPacket packet = new MyPacket { Message = "Hello Nalix!" };

// Serialize the packet into a byte array
byte[] encoded = LiteSerializer.Serialize(packet);

// Deserialize the byte array back to the packet type
MyPacket decoded = LiteSerializer.Deserialize<MyPacket>(encoded, out int bytesRead);

Console.WriteLine($"Decoded Message: {decoded.Message} (Read {bytesRead} bytes)");
```

## Performance Principles

- **Zero-Allocation:** All hot paths use `Span<byte>` and pooled buffers.
- **No Reflection:** Serialization is handled via compile-time source generation.
- **Memory Efficient:** Uses `BufferLease` and reference counting for large data payloads.

## Documentation

For technical details on the transform pipeline and packet schema, see the [Codec API Reference](https://ppn-system.me/api/Codec/index).
