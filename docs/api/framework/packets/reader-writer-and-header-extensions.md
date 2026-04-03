# Reader, Writer, and Header Extensions

This page covers the low-level helper APIs around `DataReader`, `DataWriter`, and packet header access.

## Source mapping

- `src/Nalix.Framework/Extensions/DataReaderExtensions.cs`
- `src/Nalix.Framework/Extensions/DataWriterExtensions.cs`
- `src/Nalix.Framework/Extensions/HeaderExtensions.cs`

## Main types

- `DataReaderExtensions`
- `DataWriterExtensions`
- `HeaderExtensions`

## Public members at a glance

| Type | Public members |
|---|---|
| `DataReaderExtensions` | `ReadByte`, `ReadUInt16`, `ReadUInt32`, `ReadInt32`, `ReadInt64`, `ReadUInt64`, `ReadBoolean`, `ReadEnumByte`, `ReadEnumUInt16`, `ReadEnumUInt32`, `ReadBytes`, `ReadRemainingBytes`, `ReadUnmanaged`, `Remaining` |
| `DataWriterExtensions` | `Write`, `WriteEnum`, `WriteUnmanaged` overloads for primitive, span, enum, and unmanaged values |
| `HeaderExtensions` | `ReadMagicNumberLE`, `ReadOpCodeLE`, `ReadFlagsLE`, `WriteFlagsLE`, `ReadPriorityLE`, `ReadTransportLE`, `ReadSequenceIdLE` |

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
- `ReadUInt16()`
- `ReadUInt32()`
- `ReadInt32()`
- `ReadInt64()`
- `ReadUInt64()`
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

- `ReadMagicNumberLE()`
- `ReadOpCodeLE()`
- `ReadFlagsLE()`
- `WriteFlagsLE(flags)`
- `ReadPriorityLE()`
- `ReadTransportLE()`
- `ReadSequenceIdLE()`

## Basic usage

```csharp
DataWriter writer = new();
writer.Write((ushort)opcode);
writer.WriteEnum(priority);
writer.Write(payload);

DataReader reader = new(writer.WrittenSpan);
ushort decodedOpcode = reader.ReadUInt16();
PacketPriority decodedPriority = reader.ReadEnumByte<PacketPriority>();

ushort headerOpcode = writer.WrittenSpan.ReadOpCodeLE();
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

- [Serialization](./serialization.md)
- [Frame Model](./frame-model.md)
- [Packet Registry](./packet-registry.md)
