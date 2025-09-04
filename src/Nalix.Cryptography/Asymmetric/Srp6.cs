// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Common.Security.Abstractions;
using Nalix.Cryptography.Hashing;
using Nalix.Cryptography.Primitives;
using Nalix.Framework.Randomization;

namespace Nalix.Cryptography.Asymmetric;

/// <summary>
/// Class that provides encryption and authentication methods using SRP-6.
/// </summary>
/// <remarks>
/// Initializes an SRP-6 object with a username, salt, and verifier.
/// </remarks>
/// <param name="username">User name.</param>
/// <param name="salt">Salt value as byte array.</param>
/// <param name="verifier">Verifier value as byte array.</param>
public sealed class Srp6(System.String username, System.Byte[] salt, System.Byte[] verifier) : ISrp6
{
    #region Properties

    /// <summary>
    /// Large prime ProtocolType N.
    /// </summary>
    public static readonly System.Numerics.BigInteger N = new([
        0xE3, 0x06, 0xEB, 0xC0, 0x2F, 0x1D, 0xC6, 0x9F, 0x5B, 0x43, 0x76, 0x83, 0xFE, 0x38, 0x51, 0xFD,
        0x9A, 0xAA, 0x6E, 0x97, 0xF4, 0xCB, 0xD4, 0x2F, 0xC0, 0x6C, 0x72, 0x05, 0x3C, 0xBC, 0xED, 0x68,
        0xEC, 0x57, 0x0E, 0x66, 0x66, 0xF5, 0x29, 0xC5, 0x85, 0x18, 0xCF, 0x7B, 0x29, 0x9B, 0x55, 0x82,
        0x49, 0x5D, 0xB1, 0x69, 0xAD, 0xF4, 0x8E, 0xCE, 0xB6, 0xD6, 0x54, 0x61, 0xB4, 0xD7, 0xC7, 0x5D,
        0xD1, 0xDA, 0x89, 0x60, 0x1D, 0x5C, 0x49, 0x8E, 0xE4, 0x8B, 0xB9, 0x50, 0xE2, 0xD8, 0xD5, 0xE0,
        0xE0, 0xC6, 0x92, 0xD6, 0x13, 0x48, 0x3B, 0x38, 0xD3, 0x81, 0xEA, 0x96, 0x74, 0xDF, 0x74, 0xD6,
        0x76, 0x65, 0x25, 0x9C, 0x4C, 0x31, 0xA2, 0x9E, 0x0B, 0x3C, 0xFF, 0x75, 0x87, 0x61, 0x72, 0x60,
        0xE8, 0xC5, 0x8F, 0xFA, 0x0A, 0xF8, 0x33, 0x9C, 0xD6, 0x8D, 0xB3, 0xAD, 0xB9, 0x0A, 0xAF, 0xEE ], true);

    /// <summary>
    /// BaseValue36 G.
    /// </summary>
    public static readonly System.Numerics.BigInteger G = 2;

    #endregion Properties

    #region Fields

    private readonly System.Numerics.BigInteger _saltValue = new(salt, true);
    private readonly System.Numerics.BigInteger _verifier = new(verifier, true);
    private readonly System.Byte[] _usernameBytes = System.Text.Encoding.UTF8.GetBytes(username);

    private System.Numerics.BigInteger _sessionKey;
    private System.Numerics.BigInteger _clientProof;
    private System.Numerics.BigInteger _sharedSecret;
    private System.Numerics.BigInteger _clientPublicValue;
    private System.Numerics.BigInteger _serverPublicValue;
    private System.Numerics.BigInteger _serverPrivateValue;

    #endregion Fields

    #region Public Methods

    /// <summary>
    /// Create an authenticator from a salt, username, and password.
    /// </summary>
    /// <param name="salt">Salt value as a byte array.</param>
    /// <param name="username">User name.</param>
    /// <param name="password">Password.</param>
    /// <returns>Verifier as a byte array.</returns>
    public static System.Byte[] GenerateVerifier(System.Byte[] salt, System.String username, System.String password)
    {
        System.Byte[] data = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
        System.Numerics.BigInteger x = Hash(true,
            new System.Numerics.BigInteger(salt, true),
            new System.Numerics.BigInteger(data, true));

        return System.Numerics.BigInteger.ModPow(G, x, N).ToByteArray();
    }

    /// <summary>
    /// Create server credentials to send to the client.
    /// </summary>
    /// <returns>Server credentials as a byte array.</returns>
    public System.Byte[] GenerateServerCredentials()
    {
        _serverPrivateValue = new System.Numerics.BigInteger(SecureRandom.GetBytes((System.Int32)128u), true);
        System.Numerics.BigInteger multiplierParameter = Hash(true, N, G);
        _serverPublicValue = ((multiplierParameter * _verifier) +
            System.Numerics.BigInteger.ModPow(G, _serverPrivateValue, N)) % N;

        return _serverPublicValue.ToByteArray(true);
    }

    /// <summary>
    /// Processes the client's authentication information. If valid, the shared secret is generated.
    /// </summary>
    /// <param name="clientPublicValueBytes">The client's public value as a byte array.</param>
    public void CalculateSecret(System.Byte[] clientPublicValueBytes)
    {
        var clientPublicValue = new System.Numerics.BigInteger(clientPublicValueBytes, true);

        if (clientPublicValue % N == System.Numerics.BigInteger.Zero)
        {
            throw new CryptoException("The value of clientPublicValue cannot be divisible by N");
        }

        _clientPublicValue = clientPublicValue;
        System.Numerics.BigInteger u = Hash(true, _clientPublicValue, _serverPublicValue);
        _sharedSecret = System.Numerics.BigInteger.ModPow(
            _clientPublicValue * System.Numerics.BigInteger.ModPow(_verifier, u, N), _serverPrivateValue, N);
    }

    /// <summary>
    /// Calculate session key from the shared secret.
    /// </summary>
    /// <returns>Session key as a byte array.</returns>
    public System.Byte[] CalculateSessionKey()
    {
        if (_sharedSecret == System.Numerics.BigInteger.Zero)
        {
            throw new CryptoException("Missing data from previous operations: sharedSecret");
        }

        _sessionKey = ShaInterleave(_sharedSecret);
        return _sessionKey.ToByteArray(true);
    }

    /// <summary>
    /// Validate the client proof message and save it if it is correct.
    /// </summary>
    /// <param name="clientProofMessage">The client proof message as a byte array.</param>
    /// <returns>True if the client proof message is valid, otherwise false.</returns>
    public System.Boolean VerifyClientEvidenceMessage(System.Byte[] clientProofMessage)
    {
        if (_clientPublicValue == System.Numerics.BigInteger.Zero ||
            _serverPublicValue == System.Numerics.BigInteger.Zero ||
            _sharedSecret == System.Numerics.BigInteger.Zero)
        {
            throw new CryptoException(
                "Missing data from previous operations: clientPublicValue, serverPublicValue, sharedSecret");
        }

        var usernameHash = SHA256.HashData(_usernameBytes);
        System.Numerics.BigInteger expectedClientProof = Hash(false, Hash(false, N) ^ Hash(false, G),
            new System.Numerics.BigInteger(usernameHash, true), _saltValue,
            _clientPublicValue, _serverPublicValue, _sessionKey);

        if (!clientProofMessage.SequenceEqual(expectedClientProof.ToByteArray(true)))
        {
            return false;
        }

        _clientProof = new System.Numerics.BigInteger(clientProofMessage, true);
        return true;
    }

    /// <summary>
    /// Compute the server proof message using previously verified values.
    /// </summary>
    /// <returns>The server proof message as a byte array.</returns>
    public System.Byte[] CalculateServerEvidenceMessage()
    {
        if (_clientPublicValue == System.Numerics.BigInteger.Zero ||
            _clientProof == System.Numerics.BigInteger.Zero ||
            _sessionKey == System.Numerics.BigInteger.Zero)
        {
            throw new CryptoException("Missing data from previous operations: clientPublicValue, clientProof, sessionKey");
        }

        System.Numerics.BigInteger serverProof = Hash(true, _clientPublicValue, _clientProof, _sessionKey);

        System.Byte[] serverProofBytes = serverProof.ToByteArray(true);
        ReverseBytesAsUInt32(serverProofBytes);
        return serverProofBytes;
    }

    #endregion Public Methods

    #region Private Methods


    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Numerics.BigInteger Hash(
        System.Boolean reverse,
        params System.Numerics.BigInteger[] integers)
    {
        using SHA256 sha256 = new();
        sha256.Initialize();

        for (System.Int32 i = 0; i < integers.Length; i++)
        {
            System.Byte[] buffer = integers[i].ToByteArray(true);
            System.Int32 padding = buffer.Length % 4;
            if (padding != 0)
            {
                System.Array.Resize(ref buffer, buffer.Length + (4 - padding));
            }

            if (i == integers.Length - 1)
            {
                sha256.TransformFinalBlock(buffer, 0, buffer.Length);
            }
            else
            {
                _ = sha256.TransformBlock(buffer, 0, buffer.Length, null, 0);
            }
        }

        System.Byte[] hash = sha256.Hash ?? [];
        if (reverse)
        {
            ReverseBytesAsUInt32(hash);
        }

        return new System.Numerics.BigInteger(hash, true);
    }

    private static System.Numerics.BigInteger ShaInterleave(System.Numerics.BigInteger sharedSecret)
    {
        System.Byte[] secretBytes = sharedSecret.ToByteArray(true);
        System.Byte[] reversedSecretBytes = [.. secretBytes.Reverse()];

        System.Int32 firstZeroIndex = System.Array.IndexOf(secretBytes, (System.Byte)0);
        System.Int32 length = 4;

        if (firstZeroIndex >= 0 && firstZeroIndex < reversedSecretBytes.Length - 4)
        {
            length = reversedSecretBytes.Length - firstZeroIndex;
        }

        System.Byte[] evenIndexedBytes = new System.Byte[length / 2];
        for (System.UInt32 i = 0u; i < evenIndexedBytes.Length; i++)
        {
            evenIndexedBytes[i] = reversedSecretBytes[i * 2];
        }

        System.Byte[] oddIndexedBytes = new System.Byte[length / 2];
        for (System.UInt32 i = 0u; i < oddIndexedBytes.Length; i++)
        {
            oddIndexedBytes[i] = reversedSecretBytes[(i * 2) + 1];
        }

        System.Byte[] evenHash = SHA256.HashData(evenIndexedBytes);
        System.Byte[] oddHash = SHA256.HashData(oddIndexedBytes);

        System.Byte[] interleavedHash = new System.Byte[evenHash.Length + oddHash.Length];
        for (System.UInt32 i = 0u; i < interleavedHash.Length; i++)
        {
            interleavedHash[i] = i % 2 == 0 ? evenHash[i / 2] : oddHash[i / 2];
        }

        return new System.Numerics.BigInteger(interleavedHash, true);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ReverseBytesAsUInt32(System.Byte[] byteArray)
    {
        // Efficiently reverses byte order in groups of 4 (UInt32).
        if (byteArray.Length % 4 != 0)
        {
            throw new System.ArgumentException("Array length must be a multiple of 4.");
        }

        System.Int32 j = byteArray.Length - 4;
        for (System.Int32 i = 0; i < byteArray.Length / 2; i += 4, j -= 4)
        {
            (byteArray[j + 0], byteArray[i + 0]) = (byteArray[i + 0], byteArray[j + 0]);
            (byteArray[j + 1], byteArray[i + 1]) = (byteArray[i + 1], byteArray[j + 1]);
            (byteArray[j + 2], byteArray[i + 2]) = (byteArray[i + 2], byteArray[j + 2]);
            (byteArray[j + 3], byteArray[i + 3]) = (byteArray[i + 3], byteArray[j + 3]);
        }
    }

    #endregion Private Methods
}