# Csprng — Cryptographically Secure Random Numbers

A high-performance, thread-safe static class for generating cryptographically strong random numbers for .NET.  
Automatically uses the OS CSPRNG (e.g., BCryptGenRandom, getrandom, SecRandomCopyBytes) and falls back to a high-quality PRNG if needed.

- **Namespace:** `Nalix.Framework.Random`
- **Class:** `Csprng`
- **Purpose:** Provides secure random bytes, integers, doubles, and nonces for cryptography and security.

---

## Features

- Secure by default (uses system CSPRNG with fallback, no manual seeding)
- Static, thread-safe, zero-allocation (prefers Span over array where possible)
- Multi-platform (Windows, Linux, macOS, iOS, ...)
- Fast fallback (Xoshiro256++-based) if OS CSPRNG is unavailable

---

## Usage

### Get Secure Random Bytes

**To fill a buffer (recommended for best performance):**

```csharp
Span<byte> buffer = stackalloc byte[32];
Csprng.Fill(buffer);
```

**To get a new random byte array:**

```csharp
byte[] data = Csprng.GetBytes(32);
```

### Generate Secure Random Numbers

**Random 32-bit/64-bit unsigned integer:**

```csharp
uint val32 = Csprng.NextUInt32();
ulong val64 = Csprng.NextUInt64();
```

**Random integer in a specific range (unbiased, [min, max)):**

```csharp
int random = Csprng.GetInt32(100, 1000); // e.g. between 100 and 999
int zeroToMax = Csprng.GetInt32(10);     // between 0 and 9
```

**Random double in [0.0, 1.0):**

```csharp
double value = Csprng.NextDouble();
```

### Generate Secure Nonce

For AEAD (e.g., AES-GCM, ChaCha20), secure tokens, etc.:

```csharp
byte[] nonce = Csprng.CreateNonce();       // 12 bytes (96 bits, default)
byte[] nonce16 = Csprng.CreateNonce(16);   // 16 bytes
```

---

## API Reference

| Method                           | Returns  | Purpose                                                |
|----------------------------------|----------|--------------------------------------------------------|
| `Fill(Span<byte> data)`          | `void`   | Fill a buffer with secure random bytes (no allocation) |
| `CreateNonce(int length = 12)`   | `byte[]` | Generate secure nonce (default 12 bytes for AEAD)      |
| `GetBytes(int length)`           | `byte[]` | New byte array of secure random bytes                  |
| `GetInt32(int min, int max)`     | `int`    | Random int in \[min, max)                              |
| `GetInt32(int max)`              | `int`    | Random int in \[0, max)                                |
| `NextBytes(byte[] buffer)`       | `void`   | Fill array with random bytes                           |
| `NextBytes(Span<byte> buffer)`   | `void`   | Fill Span with random bytes (prefer for performance)   |
| `NextUInt32()`                   | `uint`   | Random 32-bit unsigned integer                         |
| `NextUInt64()`                   | `ulong`  | Random 64-bit unsigned integer                         |
| `NextDouble()`                   | `double` | Random double in [0.0, 1.0)                            |

---

## Best Practices

- Use **Fill(Span<`byte`>)** or **NextBytes(Span<`byte`>)** in performance-critical code (no allocation).
- Use **CreateNonce()** for generating nonces (never reuse a nonce with same key!).
- For secret keys, tokens, and session IDs: always use Csprng, never OsRandom.
- For general shuffling or “non-secret” randomness, see OsRandom class.

---

## Security Notes

- Prefers system-level random source (never falls back to insecure algorithm unless OS random is unavailable).
- All operations are thread-safe.
- Suitable for secrets, private tokens, cryptographic keys, nonce/IV.
- **DO NOT use OsRandom for security-sensitive or cryptographic purposes; always choose Csprng.**

---

## Example

```csharp
// Generate secret key (256 bits)
byte[] key = Csprng.GetBytes(32);
// Fill buffer directly (no allocation)
Span<byte> buf = stackalloc byte[64];
Csprng.Fill(buf);
// Secure nonce (96 bits)
byte[] nonce = Csprng.CreateNonce();
// Random int in [10, 100)
int n = Csprng.GetInt32(10, 100);
```

---

## License

Licensed under the Apache License, Version 2.0.
