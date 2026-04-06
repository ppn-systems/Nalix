// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Random;

namespace Nalix.Framework.Security.Asymmetric;

/// <summary>
/// Provides methods for generating and using X25519 key pairs for elliptic curve Diffie–Hellman (ECDH) 
/// key agreement based on Curve25519 (RFC 7748).
/// </summary>
public static class X25519
{
    /// <summary>
    /// Represents an X25519 key pair consisting of a private key and a public key.
    /// </summary>
    [System.Runtime.CompilerServices.SkipLocalsInit]
    public struct X25519KeyPair
    {
        /// <summary>
        /// The private key (32 bytes).
        /// </summary>
        public byte[] PrivateKey { get; set; }

        /// <summary>
        /// The public key (32 bytes).
        /// </summary>
        public byte[] PublicKey { get; set; }
    }

    /// <summary>
    /// Generates a new X25519 key pair with a cryptographically secure random private key.
    /// </summary>
    /// <returns>A new <see cref="X25519KeyPair"/> containing both private and public keys.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static X25519KeyPair GenerateKeyPair()
    {
        X25519KeyPair key = new() { PrivateKey = new byte[32] };
        Csprng.Fill(key.PrivateKey);

        // Clamp per https://cr.yp.to/ecdh.html
        key.PrivateKey[0] &= 248;
        key.PrivateKey[31] &= 127;
        key.PrivateKey[31] |= 64;

        key.PublicKey = Curve25519.ScalarMultiplication(key.PrivateKey, Curve25519.Basepoint);
        return key;
    }

    /// <summary>
    /// Derives the public key from a provided 32-byte private key.
    /// </summary>
    /// <param name="privateKey">The 32-byte private key used to derive the key pair.</param>
    /// <returns>An <see cref="X25519KeyPair"/> instance initialized with the provided private key and its derived public key.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static X25519KeyPair GenerateKeyFromPrivateKey([System.Diagnostics.CodeAnalysis.NotNull] byte[] privateKey)
    {
        X25519KeyPair key = new() { PrivateKey = privateKey };
        key.PublicKey = Curve25519.ScalarMultiplication(key.PrivateKey, Curve25519.Basepoint);
        return key;
    }

    /// <summary>
    /// Computes a shared secret via X25519 scalar multiplication 
    /// (<paramref name="myPrivateKey"/> × <paramref name="otherPublicKey"/>).
    /// </summary>
    /// <param name="myPrivateKey">The local 32-byte private key.</param>
    /// <param name="otherPublicKey">The remote 32-byte public key.</param>
    /// <returns>A 32-byte shared secret that can be used for session key derivation.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static byte[] Agreement([System.Diagnostics.CodeAnalysis.NotNull] byte[] myPrivateKey, [System.Diagnostics.CodeAnalysis.NotNull] byte[] otherPublicKey) => Curve25519.ScalarMultiplication(myPrivateKey, otherPublicKey);
}
