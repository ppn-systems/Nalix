# Reader, Writer, and Header Extensions

This page covers the low-level helper APIs around `DataReader`, `DataWriter`, and packet header access.

## Source mapping

- `src/Nalix.Codec/Extensions/DataReaderExtensions.cs`
- `src/Nalix.Codec/Extensions/DataWriterExtensions.cs`
- `src/Nalix.Codec/Extensions/HeaderExtensions.cs`

## Main types

- `DataReaderExtensions`
- `DataWriterExtensions`
- `HeaderExtensions`

## Public members at a glance

| Type | Public members |
|---|---|
| `DataReaderExtensions` | `ReadByte`, `ReadInt16`, `ReadUInt16`, `ReadInt32`, `ReadUInt32`, `ReadInt64`, `ReadUInt64`, `ReadSByte`, `ReadChar`, `ReadSingle`, `ReadDouble`, `ReadBoolean`, `ReadEnumByte`, `ReadEnumUInt16`, `ReadEnumUInt32`, `ReadBytes`, `ReadRemainingBytes`, `ReadUnmanaged`, `Remaining` |
| `DataWriterExtensions` | `Write`, `WriteEnum`, `WriteUnmanaged` overloads for primitive, span, enum, and unmanaged values |
| `HeaderExtensions` | `ReadHeaderLE`, `WriteHeaderLE` |

## When to use these helpers

Use these APIs when you are:

- implementing custom packet serialization by hand
- inspecting packet headers before full deserialization
- writing high-throughput code that should avoid extra allocations

These helpers sit below the usual `LiteSerializer` and `PacketBase<TSelf>` flow.

## DataReaderExtensions

`DataReaderExtensions` adds direct primitive and enum readers on top of `DataReader`.

Useful methods include:

- `ReadByte()`
- `ReadSByte()`
- `ReadInt16()`
- `ReadUInt16()`
- `ReadInt32()`
- `ReadUInt32()`
- `ReadInt64()`
- `ReadUInt64()`
- `ReadChar()`
- `ReadSingle()`
- `ReadDouble()`
- `ReadBoolean()`
- `ReadEnumByte<TEnum>()`
- `ReadEnumUInt16<TEnum>()`
- `ReadEnumUInt32<TEnum>()`
- `ReadBytes(count)`
- `ReadRemainingBytes()`
- `ReadUnmanaged<T>()`
- `Remaining()`

## DataWriterExtensions

`DataWriterExtensions` adds matching write helpers for primitive, enum, span, and unmanaged values.

Useful methods include:

- `Write(byte)`
- `Write(ushort)`
- `Write(uint)`
- `Write(int)`
- `Write(long)`
- `Write(ulong)`
- `Write(bool)`
- `Write(byte[])`
- `Write(ReadOnlySpan<byte>)`
- `WriteEnum<TEnum>(value)`
- `WriteUnmanaged<T>(value)`

## HeaderExtensions

`HeaderExtensions` provides fixed-offset packet header readers over raw byte spans.

This is useful when you want to inspect protocol information before creating a full packet instance.

Useful methods include:

- `ReadHeaderLE()` — returns a `PacketHeader` struct (10 bytes) from the span
- `WriteHeaderLE(header)` — writes a `PacketHeader` struct (10 bytes) to the span

Individual fields are accessed through the returned `PacketHeader` struct:

```csharp
PacketHeader header = span.ReadHeaderLE();
uint magic = header.MagicNumber;
ushort opCode = header.OpCode;
PacketFlags flags = header.Flags;
PacketPriority priority = header.Priority;
ushort sequenceId = header.SequenceId;
```

## Basic usage

```csharp
DataWriter writer = new(256);
writer.Write((ushort)opcode);
writer.WriteEnum(priority);
writer.Write(payload);

DataReader reader = new(writer.WrittenSpan);
ushort decodedOpcode = reader.ReadUInt16();
PacketPriority decodedPriority = reader.ReadEnumByte<PacketPriority>();

ushort headerOpcode = writer.WrittenSpan.ReadHeaderLE().OpCode;
```

## Design notes

- `DataReaderExtensions` and `DataWriterExtensions` are marked as editor-hidden helpers, but they are still part of the public API surface.
- `HeaderExtensions` works directly on `Span<byte>` and `ReadOnlySpan<byte>` and is intended for hot paths that need fast header inspection.
- These helpers assume the Nalix packet header layout and explicit little-endian reads for protocol stability.

### Common pitfalls

- using these helpers when `LiteSerializer` or `PacketBase<TSelf>` already gives you the shape you want
- reading header fields from the wrong byte offset
- mixing endianness assumptions with the fixed Nalix wire layout

## Related APIs

- [Serialization](./serialization-basics.md)
- [Frame Model](../packets/frame-model.md)
- [Packet Registry](../packets/packet-registry.md)


