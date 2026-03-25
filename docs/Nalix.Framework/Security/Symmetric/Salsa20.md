# Salsa20 — Secure, Fast Stream Cipher for .NET

**Salsa20** is a high-speed, modern stream cipher (by Daniel J. Bernstein) designed for cryptographic secrecy, wireless security, and low-latency networking.  
This API provides allocation-free, convenient encryption/decryption methods for classic Salsa20 (64-bit nonce, 128/256-bit key).

- **Namespace:** `Nalix.Shared.Security.Symmetric`
- **Class:** `Salsa20` (static)

---

## Features

- Stream cipher: XORs keystream with plaintext or ciphertext for encryption/decryption
- Supports both 128-bit (16-byte) or 256-bit (32-byte) keys
- 64-bit (8-byte) nonce (classic Salsa20 spec)
- Block counter parameter allows for seeking or chunked streaming
- Symmetric API: same method for both encrypt & decrypt
- Allocation-free, fast, and suitable for all .NET platforms

---

## Typical Usage

### Encrypt a message

```csharp
using Nalix.Shared.Security.Symmetric;

byte[] key    = ...; // 16 or 32 bytes (128/256 bits)
byte[] nonce  = ...; // 8 bytes (MUST be unique per key!)
ulong counter = 0;   // usually zero for new stream
byte[] plain  = ...; // data to encrypt

// One-shot, returns new array:
byte[] cipher = Salsa20.Encrypt(key, nonce, counter, plain);
```

Or, encrypt directly into a buffer (zero allocation):

```csharp
Span<byte> plain = ...;
Span<byte> cipher = stackalloc byte[plain.Length];
Salsa20.Encrypt(key, nonce, counter, plain, cipher);
// 'cipher' holds the ciphertext
```

### Decrypt a message

**Salsa20 is symmetric. Use the same method:**

```csharp
byte[] decrypted = Salsa20.Decrypt(key, nonce, counter, cipher);
// Or into a buffer:
Salsa20.Decrypt(key, nonce, counter, cipher, plainBuf);
```

---

## Parameter Reference

| Param         | Length        | Description                                           |
|---------------|---------------|-------------------------------------------------------|
| `key`         | 16 or 32 B    | Secret key (128 or 256 bits). Must be random & secret.|
| `nonce`       | 8 B           | Public nonce, unique per (key, counter) stream.       |
| `counter`     | 8 B (ulong)   | 64-bit initial block count (set to 0 for new streams) |
| `plaintext`   | any           | Data to encrypt                                       |
| `ciphertext`  | any           | Data to decrypt                                       |

> **Never reuse (key, nonce, counter) tuple!** Repeated keystreams destroy security.

---

## API Overview

| Method                                                                | Purpose                                             |
|-----------------------------------------------------------------------|-----------------------------------------------------|
| `Encrypt(key, nonce, counter, plaintext)`                             | Encrypts bytes, returns new ciphertext array        |
| `Encrypt(key, nonce, counter, plaintext, ciphertext)`                 | Encrypts into provided buffer, returns bytes written|
| `Decrypt(key, nonce, counter, ciphertext)`                            | Decrypts bytes, returns new plaintext array         |
| `Decrypt(key, nonce, counter, ciphertext, plaintext)`                 | Decrypts into provided buffer                       |

---

## Error Handling

- Throws `ArgumentException` if key is not 16/32 bytes or nonce != 8 bytes.
- Throws if output buffer is too small.
- Performs all memory and buffer-slice checks (safe for critical systems).

---

## Security & Best Practices

- NEVER reuse `(key, nonce, counter)` for two different messages!
- For file streaming, increment `counter` for each chunk.
- Consider using a MAC (e.g., Poly1305) if you need message authentication/integrity.
- Do *not* use classic Salsa20 if your protocol requires a 24-byte nonce; use XSalsa20 for that case.

---

## Example: Encrypt + Decrypt Roundtrip

```csharp
byte[] key = new byte[32]; // Fill with CSPRNG
byte[] nonce = new byte[8]; // CSPRNG, unique
ulong counter = 0;
byte[] data = System.Text.Encoding.UTF8.GetBytes("secret!");

// Encrypt
byte[] enc = Salsa20.Encrypt(key, nonce, counter, data);

// Decrypt (with same param)
byte[] dec = Salsa20.Decrypt(key, nonce, counter, enc);
// dec == data
```

---

## Advanced Usage

For long files or streaming, process sequentially using the `counter`:

```csharp
// For every 4K block, increment counter:
for (ulong ctr = 0; offset < total; ctr++)
{
    int chunk = Math.Min(blockLen, data.Length - offset);
    Salsa20.Encrypt(key, nonce, ctr, data.Slice(offset, chunk), outBuf.Slice(offset, chunk));
    offset += chunk;
}
```

---

## Further Reading

- [Salsa20 Specification](https://cr.yp.to/snuffle/spec.pdf)  
- [Daniel J. Bernstein - Salsa20](https://cr.yp.to/snuffle.html)

---

## License

Licensed under the Apache License, Version 2.0.
