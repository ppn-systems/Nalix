# Snowflake

`Snowflake` is the compact identifier type used across the Nalix stack.

## Source mapping

- `src/Nalix.Framework/Identifiers/Snowflake.cs`

## What it is

`Snowflake` is a 56-bit identifier that combines:

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

## Notes

- machine ID is loaded from `SnowflakeOptions`
- generated IDs are compact and sortable enough for runtime use
- implements `IEquatable<Snowflake>` and overrides `GetHashCode`/`Equals` for efficient set and dictionary usage
- native support for high-performance binary serialization through `LiteSerializer`

## Advanced usage

### Equality comparison

```csharp
Snowflake id1 = ...;
Snowflake id2 = ...;
if (id1 == id2) { /* IDs are identical */ }
```

### Serialization

```csharp
byte[] buffer = ...;
id.TryWriteBytes(buffer); // Write bytes directly to a span
Snowflake parsed = Snowflake.FromBytes(buffer); // Read from bytes
```

## Related APIs

- [Task Manager](./task-manager.md)
- [Connection Contracts](../../common/connection-contracts.md)
- [Serialization Basics](../serialization/serialization-basics.md)
