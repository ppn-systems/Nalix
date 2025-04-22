# Pbkdf2 Class Documentation

The `Pbkdf2` class represents a high-performance implementation of the PBKDF2 (Password-Based Key Derivation Function 2) algorithm. PBKDF2 is used to derive cryptographic keys from passwords in a secure manner. This implementation supports both HMAC-SHA1 and HMAC-SHA256 for the hashing process. This class is part of the `Notio.Cryptography.Hash` namespace.

## Namespace

```csharp
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
```

## Class Definition

### Summary

The `Pbkdf2` class provides methods for deriving cryptographic keys from passwords using the PBKDF2 algorithm with HMAC-SHA1 or HMAC-SHA256. It supports specifying the salt, number of iterations, length of the derived key, and the hash algorithm.

```csharp
namespace Notio.Cryptography.Hash
{
    /// <summary>
    /// High-performance implementation of PBKDF2 (Password-Based Key Derivation Function 2).
    /// </summary>
    public sealed class Pbkdf2 : IDisposable
    {
        // Class implementation...
    }
}
```

## Properties

This class does not expose any public properties.

## Methods

### Constructor

```csharp
public Pbkdf2(byte[] salt, int iterations, int keyLength, HashAlgorithmType hashType = HashAlgorithmType.Sha1);
```

- **Description**: Initializes a new instance of the `Pbkdf2` class.
- **Parameters**:
  - `salt`: The salt to use for the key derivation.
  - `iterations`: The number of iterations for the key derivation.
  - `keyLength`: The length of the derived key in bytes.
  - `hashType`: The hash algorithm to use (default is `Sha1`).
- **Exceptions**:
  - `ArgumentException`: Thrown if `salt` is null or empty.
  - `ArgumentOutOfRangeException`: Thrown if `iterations` or `keyLength` is less than or equal to zero.

### DeriveKey-string

```csharp
public byte[] DeriveKey(string password);
```

- **Description**: Derives a key from the given password.
- **Parameters**:
  - `password`: The password to derive the key from.
- **Returns**: The derived key as a byte array.
- **Exceptions**:
  - `ArgumentException`: Thrown if `password` is null or empty.

### DeriveKey-byte

```csharp
public byte[] DeriveKey(ReadOnlySpan<byte> passwordBytes);
```

- **Description**: Derives a key from the given password bytes.
- **Parameters**:
  - `passwordBytes`: The password bytes to derive the key from.
- **Returns**: The derived key as a byte array.
- **Exceptions**:
  - `ArgumentException`: Thrown if `passwordBytes` is empty.

### DeriveKeyUsingHmac

```csharp
private static byte[] DeriveKeyUsingHmac(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt, int iterations, int keyLength, int hashLength);
```

- **Description**: Core PBKDF2 implementation using the selected hash algorithm.
- **Parameters**:
  - `password`: The password as a byte array.
  - `salt`: The salt as a byte array.
  - `iterations`: The number of iterations.
  - `keyLength`: The length of the derived key in bytes.
  - `hashLength`: The length of the hash in bytes.
- **Returns**: The derived key as a byte array.

### ComputeBlock

```csharp
private static void ComputeBlock(ReadOnlySpan<byte> password, ReadOnlySpan<byte> saltWithIndex, int iterations, Span<byte> outputBlock, int hashLength);
```

- **Description**: Computes a single PBKDF2 block.
- **Parameters**:
  - `password`: The password as a byte array.
  - `saltWithIndex`: The salt combined with the block index as a byte array.
  - `iterations`: The number of iterations.
  - `outputBlock`: The output block as a byte array.
  - `hashLength`: The length of the hash in bytes.

### ComputeHmac

```csharp
private static void ComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message, Span<byte> output, int hashLength);
```

- **Description**: Computes the HMAC based on the current hash type.
- **Parameters**:
  - `key`: The key as a byte array.
  - `message`: The message as a byte array.
  - `output`: The output as a byte array.
  - `hashLength`: The length of the hash in bytes.

### ComputeHmacSha1

```csharp
private static void ComputeHmacSha1(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message, Span<byte> output);
```

- **Description**: Computes HMAC-SHA1 of a message using the specified key.
- **Parameters**:
  - `key`: The key as a byte array.
  - `message`: The message as a byte array.
  - `output`: The output as a byte array.

### ComputeHmacSha256

```csharp
private static void ComputeHmacSha256(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message, Span<byte> output);
```

- **Description**: Computes HMAC-SHA256 of a message using the specified key.
- **Parameters**:
  - `key`: The key as a byte array.
  - `message`: The message as a byte array.
  - `output`: The output as a byte array.

### ConstantTimeEquals

```csharp
public static bool ConstantTimeEquals(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b);
```

- **Description**: Compares two byte arrays in constant time to prevent timing attacks.
- **Parameters**:
  - `a`: The first byte array.
  - `b`: The second byte array.
- **Returns**: True if the arrays are equal; otherwise, false.

### Dispose

```csharp
public void Dispose();
```

- **Description**: Releases all resources used by the `Pbkdf2` object.
- **Remarks**: This method clears sensitive data from memory and marks the instance as disposed.

## Example Usage

Here's a basic example of how to use the `Pbkdf2` class:

```csharp
using Notio.Cryptography.Hash;

public class Example
{
    public void DeriveKeyExample()
    {
        byte[] salt = Encoding.UTF8.GetBytes("some_salt");
        int iterations = 10000;
        int keyLength = 32; // 32 bytes = 256 bits

        Pbkdf2 pbkdf2 = new Pbkdf2(salt, iterations, keyLength, Pbkdf2.HashAlgorithmType.Sha256);
        byte[] derivedKey = pbkdf2.DeriveKey("password123");

        Console.WriteLine(BitConverter.ToString(derivedKey).Replace("-", "").ToLower());
    }
}
```

## Remarks

The `Pbkdf2` class is designed to provide a secure and efficient implementation of the PBKDF2 algorithm using HMAC-SHA1 or HMAC-SHA256. It ensures that keys derived from passwords are resistant to brute-force attacks by using a configurable number of iterations and a salt.

Feel free to explore the methods to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
