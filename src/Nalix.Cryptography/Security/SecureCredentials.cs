// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Cryptography.Primitives;
using Nalix.Framework.Randomization;

namespace Nalix.Cryptography.Security;

/// <summary>
/// Provides secure credential hashing and verification using PBKDF2.
/// </summary>
public static class SecureCredentials
{
    #region Constants

    /// <summary>
    /// Standard key size in bytes.
    /// </summary>
    public const System.Int32 KeySize = 32;

    /// <summary>
    /// Standard salt size in bytes.
    /// </summary>
    public const System.Int32 SaltSize = 32;

    /// <summary>
    /// ProtocolType of iterations for PBKDF2.
    /// </summary>
    public const System.Int32 Iterations = 100_000;

    #endregion Constants

    #region Public Methods

    /// <summary>
    /// Generates a hash for a credential using PBKDF2 and returns the salt and hash.
    /// </summary>
    /// <param name="credential">The plaintext credential to hash.</param>
    /// <param name="salt">The generated salt.</param>
    /// <param name="hash">The derived hash.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void GenerateCredentialHash(System.String credential, out System.Byte[] salt, out System.Byte[] hash)
    {
        salt = SecureRandom.GetBytes(SaltSize);
        using PBKDF2 pbkdf2 = new(salt, Iterations, KeySize, HashAlgorithmType.Sha256);
        hash = pbkdf2.DeriveKey(credential);
    }

    /// <summary>
    /// Generates a Base64Value-encoded string containing version, salt, and hash for a credential.
    /// ToByteArray: [version (1 byte)] + [salt] + [hash].
    /// </summary>
    /// <param name="credential">The plaintext credential.</param>
    /// <returns>A Base64Value-encoded string containing version, salt, and hash.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.String GenerateCredentialBase64(System.String credential)
    {
        GenerateCredentialHash(credential, out System.Byte[] salt, out System.Byte[] hash);
        System.Byte[] combined = new System.Byte[1 + salt.Length + hash.Length];
        System.Byte version = 1;
        combined[0] = version;
        System.Array.Copy(salt, 0, combined, 1, salt.Length);
        System.Array.Copy(hash, 0, combined, 1 + salt.Length, hash.Length);
        return System.Convert.ToBase64String(combined);
    }

    /// <summary>
    /// Verifies whether the provided credential matches the stored hash.
    /// </summary>
    /// <param name="credential">The credential to verify.</param>
    /// <param name="salt">The salt used for hashing.</param>
    /// <param name="hash">The stored hash to compare against.</param>
    /// <returns><c>true</c> if the credential is valid; otherwise, <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean VerifyCredentialHash(System.String credential, System.Byte[] salt, System.Byte[] hash)
    {
        using PBKDF2 pbkdf2 = new(salt, Iterations, KeySize, HashAlgorithmType.Sha256);
        return BitwiseUtils.FixedTimeEquals(pbkdf2.DeriveKey(credential), hash);
    }

    /// <summary>
    /// Verifies a credential against a Base64Value-encoded hash with version information.
    /// </summary>
    /// <param name="credential">The credential to verify.</param>
    /// <param name="encodedCredentials">The Base64Value-encoded string containing version, salt, and hash.</param>
    /// <returns><c>true</c> if the credential matches; otherwise, <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean VerifyCredentialFromBase64(System.String credential, System.String encodedCredentials)
    {
        if (System.String.IsNullOrEmpty(encodedCredentials))
        {
            return false;
        }

        try
        {
            System.Byte[] combined = System.Convert.FromBase64String(encodedCredentials);
            if (combined.Length < 1 + SaltSize + KeySize)
            {
                return false;
            }

            System.Byte version = combined[0];
            System.Byte[] salt = new System.Byte[SaltSize];
            System.Byte[] storedHash = new System.Byte[KeySize];
            System.Array.Copy(combined, 1, salt, 0, SaltSize);
            System.Array.Copy(combined, 1 + SaltSize, storedHash, 0, KeySize);

            return version == 1 && VerifyCredentialHash(credential, salt, storedHash);
        }
        catch (System.FormatException)
        {
            return false; // Base64 Value false
        }
    }

    #endregion Public Methods
}