// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Environment.Configuration;
using Nalix.Framework.Options;
using Nalix.Framework.Random;
using Nalix.Framework.Security.Internal;
using Nalix.Framework.Security.Primitives;

namespace Nalix.Framework.Security.Hashing;

/// <summary>
/// Provides secure credential hashing and verification using PBKDF2_I.
/// </summary>
public static class Pbkdf2
{
    #region Constants

    /// <summary>
    /// unified version for encoded format
    /// </summary>
    private const byte Version = 2;

    /// <summary>
    /// Standard key size in bytes.
    /// </summary>
    public const int KeySize = 32;

    /// <summary>
    /// Standard salt size in bytes.
    /// </summary>
    public const int SaltSize = 32;

    /// <summary>
    /// Base iteration count for PBKDF2_I.
    /// Loaded from SecurityOptions if available, otherwise defaults to 310,000.
    /// </summary>
    public static int Iterations => ConfigurationManager.Instance.Get<SecurityOptions>().Pbkdf2Iterations;

    #endregion Constants

    #region Public Methods

    /// <summary>
    /// Generates a hash for a credential using PBKDF2_I and returns the salt and hash.
    /// </summary>
    /// <param name="credential">The plaintext credential to hash.</param>
    /// <param name="salt">The generated salt.</param>
    /// <param name="hash">The derived hash.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Hash(
        [System.Diagnostics.CodeAnalysis.NotNull] string credential,
        [System.Diagnostics.CodeAnalysis.NotNull] out byte[] salt,
        [System.Diagnostics.CodeAnalysis.NotNull] out byte[] hash)
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
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static bool Verify(
        [System.Diagnostics.CodeAnalysis.NotNull] string credential,
        [System.Diagnostics.CodeAnalysis.NotNull] byte[] salt,
        [System.Diagnostics.CodeAnalysis.NotNull] byte[] hash)
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
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public static string Hash(
            [System.Diagnostics.CodeAnalysis.NotNull] string credential)
        {
            Pbkdf2.Hash(credential, out byte[] salt, out byte[] hash);
            byte[] blob = new byte[1 + salt.Length + hash.Length];
            blob[0] = Version;
            Array.Copy(salt, 0, blob, 1, salt.Length);
            Array.Copy(hash, 0, blob, 1 + salt.Length, hash.Length);
            return Convert.ToBase64String(blob);
        }

        /// <summary>
        /// Verifies a credential against a Base64Value-encoded hash with version information.
        /// </summary>
        /// <param name="credential">The credential to verify.</param>
        /// <param name="encoded"></param>
        /// <returns><c>true</c> if the credential matches; otherwise, <c>false</c>.</returns>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public static bool Verify(
            [System.Diagnostics.CodeAnalysis.NotNull] string credential,
            [System.Diagnostics.CodeAnalysis.NotNull] string encoded) => TryParse(encoded, out byte[] salt, out byte[] hash, out byte version) && version == Version && Pbkdf2.Verify(credential, salt, hash);

        /// <summary>
        /// Parses an encoded Base64([ver|salt|hash]) into parts without throwing.
        /// </summary>
        /// <param name="encoded"></param>
        /// <param name="salt"></param>
        /// <param name="hash"></param>
        /// <param name="version"></param>
        internal static bool TryParse(string encoded,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[] salt,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[] hash,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte version)
        {
            salt = [];
            hash = [];
            version = 0;

            if (string.IsNullOrEmpty(encoded))
            {
                return false;
            }

            try
            {
                byte[] blob = Convert.FromBase64String(encoded);

                // exact length check
                const int expected = 1 + SaltSize + KeySize;
                if (blob.Length != expected)
                {
                    return false;
                }

                version = blob[0];
                salt = new byte[SaltSize];
                hash = new byte[KeySize];

                Buffer.BlockCopy(blob, 1, salt, 0, SaltSize);
                Buffer.BlockCopy(blob, 1 + SaltSize, hash, 0, KeySize);
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
