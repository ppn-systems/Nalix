# Random

`Nalix.Environment` provides high-performance, cryptographically strong random number generation through the `Csprng` static utility.

## Source Mapping

- `src/Nalix.Environment/Random/Csprng.cs`
- `src/Nalix.Environment/Random/OsCsprng.cs`
- `src/Nalix.Environment/Random/OsRandom.cs`

## Why use Csprng?

While `.NET` provides `System.Random`, it is not thread-safe and not cryptographically secure by default. `Csprng` is a thread-safe, static utility that uses OS-level cryptographic providers (like BCrypt on Windows or getrandom on Linux) to ensure that generated values are suitable for security-sensitive operations.

## Key API Members

### `Csprng` (Static Class)

The primary entry point for generating random data.

- `Fill(Span<byte> data)`: Fills a span with cryptographically strong random bytes.
- `GetBytes(int length)`: Returns a new byte array of the specified length filled with random data.
- `CreateNonce(int length = 12)`: Generates a secure nonce, typically 96 bits (12 bytes).
- `GetInt32(int min, int max)`: Gets a random integer in the range `[min, max)` using unbiased rejection sampling.
- `GetInt32(int max)`: Shortcut for `GetInt32(0, max)`.
- `NextUInt32()` / `NextUInt64()`: Generates random unsigned integers.
- `NextDouble()`: Returns a random double in the range `[0.0, 1.0)`.

## Usage Example

```csharp
using Nalix.Environment.Random;

// Generate a random port in a safe range
int port = Csprng.GetInt32(49152, 65535);

// Fill a buffer for a handshake challenge
byte[] challenge = Csprng.GetBytes(32);

// Generate a nonce for encryption
byte[] nonce = Csprng.CreateNonce();
```

## Performance and Safety

- **Thread-Safety**: All methods are thread-safe and optimized for concurrent access.
- **No-Throw Static Constructor**: The internal state is initialized safely; if the OS CSPRNG is unavailable, it falls back to a high-quality `Xoshiro256++` pseudo-random generator to avoid killing the process.
- **Zero-Allocation Options**: Use `Fill(Span<byte>)` or `NextBytes(Span<byte>)` to avoid unnecessary heap allocations in hot paths.

## Related APIs

- [Session Contracts](../abstractions/session-contracts.md)
- [Clock](./clock.md)
