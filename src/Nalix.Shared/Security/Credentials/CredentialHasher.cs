// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Framework.Randomization;
using Nalix.Shared.Security.Primitives;

namespace Nalix.Shared.Security.Credentials;

/// <summary>
/// Provides secure credential hashing and verification using PBKDF2.
/// </summary>
public static class CredentialHasher
{
    #region Constants

    private const System.Byte Version = 2; // unified version for encoded format

    /// <summary>
    /// Standard key size in bytes.
    /// </summary>
    public const System.Int32 KeySize = 32;

    /// <summary>
    /// Standard salt size in bytes.
    /// </summary>
    public const System.Int32 SaltSize = 32;

    /// <summary>
    /// Iteration count for PBKDF2.
    /// </summary>
    public const System.Int32 Iterations = 310_000;

    #endregion Constants

    #region Public Methods

    /// <summary>
    /// Generates a hash for a credential using PBKDF2 and returns the salt and hash.
    /// </summary>
    /// <param name="credential">The plaintext credential to hash.</param>
    /// <param name="salt">The generated salt.</param>
    /// <param name="hash">The derived hash.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Hash(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.String credential,
        out System.Byte[] salt, out System.Byte[] hash)
    {
        salt = Csprng.GetBytes(SaltSize);
        using PBKDF2 pbkdf2 = new(salt, Iterations, KeySize);
        hash = pbkdf2.GenerateKey(credential);
    }

    /// <summary>
    /// Verifies whether the provided credential matches the stored hash.
    /// </summary>
    /// <param name="credential">The credential to verify.</param>
    /// <param name="salt">The salt used for hashing.</param>
    /// <param name="hash">The stored hash to compare against.</param>
    /// <returns><c>true</c> if the credential is valid; otherwise, <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Boolean Verify(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.String credential,
        System.Byte[] salt, System.Byte[] hash)
    {
        using PBKDF2 pbkdf2 = new(salt, Iterations, KeySize);
        return BitwiseOperations.FixedTimeEquals(pbkdf2.GenerateKey(credential), hash);
    }

    /// <summary>
    /// Encodes a hashed credential with versioning, salt, and hash into a Base64 string.
    /// </summary>
    public static class Encoded
    {
        /// <summary>
        /// Generates a Base64Value-encoded string containing version, salt, and hash for a credential.
        /// ToByteArray: [version (1 byte)] + [salt] + [hash].
        /// </summary>
        /// <param name="credential">The plaintext credential.</param>
        /// <returns>A Base64Value-encoded string containing version, salt, and hash.</returns>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public static System.String Hash(
            [System.Diagnostics.CodeAnalysis.DisallowNull] System.String credential)
        {
            CredentialHasher.Hash(credential, out System.Byte[] salt, out System.Byte[] hash);
            System.Byte[] blob = new System.Byte[1 + salt.Length + hash.Length];
            blob[0] = Version;
            System.Array.Copy(salt, 0, blob, 1, salt.Length);
            System.Array.Copy(hash, 0, blob, 1 + salt.Length, hash.Length);
            return System.Convert.ToBase64String(blob);
        }

        /// <summary>
        /// Verifies a credential against a Base64Value-encoded hash with version information.
        /// </summary>
        /// <param name="credential">The credential to verify.</param>
        /// <param name="encoded"></param>
        /// <returns><c>true</c> if the credential matches; otherwise, <c>false</c>.</returns>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public static System.Boolean Verify(
            [System.Diagnostics.CodeAnalysis.DisallowNull] System.String credential, System.String encoded)
        {
            if (!TryParse(encoded, out System.Byte[] salt, out System.Byte[] hash, out System.Byte version))
            {
                return false;
            }

            if (version != Version) // reject unknown/old formats
            {
                return false;
            }

            return CredentialHasher.Verify(credential, salt, hash);
        }

        /// <summary>
        /// Parses an encoded Base64([ver|salt|hash]) into parts without throwing.
        /// </summary>
        internal static System.Boolean TryParse(System.String encoded,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Byte[] salt,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Byte[] hash,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Byte version)
        {
            salt = [];
            hash = [];
            version = 0;

            if (System.String.IsNullOrEmpty(encoded))
            {
                return false;
            }

            try
            {
                System.Byte[] blob = System.Convert.FromBase64String(encoded);

                // exact length check
                const System.Int32 expected = 1 + SaltSize + KeySize;
                if (blob.Length != expected)
                {
                    return false;
                }

                version = blob[0];
                salt = new System.Byte[SaltSize];
                hash = new System.Byte[KeySize];

                System.Buffer.BlockCopy(blob, 1, salt, 0, SaltSize);
                System.Buffer.BlockCopy(blob, 1 + SaltSize, hash, 0, KeySize);
                return true;
            }
            catch (System.FormatException)
            {
                return false;
            }
        }
    }

    #endregion Public Methods
}