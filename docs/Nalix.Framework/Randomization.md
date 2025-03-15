# Nalix.Framework.Randomization

## Overview

The `Nalix.Framework.Randomization` namespace provides high-performance, flexible, and secure random number generators for .NET applications. It includes both fast pseudo-random generators (for general application logic and algorithms) and cryptographically secure generators (for security-sensitive contexts such as keys, tokens, and nonces). The library is designed for efficiency, thread safety, and compatibility with modern C#.

---

## Table of Contents

- [Overview](#overview)
- [Components](#components)
  - [MwcRandom](#mwcrandom)
  - [Rand32](#rand32)
  - [SecureRandom](#securerandom)
- [Usage](#usage)
- [Examples](#examples)
- [Notes & Security](#notes--security)
- [SOLID/DDD Principles](#solidddd-principles)

---

## Components

### MwcRandom

**Description:**  
An abstract base class implementing the Multiply-With-Carry (MWC) random number generation algorithm.  

- Maintains a 64-bit internal state.
- Generates 32-bit random numbers with a very large period (~2^63).
- Provides methods to get random numbers in various ranges, random floats/doubles, and fill buffers.

**Key API:**

- `SetSeed(uint seed)`: Set the generator seed.
- `Get()`: Get a random uint in [0, 2^32-1].
- `Get(uint max)`: Get a random uint in [0, max).
- `Get(uint min, uint max)`: Get a random uint in [min, max).
- `GetFloat()`: Get a random float in [0.0f, 1.0f).
- `GetDouble()`: Get a random double in [0.0, 1.0).
- `NextBytes(Span<byte> buffer)`: Fill buffer with random bytes.

---

### Rand32

**Description:**  
A .NET-style, easy-to-use wrapper for fast random number generation.  

- Exposes an API similar to `System.Random`, but is based on a custom high-performance RNG.
- Suitable for general randomized logic, non-crypto shuffling, choosing, and probabilistic operations.

**Key API:**

- `Next()`: Random int in [0, int.MaxValue].
- `Next(int max)`: Random int in [0, max).
- `Next(int min, int max)`: Random int in [min, max).
- `NextFloat()`, `NextDouble()`: Random float/double in [0, 1).
- `NextPct(int pct)`, `NextProbability(double probability)`: Random boolean based on probability.
- `ShuffleList<T>(IList<T> list)`, `ShuffleSpan<T>(Span<T> span)`: Fisher-Yates shuffling.
- `Choose<T>(IList<T> list)`, `Choose<T>(ReadOnlySpan<T> span)`: Random item selection.
- `NextBytes(Span<byte> buffer)`: Fill buffer with random bytes.

---

### SecureRandom

**Description:**  
A thread-safe, cryptographically strong random generator based on the Xoshiro256++ algorithm, using additional entropy sources.  

- Suitable for generating keys, nonces, IVs, and any security-sensitive randomness.
- Not a direct CSPRNG replacement, but robust for most .NET application needs.
- Provides static, easy-to-use methods for secure randomness.

**Key API:**

- `CreateKey(int length)`: Generate a key (byte array) of specified length.
- `CreateNonce(int length = 12)`: Generate a secure nonce.
- `CreateIV(int length = 16)`: Generate a secure IV.
- `Fill(Span<byte> data)`, `NextBytes(byte[] buffer)`, `NextBytes(Span<byte> buffer)`: Fill buffer with secure random bytes.
- `NextUInt32()`, `NextUInt64()`: Secure random integers.
- `GetInt32(int min, int max)`: Secure random int in [min, max).
- `NextDouble()`: Secure random double in [0, 1).
- `Reseed()`: Force reseeding from entropy sources.

---

## Usage

### Generating a random integer (fast, non-crypto)

```csharp
var rng = new Rand32(12345);
int value = rng.Next();        // [0, int.MaxValue)
int rangeVal = rng.Next(100);  // [0, 100)
```

### Random float/double

```csharp
float f = rng.NextFloat();          // [0.0f, 1.0f)
double d = rng.NextDouble(10.0);   // [0.0, 10.0)
```

### Random shuffle

```csharp
var list = new List<int> {1, 2, 3, 4, 5};
rng.ShuffleList(list);
```

### Random choice

```csharp
int randomItem = rng.Choose(list);
```

### Secure random bytes/key

```csharp
byte[] key = SecureRandom.CreateKey(32); // 256-bit key
byte[] nonce = SecureRandom.CreateNonce(); // 12-byte nonce
SecureRandom.NextBytes(key); // Fill key with secure random bytes
```

### Secure random int/double

```csharp
int secureInt = SecureRandom.GetInt32(0, 1000);
double secureDouble = SecureRandom.NextDouble();
```

---

## Examples

```csharp
// Fast generator for general logic
Rand32 rng = new Rand32();
int v1 = rng.Next(10, 100);

// Cryptographically secure generator for keys/nonces
byte[] aesKey = SecureRandom.CreateKey(32);
byte[] iv = SecureRandom.CreateIV(16);

// Shuffle a list of strings
var names = new List<string> { "Alice", "Bob", "Carol" };
rng.ShuffleList(names);

// Randomly pick a name
string chosen = rng.Choose(names);

// Generate a secure random integer in a range
int secureRand = SecureRandom.GetInt32(100, 1000);
```

---

## Notes & Security

- **Use the appropriate random generator for your needs:**
  - Use `Rand32`/`MwcRandom` for fast, non-security-critical randomness (game logic, shuffling, etc.).
  - Use `SecureRandom` for all cryptographic or security-sensitive scenarios.
- All generators are thread-safe, but `SecureRandom` offers extra safety for highly concurrent scenarios and uses thread-local state.
- Buffer filling functions are efficient and use block operations where possible.
- Avoid using these APIs for security purposes unless you fully understand their guarantees. For highest security (e.g. generating passwords, cryptographic secrets), always prefer `SecureRandom`.
- Reseeding is automatic, but you can explicitly call `Reseed()` if additional entropy is needed.

---

## SOLID/DDD Principles

- **Single Responsibility:** Each class focuses on a single type of random generation (fast, cryptographic).
- **Open/Closed:** Algorithms and seeding logic can be extended with new classes without modifying existing code.
- **Liskov Substitution:** Random generators can be used polymorphically for different randomness needs.
- **Interface Segregation:** APIs are separated by generator type (general vs secure).
- **Dependency Inversion:** Consumers depend on abstractions (interfaces or base classes) for testability and DDD alignment.

---

## Additional Notes

- Designed for modern C#/.NET and compatible with Visual Studio and VS Code.
- All APIs use clear, explicit naming and follow .NET conventions.
- Advanced performance optimizations (e.g. span, block copy, bitwise ops) are used for minimal overhead.
- Suitable for both domain-driven design (DDD) and general .NET application needs.

---
