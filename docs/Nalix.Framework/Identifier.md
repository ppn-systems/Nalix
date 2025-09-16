# Identifier Struct Documentation

## Overview

The `Identifier` struct provides a compact, high-performance representation for unique identifiers in the Nalix framework. It encodes a 32-bit value, a 16-bit machine ID, and an 8-bit type into a 7-byte structure. This design enables efficient use as dictionary keys, supports fast serialization, and ensures memory layout consistency across different platforms. The struct also offers multiple string representations, including hexadecimal and Base36, for interoperability and readability.

---

## Table of Contents

- [Overview](#overview)
- [Code Structure and Details](#code-structure-and-details)
  - [Memory Layout](#memory-layout)
  - [Fields and Properties](#fields-and-properties)
  - [Constructors and Factory Methods](#constructors-and-factory-methods)
  - [State Checking Methods](#state-checking-methods)
  - [Equality and Comparison](#equality-and-comparison)
  - [Serialization and Formatting](#serialization-and-formatting)
  - [Parsing and Deserialization](#parsing-and-deserialization)
- [Usage](#usage)
- [Example](#example)
- [Notes & Security Considerations](#notes--security-considerations)
- [SOLID and DDD Principles](#solid-and-ddd-principles)

---

## Code Structure and Details

### Memory Layout

- 7 bytes in total, with explicit layout for platform consistency:
  - **Bytes 0-3:** `Value` (`uint`, little-endian)
  - **Bytes 4-5:** `MachineId` (`ushort`, little-endian)
  - **Byte 6:** `Type` (`byte`)

### Fields and Properties

- `Value`: Main identifier value (`uint`)
- `MachineId`: Machine-specific identifier (`ushort`)
- `_type`: Internal byte representing identifier type (`byte`)
- `Type`: Exposed as an `IdentifierType` enum
- `Size`: Constant (7 bytes)
- `Empty`: Static readonly property for the zero/default identifier

### Constructors and Factory Methods

- **Private Constructor:**  
  Initializes all fields explicitly.
- **NewId Methods:**  
  - `NewId(uint value, ushort machineId, IdentifierType type)`: Create from components.
  - `NewId(IdentifierType type, ushort machineId = 1)`: Create with random value.

### State Checking Methods

- `IsEmpty()`: Returns `true` if all fields are zero.
- `IsValid()`: Returns `true` if any field is non-zero.

### Equality and Comparison

- Implements `IEquatable<Identifier>` and overrides `Equals`, `GetHashCode`
- Supports comparison with both `Identifier` and `IIdentifier`
- Defines `==` and `!=` operators
- Uses a combined 64-bit value for fast comparisons and hashing

### Serialization and Formatting

- **Binary Format:**
  - `Format()`: Returns a 7-byte array.
  - `TryFormat(Span<byte>, out int)`: Writes to a provided span.
- **String Format:**
  - `ToBase36String()`: Base36 big-endian string (URL-safe, compact)
  - `ToHexString()`: Hexadecimal string
  - `ToString(bool useHexFormat)`: Conditional string format
  - `TryFormat(Span<char>, out byte)`: Format as Base36 into a span

### Parsing and Deserialization

- **FromBytes/FromByteArray:**  
  Construct from 7 bytes.
- **Parse/TryParse:**  
  From Base36 string, with input validation and error handling.

---

## Usage

- **Creating a new Identifier:**

  ```csharp
  var id = Identifier.NewId(12345u, 1001, IdentifierType.User);
  ```

- **Generating a random Identifier:**

  ```csharp
  var id = Identifier.NewId(IdentifierType.System);
  ```

- **Serialize to Base36 string:**

  ```csharp
  string base36 = id.ToBase36String();
  ```

- **Deserialize from Base36 string:**

  ```csharp
  var id = Identifier.Parse("1Z141B7Z8J2A");
  ```

- **Convert to/from byte array:**

  ```csharp
  byte[] bytes = id.Format();
  var id2 = Identifier.FromByteArray(bytes);
  ```

- **Check if identifier is empty or valid:**

  ```csharp
  if (id.IsEmpty()) { /* ... */ }
  if (id.IsValid()) { /* ... */ }
  ```

---

## Example

```csharp
using Nalix.Framework.Identity;
using Nalix.Common.Enums;

// Create a new identifier for a user
Identifier userId = Identifier.NewId(123456789u, 42, IdentifierType.User);

// Convert to Base36 string for URLs or storage
string base36 = userId.ToBase36String();

// Parse back from string
Identifier parsedId = Identifier.Parse(base36);

// Serialize to byte array for network transfer
byte[] raw = userId.Format();

// Deserialize from byte array
Identifier fromBytes = Identifier.FromByteArray(raw);

// Check for equality
bool same = userId == parsedId;

// Check if identifier is valid
if (userId.IsValid())
{
    // Do something with the valid identifier
}
```

---

## Notes & Security Considerations

- **Thread Safety:**  
  `Identifier` is a readonly struct, making it naturally thread-safe.
- **Random Value Generation:**  
  Uses `SecureRandom.NextUInt32()` for generating random values, which is cryptographically secure.
- **Equality:**  
  All equality checks use the combined value of all fields for accuracy and performance.
- **Serialization:**  
  Always ensure the byte array is exactly 7 bytes when parsing or serializing.
- **Type Safety:**  
  The Type is enforced as an enum for clarity and to avoid magic numbers.
- **Base36 Representation:**  
  Case-insensitive, but always outputs uppercase for consistency.

---

## SOLID and DDD Principles

- **Single Responsibility:**  
  The struct focuses only on identity representation, comparison, and serialization.
- **Open/Closed Principle:**  
  New identifier formats or types can be added via the `IdentifierType` enum without modifying the struct.
- **Liskov Substitution:**  
  Implements `IIdentifier` for interoperability and substitutability in domain-driven contexts.
- **Interface Segregation:**  
  Only exposes relevant identity methods through the `IIdentifier` contract.
- **Dependency Inversion:**  
  Relies on abstractions (`IIdentifier`), supporting DDD and future extensibility.

**Domain-Driven Design (DDD) Alignment:**  

- The identifier serves as a Value Object, which is immutable and compared based on value, not reference.  
- Strongly typed, prevents accidental misuse, and suitable for use as an aggregate root key or entity identifier.

---

## Other Notes

- Suitable for distributed systems (machine ID component).
- Encapsulation of memory layout ensures cross-platform consistency.
- Efficient for high-performance scenarios (hash tables, serialization, etc.).

---
