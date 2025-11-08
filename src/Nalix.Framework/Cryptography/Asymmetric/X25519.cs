// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Framework.Randomization;

namespace Nalix.Framework.Cryptography.Asymmetric;

/// <summary>
/// Provides methods for generating and using X25519 key pairs for elliptic curve cryptography based on Curve25519.
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
        /// The private key as a byte array of length 32.
        /// </summary>
        public System.Byte[] PrivateKey;

        /// <summary>
        /// The public key as a byte array of length 32.
        /// </summary>
        public System.Byte[] PublicKey;
    }

    /// <summary>
    /// Generates a new X25519 key pair with a random private key and its corresponding public key.
    /// </summary>
    /// <returns>An <see cref="X25519KeyPair"/> containing the generated private key and public key.</returns>
    /// <remarks>
    /// The private key is generated using a secure random number generator and modified according to the X25519 specification
    /// (see <see href="https://cr.yp.to/ecdh.html"/>). The public key is computed using scalar multiplication with the Curve25519 basepoint.
    /// </remarks>
    public static X25519KeyPair GenerateKeyPair()
    {
        // at first generate the private key
        X25519KeyPair key = new()
        {
            PrivateKey = new System.Byte[32]
        };

        SecureRandom.Fill(key.PrivateKey);

        // as defined in https://cr.yp.to/ecdh.html do these operation to finalize the private key
        key.PrivateKey[0] &= 248;
        key.PrivateKey[31] &= 127;
        key.PrivateKey[31] |= 64;
        // compute the public key
        key.PublicKey = Curve25519.ScalarMultiplication(key.PrivateKey, Curve25519.Basepoint);
        return key;
    }

    /// <summary>
    /// Generates an X25519 key pair from a provided private key.
    /// </summary>
    /// <param name="privateKey">A byte array of length 32 representing the private key.</param>
    /// <returns>An <see cref="X25519KeyPair"/> containing the provided private key and its corresponding public key.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="privateKey"/> is <c>null</c>.</exception>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="privateKey"/> is not 32 bytes in length.</exception>
    /// <remarks>
    /// The public key is computed using scalar multiplication of the provided private key with the Curve25519 basepoint.
    /// </remarks>
    public static X25519KeyPair GenerateKeyFromPrivateKey(System.Byte[] privateKey)
    {
        X25519KeyPair key = new()
        {
            PrivateKey = privateKey
        };
        key.PublicKey = Curve25519.ScalarMultiplication(key.PrivateKey, Curve25519.Basepoint);
        return key;
    }

    /// <summary>
    /// Computes a shared secret using the X25519 key agreement protocol.
    /// </summary>
    /// <param name="myPrivateKey">A byte array of length 32 representing the local private key.</param>
    /// <param name="otherPublicKey">A byte array of length 32 representing the remote public key.</param>
    /// <returns>A byte array of length 32 containing the shared secret.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="myPrivateKey"/> or <paramref name="otherPublicKey"/> is <c>null</c>.</exception>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="myPrivateKey"/> or <paramref name="otherPublicKey"/> is not 32 bytes in length.</exception>
    /// <remarks>
    /// The shared secret is computed by performing scalar multiplication of the local private key with the remote public key
    /// using the Curve25519 algorithm.
    /// </remarks>
    public static System.Byte[] Agreement(System.Byte[] myPrivateKey, System.Byte[] otherPublicKey)
        => Curve25519.ScalarMultiplication(myPrivateKey, otherPublicKey);
}

[System.Diagnostics.StackTraceHidden]
internal static class Curve25519
{
    /// <summary>
    /// The base point that is x = 9
    /// </summary>
    public static readonly System.Byte[] Basepoint =
    [
        9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    ];

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Byte[] ScalarMult(System.Byte[] input, System.Byte[] baseIn)
    {
        var e = new System.Byte[32];

        System.Array.Copy(input, e, 32); //copy(e[:], input[:])
        e[0] &= 248;
        e[31] &= 127;
        e[31] |= 64;

        FieldElement x1, x2, z2, x3, z3, tmp0, tmp1;
        z2 = new FieldElement();
        // feFromBytes(&x1, base)
        x1 = new FieldElement(baseIn); //SECOND NUMBER
        //feOne(&x2)
        x2 = new FieldElement();
        x2.One();
        //feCopy(&x3, &x1)
        x3 = new FieldElement();
        FieldElement.Copy(ref x3, x1);
        //feOne(&z3)
        z3 = new FieldElement();
        z3.One();

        System.Int32 swap = 0;
        for (System.Int32 pos = 254; pos >= 0; pos--)
        {
            System.Byte b = System.Convert.ToByte(e[pos / 8] >> (pos & 7));
            b &= 1;
            swap ^= b;
            FieldElement.CSwap(ref x2, ref x3, swap);
            FieldElement.CSwap(ref z2, ref z3, swap);
            swap = b;

            tmp0 = x3 - z3; //feSub(&tmp0, &x3, &z3)
            tmp1 = x2 - z2; //feSub(&tmp1, &x2, &z2)
            x2 += z2; //feAdd(&x2, &x2, &z2)
            z2 = x3 + z3; //feAdd(&z2, &x3, &z3)
            z3 = tmp0.Multiply(x2);
            z2 = z2.Multiply(tmp1);
            tmp0 = tmp1.Square();
            tmp1 = x2.Square();
            x3 = z3 + z2; //feAdd(&x3, &z3, &z2)
            z2 = z3 - z2; //feSub(&z2, &z3, &z2)
            x2 = tmp1.Multiply(tmp0);
            tmp1 -= tmp0;//feSub(&tmp1, &tmp1, &tmp0)
            z2 = z2.Square();
            z3 = tmp1.Mul121666();
            x3 = x3.Square();
            tmp0 += z3; //feAdd(&tmp0, &tmp0, &z3)
            z3 = x1.Multiply(z2);
            z2 = tmp1.Multiply(tmp0);
        }

        FieldElement.CSwap(ref x2, ref x3, swap);
        FieldElement.CSwap(ref z2, ref z3, swap);

        z2 = z2.Invert();
        x2 = x2.Multiply(z2);
        return x2.ToBytes();
    }

    /// <summary>
    /// <para>
    /// X25519 returns the result of the scalar multiplication (scalar * point),
    /// according to RFC 7748, Section 5. scalar, point and the return value are
    /// slices of 32 bytes.
    /// </para>
    /// <para>
    /// If point is Basepoint (but not if it's a different slice with the same
    /// contents) a precomputed implementation might be used for performance.
    /// </para>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Byte[] ScalarMultiplication(System.Byte[] scalar, System.Byte[] point)
    {
        if (scalar.Length != 32)
        {
            throw new System.ArgumentException("Length of scalar must be 32", nameof(scalar));
        }

        if (point.Length != 32)
        {
            throw new System.ArgumentException("Length of point must be 32", nameof(point));
        }

        System.Byte[] zero = new System.Byte[32];
        System.Byte[] result = ScalarMult(scalar, point);
        // here I tried to make something like subtle.ConstantTimeCompare
        if (result.Length != zero.Length)
        {
            throw new System.Exception("This should not happen. Because result is always 32 bytes");
        }

        System.Byte v = 0;
        for (System.Int32 i = 0; i < result.Length; i++)
        {
            v = (System.Byte)(v | (zero[i] ^ result[i]));
        }

        return (System.Int32)((System.UInt32)(v ^ 0) - 1 >> 31) == 1
            ? throw new System.Exception("bad input point: low order point")
            : result;
    }
}