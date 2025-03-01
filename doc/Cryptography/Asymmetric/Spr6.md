# Srp6 Class Documentation

The `Srp6` class provides encryption and authentication methods using the SRP-6 (Secure Remote Password) protocol. SRP-6 is a cryptographic protocol used to securely authenticate users over an insecure network. This class is part of the `Notio.Cryptography.Asymmetric` namespace.

## Namespace

```csharp
using Notio.Common.Exceptions;
using Notio.Cryptography.Hash;
using Notio.Randomization;
using System;
using System.Linq;
using System.Numerics;
using System.Text;
```

## Class Definition

### Summary

The `Srp6` class offers methods for generating verifiers, creating server credentials, processing client authentication information, computing the shared secret, calculating session keys, and verifying client proof messages. The class is initialized with a username, salt, and verifier.

```csharp
namespace Notio.Cryptography.Asymmetric
{
    /// <summary>
    /// Class that provides encryption and authentication methods using SRP-6.
    /// </summary>
    /// <remarks>
    /// Initializes an SRP-6 object with a username, salt, and verifier.
    /// </remarks>
    /// <param name="username">User name.</param>
    /// <param name="salt">Salt value as byte array.</param>
    /// <param name="verifier">Verifier value as byte array.</param>
    public sealed class Srp6(string username, byte[] salt, byte[] verifier)
    {
        // Class implementation...
    }
}
```

## Methods

### Constructor

```csharp
public Srp6(string username, byte[] salt, byte[] verifier);
```

- **Description**: Initializes a new instance of the `Srp6` class with the given username, salt, and verifier.
- **Parameters**:
  - `username`: User name.
  - `salt`: Salt value as a byte array.
  - `verifier`: Verifier value as a byte array.

### GenerateVerifier

```csharp
public static byte[] GenerateVerifier(byte[] salt, string username, string password);
```

- **Description**: Creates a verifier from a salt, username, and password.
- **Parameters**:
  - `salt`: Salt value as a byte array.
  - `username`: User name.
  - `password`: Password.
- **Returns**: Verifier as a byte array.

### GenerateServerCredentials

```csharp
public byte[] GenerateServerCredentials();
```

- **Description**: Creates server credentials to send to the client.
- **Returns**: Server credentials as a byte array.

### CalculateSecret

```csharp
public void CalculateSecret(byte[] clientPublicValueBytes);
```

- **Description**: Processes the client's authentication information and generates the shared secret if valid.
- **Parameters**:
  - `clientPublicValueBytes`: The client's public value as a byte array.

### CalculateSessionKey

```csharp
public byte[] CalculateSessionKey();
```

- **Description**: Calculates the session key from the shared secret.
- **Returns**: Session key as a byte array.

### VerifyClientEvidenceMessage

```csharp
public bool VerifyClientEvidenceMessage(byte[] clientProofMessage);
```

- **Description**: Validates the client proof message and saves it if it is correct.
- **Parameters**:
  - `clientProofMessage`: The client proof message as a byte array.
- **Returns**: True if the client proof message is valid, otherwise false.

### CalculateServerEvidenceMessage

```csharp
public byte[] CalculateServerEvidenceMessage();
```

- **Description**: Computes the server proof message using previously verified values.
- **Returns**: The server proof message as a byte array.

### Static Helper Methods

```csharp
private static BigInteger Hash(bool reverse, params BigInteger[] integers);
```

- **Description**: Computes the hash of the provided integers.
- **Parameters**:
  - `reverse`: Whether to reverse the byte order of the hash.
  - `integers`: The integers to hash.
- **Returns**: The computed hash as a `BigInteger`.

```csharp
private static BigInteger ShaInterleave(BigInteger sharedSecret);
```

- **Description**: Computes the interleaved SHA-256 hash of the shared secret.
- **Parameters**:
  - `sharedSecret`: The shared secret.
- **Returns**: The interleaved hash as a `BigInteger`.

```csharp
private static void ReverseBytesAsUInt32(byte[] byteArray);
```

- **Description**: Efficiently reverses byte order in groups of 4 (UInt32).
- **Parameters**:
  - `byteArray`: The byte array to reverse.

## Example Usage

Here's a basic example of how to use the `Srp6` class:

```csharp
using Notio.Cryptography.Asymmetric;
using System;

public class Example
{
    public void Srp6Example()
    {
        string username = "user";
        string password = "password";
        byte[] salt = new byte[16]; // Replace with your salt
        byte[] verifier = Srp6.GenerateVerifier(salt, username, password);

        // Create SRP-6 instance
        Srp6 srp6 = new Srp6(username, salt, verifier);

        // Generate server credentials
        byte[] serverCredentials = srp6.GenerateServerCredentials();
        Console.WriteLine("Server Credentials: " + BitConverter.ToString(serverCredentials).Replace("-", "").ToLower());

        // Simulate client public value
        byte[] clientPublicValue = new byte[256]; // Replace with client's public value
        srp6.CalculateSecret(clientPublicValue);

        // Calculate session key
        byte[] sessionKey = srp6.CalculateSessionKey();
        Console.WriteLine("Session Key: " + BitConverter.ToString(sessionKey).Replace("-", "").ToLower());

        // Simulate client proof message and verify
        byte[] clientProofMessage = new byte[256]; // Replace with client's proof message
        bool isValid = srp6.VerifyClientEvidenceMessage(clientProofMessage);
        Console.WriteLine("Client Proof Valid: " + isValid);

        // Calculate server proof message
        byte[] serverProofMessage = srp6.CalculateServerEvidenceMessage();
        Console.WriteLine("Server Proof Message: " + BitConverter.ToString(serverProofMessage).Replace("-", "").ToLower());
    }
}
```

## Remarks

The `Srp6` class is designed to provide a secure and efficient implementation of the SRP-6 protocol for user authentication. It ensures accurate computation of verifiers, server credentials, shared secrets, session keys, and proof messages.

Feel free to explore the methods to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
