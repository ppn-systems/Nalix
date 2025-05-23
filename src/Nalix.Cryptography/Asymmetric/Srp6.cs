using Nalix.Common.Cryptography.Asymmetric;
using Nalix.Common.Exceptions;
using Nalix.Cryptography.Hashing;
using Nalix.Environment;
using Nalix.Randomization;
using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

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
public sealed class Srp6(string username, byte[] salt, byte[] verifier) : ISrp6
{
    #region Properties

    /// <summary>
    /// Large prime Number N.
    /// </summary>
    public static readonly BigInteger N = new([
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
    public static readonly BigInteger G = 2;

    #endregion Properties

    #region Fields

    private readonly BigInteger _saltValue = new(salt, true);
    private readonly BigInteger _verifier = new(verifier, true);
    private readonly byte[] _usernameBytes = SerializationOptions.Encoding.GetBytes(username);

    private BigInteger _sessionKey;
    private BigInteger _clientProof;
    private BigInteger _sharedSecret;
    private BigInteger _clientPublicValue;
    private BigInteger _serverPublicValue;
    private BigInteger _serverPrivateValue;

    #endregion Fields

    #region Public Methods

    /// <summary>
    /// Create an authenticator from a salt, username, and password.
    /// </summary>
    /// <param name="salt">Salt value as a byte array.</param>
    /// <param name="username">User name.</param>
    /// <param name="password">Password.</param>
    /// <returns>Verifier as a byte array.</returns>
    public static byte[] GenerateVerifier(byte[] salt, string username, string password)
    {
        byte[] data = SHA256.HashData(SerializationOptions.Encoding.GetBytes($"{username}:{password}"));
        BigInteger x = Hash(true, new BigInteger(salt, true), new BigInteger(data, true));

        return BigInteger.ModPow(G, x, N).ToByteArray();
    }

    /// <summary>
    /// Create server credentials to send to the client.
    /// </summary>
    /// <returns>Server credentials as a byte array.</returns>
    public byte[] GenerateServerCredentials()
    {
        _serverPrivateValue = new BigInteger(RandGenerator.GetBytes((int)128u), true);
        BigInteger multiplierParameter = Hash(true, N, G);
        _serverPublicValue = (multiplierParameter * _verifier + BigInteger.ModPow(G, _serverPrivateValue, N)) % N;

        return _serverPublicValue.ToByteArray(true);
    }

    /// <summary>
    /// Processes the client's authentication information. If valid, the shared secret is generated.
    /// </summary>
    /// <param name="clientPublicValueBytes">The client's public value as a byte array.</param>
    public void CalculateSecret(byte[] clientPublicValueBytes)
    {
        var clientPublicValue = new BigInteger(clientPublicValueBytes, true);

        if (clientPublicValue % N == BigInteger.Zero)
            throw new CryptoException("The value of clientPublicValue cannot be divisible by N");

        _clientPublicValue = clientPublicValue;
        BigInteger u = Hash(true, _clientPublicValue, _serverPublicValue);
        _sharedSecret = BigInteger.ModPow(_clientPublicValue * BigInteger.ModPow(_verifier, u, N), _serverPrivateValue, N);
    }

    /// <summary>
    /// Calculate session key from the shared secret.
    /// </summary>
    /// <returns>Session key as a byte array.</returns>
    public byte[] CalculateSessionKey()
    {
        if (_sharedSecret == BigInteger.Zero)
            throw new CryptoException("Missing data from previous operations: sharedSecret");

        _sessionKey = ShaInterleave(_sharedSecret);
        return _sessionKey.ToByteArray(true);
    }

    /// <summary>
    /// Validate the client proof message and save it if it is correct.
    /// </summary>
    /// <param name="clientProofMessage">The client proof message as a byte array.</param>
    /// <returns>True if the client proof message is valid, otherwise false.</returns>
    public bool VerifyClientEvidenceMessage(byte[] clientProofMessage)
    {
        if (_clientPublicValue == BigInteger.Zero ||
            _serverPublicValue == BigInteger.Zero ||
            _sharedSecret == BigInteger.Zero)
            throw new CryptoException(
                "Missing data from previous operations: clientPublicValue, serverPublicValue, sharedSecret");

        var usernameHash = SHA256.HashData(_usernameBytes);
        BigInteger expectedClientProof = Hash(false, Hash(false, N) ^ Hash(false, G),
            new BigInteger(usernameHash, true), _saltValue, _clientPublicValue, _serverPublicValue, _sessionKey);

        if (!clientProofMessage.SequenceEqual(expectedClientProof.ToByteArray(true)))
            return false;

        _clientProof = new BigInteger(clientProofMessage, true);
        return true;
    }

    /// <summary>
    /// Compute the server proof message using previously verified values.
    /// </summary>
    /// <returns>The server proof message as a byte array.</returns>
    public byte[] CalculateServerEvidenceMessage()
    {
        if (_clientPublicValue == BigInteger.Zero || _clientProof == BigInteger.Zero || _sessionKey == BigInteger.Zero)
            throw new CryptoException("Missing data from previous operations: clientPublicValue, clientProof, sessionKey");

        BigInteger serverProof = Hash(true, _clientPublicValue, _clientProof, _sessionKey);

        byte[] serverProofBytes = serverProof.ToByteArray(true);
        ReverseBytesAsUInt32(serverProofBytes);
        return serverProofBytes;
    }

    #endregion Public Methods

    #region Private Methods

    private static BigInteger Hash(bool reverse, params BigInteger[] integers)
    {
        using SHA256 sha256 = new();
        sha256.Initialize();

        for (int i = 0; i < integers.Length; i++)
        {
            byte[] buffer = integers[i].ToByteArray(true);
            int padding = buffer.Length % 4;
            if (padding != 0)
                Array.Resize(ref buffer, buffer.Length + (4 - padding));

            if (i == integers.Length - 1)
                sha256.TransformFinalBlock(buffer, 0, buffer.Length);
            else
                sha256.TransformBlock(buffer, 0, buffer.Length, null, 0);
        }

        byte[] hash = sha256.Hash ?? [];
        if (reverse)
            ReverseBytesAsUInt32(hash);
        return new BigInteger(hash, true);
    }

    private static BigInteger ShaInterleave(BigInteger sharedSecret)
    {
        byte[] secretBytes = sharedSecret.ToByteArray(true);
        byte[] reversedSecretBytes = [.. secretBytes.Reverse()];

        int firstZeroIndex = Array.IndexOf(secretBytes, (byte)0);
        int length = 4;

        if (firstZeroIndex >= 0 && firstZeroIndex < reversedSecretBytes.Length - 4)
            length = reversedSecretBytes.Length - firstZeroIndex;

        byte[] evenIndexedBytes = new byte[length / 2];
        for (uint i = 0u; i < evenIndexedBytes.Length; i++)
            evenIndexedBytes[i] = reversedSecretBytes[i * 2];

        byte[] oddIndexedBytes = new byte[length / 2];
        for (uint i = 0u; i < oddIndexedBytes.Length; i++)
            oddIndexedBytes[i] = reversedSecretBytes[i * 2 + 1];

        byte[] evenHash = SHA256.HashData(evenIndexedBytes);
        byte[] oddHash = SHA256.HashData(oddIndexedBytes);

        byte[] interleavedHash = new byte[evenHash.Length + oddHash.Length];
        for (uint i = 0u; i < interleavedHash.Length; i++)
        {
            if (i % 2 == 0)
                interleavedHash[i] = evenHash[i / 2];
            else
                interleavedHash[i] = oddHash[i / 2];
        }

        return new BigInteger(interleavedHash, true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReverseBytesAsUInt32(byte[] byteArray)
    {
        // Efficiently reverses byte order in groups of 4 (UInt32).
        if (byteArray.Length % 4 != 0)
            throw new ArgumentException("Array length must be a multiple of 4.");

        int j = byteArray.Length - 4;
        for (int i = 0; i < byteArray.Length / 2; i += 4, j -= 4)
        {
            (byteArray[j + 0], byteArray[i + 0]) = (byteArray[i + 0], byteArray[j + 0]);
            (byteArray[j + 1], byteArray[i + 1]) = (byteArray[i + 1], byteArray[j + 1]);
            (byteArray[j + 2], byteArray[i + 2]) = (byteArray[i + 2], byteArray[j + 2]);
            (byteArray[j + 3], byteArray[i + 3]) = (byteArray[i + 3], byteArray[j + 3]);
        }
    }

    #endregion Private Methods
}
