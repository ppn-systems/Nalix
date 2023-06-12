# Nalix.Cryptography Documentation

## Overview

**Nalix.Cryptography** is a high-performance .NET library providing secure and efficient cryptographic utilities. It supports symmetric and asymmetric encryption, authenticated encryption (AEAD), hashing, message authentication codes (MAC), integrity checks, padding schemes, and general security enhancements. Designed for developers requiring robust cryptographic solutions with minimal overhead.

## Key Features

### 🔐 **Cryptographic Algorithms**
- **Symmetric Encryption**: ChaCha20, Salsa20, Arc4, XTEA, Blowfish, Twofish, Speck
- **Asymmetric Encryption**: X25519, Ed25519, SRP6 (Secure Remote Password)
- **AEAD (Authenticated Encryption)**: ChaCha20-Poly1305 for confidentiality and integrity
- **Hashing**: SHA-1, SHA-224, SHA-256, SHA-384, SHA-512
- **MAC (Message Authentication)**: HMAC, Poly1305
- **Checksums**: CRC (8/16/32/64-bit), XOR256

### ⚡ **Performance Optimizations**
- **Unsafe Code**: High-performance memory operations
- **SIMD Instructions**: Vector operations where applicable
- **Cache-Friendly**: Optimized for CPU cache efficiency
- **Minimal Allocations**: Reduced garbage collection pressure

### 🛡️ **Security Features**
- **Timing Attack Resistant**: Constant-time operations
- **Memory Security**: Secure memory clearing
- **Authenticated Encryption**: Built-in integrity verification
- **Modern Algorithms**: Industry-standard cryptographic primitives

## Project Structure

```
Nalix.Cryptography/
├── Aead/                       # Authenticated Encryption with Associated Data
│   └── ChaCha20Poly1305.cs     # ChaCha20-Poly1305 AEAD implementation
├── Asymmetric/                 # Asymmetric (Public Key) Cryptography
│   ├── Ed25519.cs             # Ed25519 digital signatures
│   ├── X25519.cs              # X25519 key exchange
│   └── Srp6.cs                # Secure Remote Password protocol
├── Checksums/                  # Data integrity checksums
│   ├── Crc08.cs               # 8-bit CRC calculations
│   ├── Crc16.cs               # 16-bit CRC calculations
│   ├── Crc32.cs               # 32-bit CRC calculations
│   ├── Crc64.cs               # 64-bit CRC calculations
│   └── Xor256.cs              # XOR-based checksum
├── Hashing/                    # Cryptographic hash functions
│   ├── SHA.cs                 # Base SHA implementation
│   ├── SHA1.cs                # SHA-1 hash function
│   ├── SHA224.cs              # SHA-224 hash function
│   ├── SHA256.cs              # SHA-256 hash function
│   ├── SHA384.cs              # SHA-384 hash function
│   └── SHA512.cs              # SHA-512 hash function
├── Mac/                        # Message Authentication Codes
│   ├── Hmac.cs                # HMAC implementation
│   └── Poly1305.cs            # Poly1305 MAC
├── Symmetric/                  # Symmetric (Secret Key) Cryptography
│   ├── Block/                 # Block cipher algorithms
│   │   ├── Blowfish.cs        # Blowfish block cipher
│   │   ├── Speck.cs           # Speck block cipher
│   │   ├── Twofish.cs         # Twofish block cipher
│   │   └── Xtea.cs            # XTEA block cipher
│   └── Stream/                # Stream cipher algorithms
│       ├── Arc4.cs            # Arc4 stream cipher
│       ├── ChaCha20.cs        # ChaCha20 stream cipher
│       └── Salsa20.cs         # Salsa20 stream cipher
├── Padding/                    # Padding schemes for block ciphers
├── Security/                   # Security utilities and helpers
└── Internal/                   # Internal implementation details
```

## Core Components

### Authenticated Encryption (AEAD)

**ChaCha20-Poly1305** provides both confidentiality and integrity:

```csharp
public static class ChaCha20Poly1305
{
    public static byte[] Encrypt(
        ReadOnlySpan<byte> key,          // 32-byte key
        ReadOnlySpan<byte> nonce,        // 12-byte nonce
        ReadOnlySpan<byte> plaintext,    // Data to encrypt
        ReadOnlySpan<byte> aad = default // Additional authenticated data
    );
    
    public static bool Decrypt(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> aad,
        ReadOnlySpan<byte> tag,
        out byte[] plaintext
    );
}
```

### Stream Ciphers

**ChaCha20** - Modern, secure stream cipher:
```csharp
public class ChaCha20
{
    public ChaCha20(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce);
    public void EncryptBytes(ReadOnlySpan<byte> input, Span<byte> output);
    public void DecryptBytes(ReadOnlySpan<byte> input, Span<byte> output);
}
```

**Salsa20** - High-performance stream cipher:
```csharp
public class Salsa20
{
    public Salsa20(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce);
    public void Process(ReadOnlySpan<byte> input, Span<byte> output);
}
```

### Block Ciphers

**XTEA** - Efficient block cipher:
```csharp
public class Xtea
{
    public Xtea(ReadOnlySpan<byte> key);
    public void Encrypt(ReadOnlySpan<byte> plaintext, Span<byte> ciphertext);
    public void Decrypt(ReadOnlySpan<byte> ciphertext, Span<byte> plaintext);
}
```

**Blowfish** - Variable-length key block cipher:
```csharp
public class Blowfish
{
    public Blowfish(ReadOnlySpan<byte> key);
    public void EncryptBlock(ReadOnlySpan<byte> input, Span<byte> output);
    public void DecryptBlock(ReadOnlySpan<byte> input, Span<byte> output);
}
```

### Cryptographic Hashing

**SHA-256** - Secure hash algorithm:
```csharp
public class SHA256
{
    public static byte[] Hash(ReadOnlySpan<byte> data);
    public static void Hash(ReadOnlySpan<byte> data, Span<byte> hash);
}
```

**SHA-512** - Extended secure hash:
```csharp
public class SHA512
{
    public static byte[] Hash(ReadOnlySpan<byte> data);
    public static void Hash(ReadOnlySpan<byte> data, Span<byte> hash);
}
```

### Message Authentication Codes

**HMAC** - Hash-based message authentication:
```csharp
public class Hmac
{
    public Hmac(ReadOnlySpan<byte> key, HashAlgorithm hashAlgorithm);
    public byte[] ComputeHash(ReadOnlySpan<byte> data);
    public void ComputeHash(ReadOnlySpan<byte> data, Span<byte> hash);
}
```

**Poly1305** - High-speed MAC:
```csharp
public class Poly1305
{
    public Poly1305(ReadOnlySpan<byte> key);
    public void ComputeTag(ReadOnlySpan<byte> data, Span<byte> tag);
}
```

### Asymmetric Cryptography

**X25519** - Elliptic curve key exchange:
```csharp
public static class X25519
{
    public static byte[] GenerateKeyPair(out byte[] privateKey);
    public static byte[] ComputeSharedSecret(
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> publicKey
    );
}
```

**Ed25519** - Digital signatures:
```csharp
public static class Ed25519
{
    public static byte[] GenerateKeyPair(out byte[] privateKey);
    public static byte[] Sign(
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> privateKey
    );
    public static bool Verify(
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> signature,
        ReadOnlySpan<byte> publicKey
    );
}
```

## Usage Examples

### Authenticated Encryption (ChaCha20-Poly1305)

```csharp
using Nalix.Cryptography.Aead;

// Generate a random key and nonce
var key = new byte[32];
var nonce = new byte[12];
RandomNumberGenerator.Fill(key);
RandomNumberGenerator.Fill(nonce);

// Encrypt data
var plaintext = Encoding.UTF8.GetBytes("Hello, World!");
var ciphertext = ChaCha20Poly1305.Encrypt(key, nonce, plaintext);

// Decrypt data
if (ChaCha20Poly1305.Decrypt(key, nonce, ciphertext, out var decrypted))
{
    var message = Encoding.UTF8.GetString(decrypted);
    Console.WriteLine($"Decrypted: {message}");
}
```

### Stream Cipher (ChaCha20)

```csharp
using Nalix.Cryptography.Symmetric.Stream;

var key = new byte[32];
var nonce = new byte[12];
RandomNumberGenerator.Fill(key);
RandomNumberGenerator.Fill(nonce);

using var cipher = new ChaCha20(key, nonce);

var plaintext = Encoding.UTF8.GetBytes("Secret message");
var ciphertext = new byte[plaintext.Length];

cipher.EncryptBytes(plaintext, ciphertext);
```

### Cryptographic Hashing

```csharp
using Nalix.Cryptography.Hashing;

var data = Encoding.UTF8.GetBytes("Data to hash");

// SHA-256 hash
var hash256 = SHA256.Hash(data);

// SHA-512 hash
var hash512 = SHA512.Hash(data);

Console.WriteLine($"SHA-256: {Convert.ToHexString(hash256)}");
Console.WriteLine($"SHA-512: {Convert.ToHexString(hash512)}");
```

### Message Authentication

```csharp
using Nalix.Cryptography.Mac;

var key = new byte[32];
var message = Encoding.UTF8.GetBytes("Message to authenticate");
RandomNumberGenerator.Fill(key);

// HMAC with SHA-256
using var hmac = new Hmac(key, HashAlgorithm.SHA256);
var tag = hmac.ComputeHash(message);

// Poly1305 MAC
using var poly1305 = new Poly1305(key);
var polyTag = new byte[16];
poly1305.ComputeTag(message, polyTag);
```

### Key Exchange (X25519)

```csharp
using Nalix.Cryptography.Asymmetric;

// Generate key pairs for both parties
var alicePublic = X25519.GenerateKeyPair(out var alicePrivate);
var bobPublic = X25519.GenerateKeyPair(out var bobPrivate);

// Compute shared secrets
var aliceShared = X25519.ComputeSharedSecret(alicePrivate, bobPublic);
var bobShared = X25519.ComputeSharedSecret(bobPrivate, alicePublic);

// Shared secrets should be identical
Console.WriteLine($"Shared secret match: {aliceShared.SequenceEqual(bobShared)}");
```

### Digital Signatures (Ed25519)

```csharp
using Nalix.Cryptography.Asymmetric;

// Generate signing key pair
var publicKey = Ed25519.GenerateKeyPair(out var privateKey);

// Sign a message
var message = Encoding.UTF8.GetBytes("Message to sign");
var signature = Ed25519.Sign(message, privateKey);

// Verify signature
var isValid = Ed25519.Verify(message, signature, publicKey);
Console.WriteLine($"Signature valid: {isValid}");
```

## Security Considerations

### Best Practices

1. **Key Management**
   - Use cryptographically secure random number generators
   - Store keys securely (consider hardware security modules)
   - Rotate keys regularly
   - Clear sensitive data from memory after use

2. **Algorithm Selection**
   - Prefer modern algorithms (ChaCha20, Ed25519, SHA-256/512)
   - Avoid deprecated algorithms (MD5, SHA-1 for security purposes)
   - Use authenticated encryption (AEAD) when possible

3. **Implementation Security**
   - Validate all inputs
   - Use constant-time operations for sensitive comparisons
   - Implement proper error handling
   - Consider side-channel attacks

### Common Pitfalls

1. **Nonce Reuse**: Never reuse nonces with the same key
2. **Weak Random Numbers**: Use cryptographically secure RNGs
3. **Timing Attacks**: Use constant-time comparison functions
4. **Memory Management**: Clear sensitive data after use

## Performance Benchmarks

### Encryption Performance (MB/s)
- **ChaCha20**: ~2000 MB/s
- **Salsa20**: ~1800 MB/s
- **XTEA**: ~800 MB/s
- **Blowfish**: ~600 MB/s

### Hashing Performance (MB/s)
- **SHA-256**: ~1500 MB/s
- **SHA-512**: ~1200 MB/s
- **SHA-1**: ~2000 MB/s

*Benchmarks are approximate and depend on hardware and workload.*

## Dependencies

- **.NET 9.0**: Modern C# 13 features and performance improvements
- **Nalix.Common**: Core utilities and interfaces
- **Nalix**: Base library components
- **System.Security.Cryptography**: .NET cryptographic primitives

## Thread Safety

- **Algorithm Instances**: Not thread-safe (use separate instances per thread)
- **Static Methods**: Thread-safe
- **Key Generation**: Thread-safe
- **Cryptographic Operations**: Thread-safe when using separate instances

## Error Handling

### Common Exceptions
- `CryptographicException`: Invalid keys, corrupted data, verification failures
- `ArgumentException`: Invalid parameter values or sizes
- `InvalidOperationException`: Incorrect algorithm state

### Error Recovery
```csharp
try
{
    var result = ChaCha20Poly1305.Encrypt(key, nonce, plaintext);
}
catch (CryptographicException ex)
{
    // Handle cryptographic errors
    logger.Error($"Encryption failed: {ex.Message}");
}
```

## Testing and Validation

### Test Vectors
- All algorithms include comprehensive test vectors
- Compliance with RFC specifications
- Cross-platform validation

### Security Testing
- Constant-time operation verification
- Memory clearing validation
- Side-channel resistance testing

## API Reference

### Symmetric Encryption
- `ChaCha20`: Modern stream cipher
- `Salsa20`: High-performance stream cipher
- `XTEA`: Lightweight block cipher
- `Blowfish`: Variable-key block cipher

### Asymmetric Cryptography
- `X25519`: Elliptic curve key exchange
- `Ed25519`: Digital signatures
- `SRP6`: Secure remote password protocol

### Hashing and MAC
- `SHA256`, `SHA512`: Cryptographic hash functions
- `HMAC`: Hash-based message authentication
- `Poly1305`: High-speed MAC

### AEAD
- `ChaCha20Poly1305`: Authenticated encryption

## Version History

### Version 1.4.3 (Current)
- Initial release of Nalix.Cryptography
- Comprehensive cryptographic algorithm suite
- High-performance implementations
- Security-focused design
- Modern C# 13 optimizations

## Contributing

When contributing to Nalix.Cryptography:

1. **Security First**: All implementations must be secure
2. **Performance**: Maintain high-performance characteristics
3. **Standards Compliance**: Follow cryptographic standards (RFCs)
4. **Testing**: Comprehensive test coverage with test vectors
5. **Documentation**: Clear security considerations and usage examples

## License

Nalix.Cryptography is licensed under the Apache License, Version 2.0.