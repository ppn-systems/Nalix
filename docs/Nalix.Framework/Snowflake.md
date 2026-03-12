# Snowflake — Unique 56-bit Identifier (Nalix.Framework.Identifiers)

`Snowflake` is a high-performance, compact 56-bit unique identifier struct, suitable for distributed systems that require sortable, efficient, and strongly-typed IDs.

- **Namespace:** `Nalix.Framework.Identifiers`
- **Struct:** `Snowflake`
- **Implements:** `ISnowflake`, `IEquatable<Snowflake>`, `IComparable<Snowflake>`
- **Design:** Encodes value, machine identifier, and type into a 7-byte/56-bit integer.

---

## Features

- Small footprint: 7 bytes per ID (56 bits)
- Encodes:
  - **Value:** Main integer (32 bits)
  - **MachineId:** Distributed node/machine (16 bits)
  - **Type:** Application-assigned type (8 bits)
- Provides sortable, unique identifiers suitable for sharded or multi-machine environments.
- Easy conversion to/from hex, bytes, integer, etc.
- Constant-time equality/comparison for security.

---

## Basic Usage

### Create a new Snowflake ID

**Default static factory method (timestamp, configured machine ID):**

```csharp

using Nalix.Framework.Identifiers;
using Nalix.Common.Identity.Enums;

var id = Snowflake.NewId(SnowflakeType.USER);
Console.WriteLine(id.ToString()); // e.g., "00110022990001AB"
```

**Create with specified machine:**

```csharp
var id = Snowflake.NewId(SnowflakeType.SYSTEM, machineId: 42);
```

**Create from components:**

```csharp
var id = Snowflake.NewId(value: 12345, machineId: 1, type: SnowflakeType.ORDER);
```

---

## Decompose/Read Properties

| Property          | Type              | Description                        |
|-------------------|-------------------|------------------------------------|
| `Value`           | `uint`            | Main integer value (low 32 bits)   |
| `MachineId`       | `ushort`          | Machine/node identifier (16 bits)  |
| `Type`            | `SnowflakeType`   | Application-assigned type (8 bits) |
| `IsEmpty`         | `bool`            | True if all bits are zero          |
| `Empty` (static)  | `Snowflake`       | All-zero singleton                 |

**Example:**

```csharp
var value     = id.Value;
var machineId = id.MachineId;
var type      = id.Type;
var emptyId   = Snowflake.Empty;
```

---

## Serialization & Conversion

- **To hex string:**  

  ```csharp
  string hex = id.ToString(); // Always 14 hex digits (7 bytes)
  ```

- **To integer:**  

  ```csharp
  using Nalix.Common.Primitives;

  UInt56 raw = id.ToUInt56();
  ```

- **To byte array:**  

  ```csharp
  byte[] arr = id.ToByteArray();
  ```

- **From bytes:**  

  ```csharp
  var id2 = Snowflake.FromBytes(arr);
  ```

- **TryWriteBytes:**  

  ```csharp
  Span<byte> dst = stackalloc byte[Snowflake.Size];
  id.TryWriteBytes(dst);
  ```

---

## Equality and Comparison

- Constant-time operators: `==`, `!=`, `<`, `>`, `<=`, `>=`
- Implements `IEquatable<Snowflake>`, `IComparable<Snowflake>`
- Recommended: always use `==` for equality test (reference type: use `is` as .NET best practice).

**Example:**

```csharp
var a = Snowflake.NewId(SnowflakeType.USER);
var b = Snowflake.NewId(SnowflakeType.USER);
if (a == b) { Console.WriteLine("Equal!"); }
int order = a.CompareTo(b); // -1, 0, or 1
```

---

## Static Factory Methods

| Method                                                              | Purpose                                         |
|---------------------------------------------------------------------|-------------------------------------------------|
| `Snowflake.NewId(UInt56 uInt56)`                                    | New from raw 56-bit integer                     |
| `Snowflake.NewId(uint value, ushort machineId, SnowflakeType type)` | New from components                             |
| `Snowflake.NewId(SnowflakeType type)`                               | Default: type + global machine id + timestamp   |
| `Snowflake.NewId(SnowflakeType type, ushort machineId)`             | Type + custom machine + timestamp               |
| `Snowflake.FromUInt56(UInt56)`                                      | Deserialize from raw 56-bit integer             |
| `Snowflake.FromBytes(byte[]) / FromBytes(Span<byte>)`               | Deserialize from bytes, little-endian           |
| `Snowflake.Empty`                                                   | All bits zero                                   |

---

## Best Practices

- Use `Snowflake.NewId(SnowflakeType.XYZ)` for automatic, unique, time-based IDs in most scenarios
- For serialization/deserialization, use `ToString()` for hex or `.ToByteArray()` for compact binary
- Always use `==`/`!=` for equality — methods are optimized for timing attacks resistance
- **Thread-safe:** Factory methods use internal locks for sequence resets; IDs are value types and safe for concurrent use.

---

## Example: Generate and Parse

```csharp
var newId = Snowflake.NewId(SnowflakeType.MESSAGE);

string hex = newId.ToString();
byte[] rawBytes = newId.ToByteArray();

var parsedId = Snowflake.FromUInt56(newId.ToUInt56());
var fromBytes = Snowflake.FromBytes(rawBytes);

Console.WriteLine(parsedId == newId); // True
```

---

## Note

- Default machine ID is loaded from configuration (`SnowflakeOptions` via `ConfigurationManager`).
- Type parameter (`SnowflakeType`) is application-defined (e.g., USER, ORDER, SYSTEM, etc).

---

## License

Licensed under the Apache License, Version 2.0.
