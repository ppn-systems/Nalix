# ChaCha20 — Modern Stream Cipher (RFC 7539) for .NET

**ChaCha20** is a high-speed, secure stream cipher, widely used in TLS/SSL, WireGuard, encrypted messaging, and modern protocols.  
This implementation provides high-performance, allocation-free, SIMD-capable encryption and decryption with zero heap churn and support for large/streaming data.

- **Namespace:** `Nalix.Shared.Security.Symmetric`
- **Class:** `ChaCha20` (`ref struct`)
- **Spec:** [RFC 7539](https://tools.ietf.org/html/rfc7539)

---

## Features

- Allocation-free (`ref struct`), stack-based state & keystream buffer
- 256-bit key (32 bytes); 96-bit nonce (12 bytes; required by modern ChaCha20)
- Block counter (32-bit initial; can process many GBs of data per stream)
- Explicit SIMD mode: auto-detects CPU support for Vector128/256/512 where available
- Symmetric: `Encrypt` and `Decrypt` are identical (XOR with stream)
- In-place as well as two-buffer APIs, plus static one-shot mode

---

## API Overview

| Method                                 | Description                                      |
|----------------------------------------|--------------------------------------------------|
| `EncryptBytes(output, input, ...)`     | Encrypts to output buffer (array/span)           |
| `Encrypt(input, numBytes)`             | Encrypt to new buffer, returns ciphertext        |
| `Encrypt(src, dst)`                    | Span-to-Span encrypt, returns bytes written      |
| `Decrypt...`                           | Identical to Encrypt (ChaCha20 is symmetric)     |
| `EncryptInPlace(buffer)`               | In-place encryption (overwrite input)            |
| `Static Encrypt/Decrypt`               | One-shot using key, nonce, counter               |
| `Clear()`                              | Overwrite state after use (erases key material)  |

**Parameters:**

- `key` — 32 bytes (256 bits), required for all operations
- `nonce` — 12 bytes (96 bits), must be unique for each message/key!
- `counter` — 32-bit start block (usually 0, can be chunked for parallel processing)

---

## Typical Usage

### One-shot encrypt/decrypt

```csharp
using Nalix.Shared.Security.Symmetric;

byte[] key = ...;       // 32 bytes (CSPRNG)
byte[] nonce = ...;     // 12 bytes (Nonce: must be unique, per RFC!)
uint counter = 0;       // Start block (default is 0)
byte[] data = ...;      // Any binary message

// Encrypt
byte[] encrypted = ChaCha20.Encrypt(key, nonce, counter, data);

// Decrypt (identical)
byte[] decrypted = ChaCha20.Decrypt(key, nonce, counter, encrypted);
```

---

### Allocation-Free, Span/Buffer Use

```csharp
Span<byte> input = ...;
Span<byte> output = stackalloc byte[input.Length];

var cipher = new ChaCha20(key, nonce, 0);
cipher.Encrypt(input, output); // Out-of-place, allocation free

// In-place encrypt
cipher.EncryptInPlace(input);  // input is now encrypted
```

### Chunked File/Stream Processing

For large streams, increment the block counter for each chunk.

```csharp
var cipher = new ChaCha20(key, nonce, 0);
for (long offset = 0; offset < total; offset += blockLen)
{
    cipher.EncryptBytes(outBufChunk, inBufChunk, actualLen);
    // Block counter will advance internally
}
```

---

## SIMD, Performance, and Thread Safety

- **SIMD support**: Detects and utilizes Vector512/256/128 if supported by CPU.
- **ref struct** means the same `ChaCha20` instance is not thread-safe — create per thread/stream.
- For optimal throughput, prefer passing large buffers and process in block multiples (64 bytes/block).
- Always call `.Clear()` if you're using long-lived instances (overwrites secret keys in memory).

---

## Security Notes

- **Nonce MUST be unique per key!**  
  Never reuse (key, nonce, counter) combination for different messages; reuse instantly breaks confidentiality.
- Use random (CSPRNG) keys and nonces.
- For authenticated encryption (AEAD), pair ChaCha20 with a MAC (e.g., Poly1305).
- Max security: clear (wipe) key material as soon as possible (`Clear()`).

---

## Error Handling

- Throws `ArgumentNullException` or `ArgumentOutOfRangeException` for invalid buffer sizes, key/nonce length
- Throws `ObjectDisposedException` if used after `.Clear()`
- All buffer parameters checked; safe for critical code

---

## Parameters

| Name      | Size      | Description                                          |
|-----------|-----------|------------------------------------------------------|
| `key`     | 32 bytes  | Secret key (random, NEVER re-use between sessions)   |
| `nonce`   | 12 bytes  | Nonce (unique per message)                           |
| `counter` | uint32    | Initial block counter (for long streams, chunking)   |
| `SimdMode`| enum      | AUTO_DETECT (default), V128, V256, V512, NONE        |

---

## Example: One-shot Encrypt & Decrypt

```csharp
// Generate key & nonce securely
byte[] key = ...;    // length == 32
byte[] nonce = ...;  // length == 12
byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("Hello, ChaCha!");

byte[] cipher = ChaCha20.Encrypt(key, nonce, 0, plaintext);

byte[] plainAgain = ChaCha20.Decrypt(key, nonce, 0, cipher);
// plainAgain == plaintext
```

---

## Best Practices

- For maximum security, never re-use (key, nonce, counter) across messages/files
- Use .NET's CSPRNG (or Csprng from your lib) for keys and nonces
- Use the buffer/Span API for high-performance, zero-allocation networking, file IO, and real-time protocols
- For thread safety, create a new `ChaCha20` per stream/consumer
- After use, always call `.Clear()` to wipe sensitive data if instance is long-lived or contains secrets

---

## Compliance

- Implements [RFC 7539](https://tools.ietf.org/html/rfc7539).
- Works with modern AEAD constructions (Poly1305, AEAD_CHACHA20_POLY1305).

---

## License

Licensed under the Apache License, Version 2.0.
