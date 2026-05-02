# Snowflake

`Snowflake` is the compact identifier type used across the Nalix stack.

## Source mapping

- `src/Nalix.Framework/Identifiers/Snowflake.cs`

## What it is

`Snowflake` is a 64-bit identifier that combines:

- a value portion
- a machine ID
- a `SnowflakeType`

## What it is used for

Common uses include:

- connection IDs
- worker IDs
- system-generated identifiers in server runtime code

## Basic usage

```csharp
Snowflake id = Snowflake.NewId(SnowflakeType.System);
string text = id.ToString();
```

You can also build one explicitly:

```csharp
Snowflake id = Snowflake.NewId(12345, 7, SnowflakeType.System);
```

## Decomposition

| Property | Type | Description |
| :--- | :--- | :--- |
| `Value` | `uint` | The main identifier value (32 bits). |
| `MachineId` | `ushort` | The machine identifier (10 bits). |
| `Type` | `SnowflakeType` | The identifier type (8 bits). |
| `Sequence` | `ushort` | The sequence component (14 bits). |
| `IsEmpty` | `bool` | Whether the identifier is all zeros. |

## Constants & Static Fields

| Member | Type | Description |
| :--- | :--- | :--- |
| `Size` | `const int` | Size in bytes (8). |
| `Empty` | `static ISnowflake` | An empty instance with all components set to zero. |

## Factory Methods

| Method | Signature | Description |
| :--- | :--- | :--- |
| `NewId` | `Snowflake NewId(SnowflakeType type, ushort machineId = 1)` | Creates a new identifier with the specified type and optional machine ID. |
| `NewId` | `Snowflake NewId(SnowflakeType type)` | Creates a new identifier using the configured machine ID. |
| `NewId` | `Snowflake NewId(uint value, ushort machineId, SnowflakeType type)` | Creates an identifier from individual components. |
| `NewId` | `Snowflake NewId(ulong value)` | Creates an identifier from a pre-composed 64-bit value. |
| `FromUInt64` | `static Snowflake FromUInt64(ulong combined)` | Alias for `NewId(ulong)`. |
| `FromBytes` | `static Snowflake FromBytes(ReadOnlySpan<byte> bytes)` | Deserializes from an 8-byte span. |
| `FromBytes` | `static Snowflake FromBytes(byte[] bytes)` | Deserializes from a byte array. |
| `TryParse` | `static bool TryParse(string? s, out Snowflake result)` | Attempts to parse a 16-character hex string. |

## Serialization

| Method | Signature | Description |
| :--- | :--- | :--- |
| `ToUInt64` | `ulong ToUInt64()` | Returns the underlying 64-bit combined value. |
| `ToByteArray` | `byte[] ToByteArray()` | Allocates and returns an 8-byte array. |
| `TryWriteBytes` | `bool TryWriteBytes(Span<byte> destination, out int bytesWritten)` | Writes to a span, returning bytes written. |
| `TryWriteBytes` | `bool TryWriteBytes(Span<byte> destination)` | Writes to a span. |

## Comparison & Equality

Implements `IEquatable<Snowflake>`, `IComparable<Snowflake>`, and `IEquatable<ISnowflake>`.

| Method / Operator | Description |
| :--- | :--- |
| `CompareTo(Snowflake)` | Compares this instance to another. |
| `Compare(Snowflake, Snowflake)` | Static comparison of two identifiers. |
| `Equals(Snowflake)` | Strongly-typed equality check. |
| `Equals(ISnowflake?)` | Equality check against `ISnowflake`. |
| `Equals(Snowflake, Snowflake)` | Static equality check. |
| `==`, `!=` | Equality operators (constant-time comparison). |
| `<`, `>`, `<=`, `>=` | Ordering operators based on underlying 64-bit value. |

## Notes

- Machine ID is loaded from `SnowflakeOptions`
- Generated IDs are compact and sortable enough for runtime use
- Overrides `GetHashCode`/`Equals` for efficient set and dictionary usage
- Native support for high-performance binary serialization through `LiteSerializer`

## Advanced usage

### Equality comparison

```csharp
Snowflake id1 = ...;
Snowflake id2 = ...;
if (id1 == id2) { /* IDs are identical */ }
if (id1 < id2) { /* id1 was created earlier */ }
```

### Serialization

```csharp
byte[] buffer = ...;
id.TryWriteBytes(buffer); // Write bytes directly to a span
Snowflake parsed = Snowflake.FromBytes(buffer); // Read from bytes
ulong raw = id.ToUInt64(); // Get the raw 64-bit value
Snowflake rebuilt = Snowflake.NewId(raw); // Reconstruct from raw value
```

## Related APIs

- [Task Manager](./task-manager.md)
- [Connection Contracts](../abstractions/connection-contracts.md)
- [Serialization Basics](../codec/serialization/serialization-basics.md)

