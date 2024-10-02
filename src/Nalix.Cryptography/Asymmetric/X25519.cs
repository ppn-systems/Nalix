using Nalix.Framework.Randomization;

namespace Nalix.Cryptography.Asymmetric;

/// <summary>
/// Provides an implementation of the X25519 elliptic curve Diffie–Hellman key exchange.
/// Includes utilities for key generation, scalar multiplication, and shared secret computation.
/// Optimized with aggressive inlining and low-level stack-allocated buffers.
/// </summary>
public static unsafe class X25519
{
    #region Constants

    private const System.Byte ScalarSize = 32;
    private const System.Byte PointSize = 32;
    private const System.Byte FieldElementSize = 32;

    #endregion Constants

    #region APIs

    /// <summary>
    /// Applies clamping to a scalar to conform with X25519 security requirements.
    /// </summary>
    /// <param name="scalar">A 32-byte buffer representing the scalar.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void ClampScalar(System.Span<System.Byte> scalar)
    {
        if (scalar.Length != ScalarSize)
            throw new System.ArgumentException("Scalar must be 32 bytes");

        scalar[0] &= 248;
        scalar[31] &= 127;
        scalar[31] |= 64;
    }

    /// <summary>
    /// Performs scalar multiplication between a private scalar and a public base point.
    /// </summary>
    /// <param name="scalar">The clamped private scalar.</param>
    /// <param name="basePoint">The public base point (usually 0x09 followed by zeroes).</param>
    /// <param name="result">The resulting public key or shared secret.</param>
    public static void ScalarMult(
        System.ReadOnlySpan<System.Byte> scalar,
        System.ReadOnlySpan<System.Byte> basePoint,
        System.Span<System.Byte> result)
    {
        if (scalar.Length != ScalarSize || basePoint.Length != PointSize || result.Length != PointSize)
            throw new System.ArgumentException("Invalid input sizes");

        System.Span<System.UInt32> x1 = stackalloc System.UInt32[10];
        System.Span<System.UInt32> x2 = stackalloc System.UInt32[10];
        System.Span<System.UInt32> z2 = stackalloc System.UInt32[10];
        System.Span<System.UInt32> x3 = stackalloc System.UInt32[10];
        System.Span<System.UInt32> z3 = stackalloc System.UInt32[10];
        System.Span<System.UInt32> tmp0 = stackalloc System.UInt32[10];
        System.Span<System.UInt32> tmp1 = stackalloc System.UInt32[10];

        // Unpack base point
        Unpack25519(x1, basePoint);

        // Initialize
        x2[0] = 1;
        // With the following code
        x1.CopyTo(x3);
        z3[0] = 1;

        System.UInt32 swap = 0;
        for (System.Byte pos = 254; pos >= 0; --pos)
        {
            System.UInt32 b = (System.UInt32)(scalar[pos / 8] >> (pos & 7)) & 1;
            swap ^= b;
            CSwap(x2, x3, swap);
            CSwap(z2, z3, swap);
            swap = b;

            // Montgomery ladder step
            Add(tmp0, x2, z2);
            Sub(tmp1, x2, z2);
            Add(x2, x3, z3);
            Sub(z2, x3, z3);
            Mult(z3, tmp0, z2);
            Mult(z2, tmp1, x2);
            Add(tmp0, z3, z2);
            Sub(tmp1, z3, z2);
            Square(x3, tmp0);
            Square(z2, tmp1);
            Mult(z3, z2, x1);
            Square(tmp0, tmp1);
            Mult(z2, tmp0, tmp1);
            Square(tmp1, tmp0);
            Mult(tmp0, z2, tmp1);
            Mult(z2, tmp0, tmp1);
            Square(tmp1, tmp0);
            Mult(tmp0, z2, tmp1);
        }

        CSwap(x2, x3, swap);
        CSwap(z2, z3, swap);

        // Invert z2
        Invert(z2, z2);
        Mult(x2, x2, z2);

        // Pack result
        Pack25519(result, x2);
    }

    /// <summary>
    /// Generates a new private/public key pair for X25519 key exchange.
    /// </summary>
    /// <param name="privateKey">Output buffer containing the generated private key.</param>
    /// <param name="publicKey">Output buffer containing the corresponding public key.</param>
    public static void GenerateKeyPair(out System.Byte[] privateKey, out System.Byte[] publicKey)
    {
        privateKey = new System.Byte[ScalarSize];
        publicKey = new System.Byte[PointSize];

        SecureRandom.NextBytes(privateKey);

        ClampScalar(privateKey);

        System.ReadOnlySpan<System.Byte> basePoint =
        [
            9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        ];

        ScalarMult(privateKey, basePoint, publicKey);
    }

    /// <summary>
    /// Computes a shared secret using a private key and a peer's public key.
    /// </summary>
    /// <param name="privateKey">Your private key.</param>
    /// <param name="peerPublicKey">The public key of your peer.</param>
    /// <param name="result">The computed shared secret.</param>
    public static void ComputeSharedSecret(
        System.ReadOnlySpan<System.Byte> privateKey,
        System.ReadOnlySpan<System.Byte> peerPublicKey,
        System.Span<System.Byte> result)
    {
        if (privateKey.Length != ScalarSize || peerPublicKey.Length != PointSize || result.Length != PointSize)
            throw new System.ArgumentException("Invalid input sizes");

        ScalarMult(privateKey, peerPublicKey, result);
    }

    #endregion APIs

    #region Private Methods

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Unpack25519(
        System.Span<System.UInt32> output,
        System.ReadOnlySpan<System.Byte> input)
    {
        fixed (System.Byte* inp = input)
        fixed (System.UInt32* outp = output)
        {
            for (System.Byte i = 0; i < 10; i++)
                outp[i] = 0;

            for (System.Byte i = 0; i < 32; i++)
            {
                outp[i >> 2] |= (System.UInt32)inp[i] << ((i & 3) << 3);
            }
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Pack25519(
        System.Span<System.Byte> output,
        System.ReadOnlySpan<System.UInt32> input)
    {
        System.Span<System.UInt32> temp = stackalloc System.UInt32[10];
        input.CopyTo(temp);

        Carry(temp);
        Carry(temp);
        Carry(temp);

        fixed (System.Byte* outp = output)
        fixed (System.UInt32* inp = temp)
        {
            for (System.Byte i = 0; i < 32; i++)
            {
                outp[i] = (System.Byte)(inp[i >> 2] >> ((i & 3) << 3));
            }
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Carry(System.Span<System.UInt32> h)
    {
        System.UInt32 c;
        for (System.Byte i = 0; i < 10; i++)
        {
            c = h[i] >> 26;
            h[i] -= c << 26;
            h[(i + 1) % 10] += c * (i == 9 ? 19u : 1u);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Add(
        System.Span<System.UInt32> h,
        System.ReadOnlySpan<System.UInt32> f,
        System.ReadOnlySpan<System.UInt32> g)
    {
        for (System.Byte i = 0; i < 10; i++)
        {
            h[i] = f[i] + g[i];
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Sub(
        System.Span<System.UInt32> h,
        System.ReadOnlySpan<System.UInt32> f,
        System.ReadOnlySpan<System.UInt32> g)
    {
        for (System.Byte i = 0; i < 10; i++)
        {
            h[i] = f[i] + 0x3ffffed + (0x1ffffff << 1) - g[i];
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Mult(
        System.Span<System.UInt32> h,
        System.ReadOnlySpan<System.UInt32> f,
        System.ReadOnlySpan<System.UInt32> g)
    {
        System.Span<System.Int64> temp = stackalloc System.Int64[19];

        for (System.Byte i = 0; i < 10; i++)
        {
            for (System.Byte j = 0; j < 10; j++)
            {
                temp[i + j] += (System.Int64)f[i] * g[j];
            }
        }

        for (System.Byte i = 0; i < 19; i++)
        {
            if (i >= 10)
            {
                temp[i - 10] += 19 * temp[i];
            }
        }

        for (System.Byte i = 0; i < 10; i++)
        {
            h[i] = (System.UInt32)temp[i];
        }

        Carry(h);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Square(
        System.Span<System.UInt32> h,
        System.ReadOnlySpan<System.UInt32> f) => Mult(h, f, f);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void CSwap(
        System.Span<System.UInt32> a,
        System.Span<System.UInt32> b,
        System.UInt32 swap)
    {
        System.UInt32 mask = 0u - swap;
        for (System.Byte i = 0; i < 10; i++)
        {
            System.UInt32 t = mask & (a[i] ^ b[i]);
            a[i] ^= t;
            b[i] ^= t;
        }
    }

    private static void Invert(
        System.Span<System.UInt32> output,
        System.ReadOnlySpan<System.UInt32> z)
    {
        System.Span<System.UInt32> t0 = stackalloc System.UInt32[10];
        System.Span<System.UInt32> t1 = stackalloc System.UInt32[10];
        System.Span<System.UInt32> t2 = stackalloc System.UInt32[10];
        System.Span<System.UInt32> t3 = stackalloc System.UInt32[10];

        z.CopyTo(t0);

        // x^(2^255-21) = x^(2^255-19-2) = x^(p-2)
        // where p = 2^255-19
        Square(t1, t0);
        Square(t2, t1);
        Square(t2, t2);
        Mult(t2, z, t2);
        Mult(t1, t1, t2);
        Square(t1, t1);
        Mult(t2, t2, t1);
        Square(t1, t2);

        for (System.Byte i = 1; i < 5; i++)
        {
            Square(t1, t1);
        }

        Mult(t2, t1, t2);
        Square(t1, t2);

        for (System.Byte i = 1; i < 10; i++)
        {
            Square(t1, t1);
        }

        Mult(t3, t1, t2);
        Square(t1, t3);

        for (System.Byte i = 1; i < 20; i++)
        {
            Square(t1, t1);
        }

        Mult(t1, t1, t3);
        Square(t1, t1);

        for (System.Byte i = 1; i < 10; i++)
        {
            Square(t1, t1);
        }

        Mult(t2, t1, t2);
        Square(t1, t2);

        for (System.Byte i = 1; i < 50; i++)
        {
            Square(t1, t1);
        }

        Mult(t3, t1, t2);
        Square(t1, t3);

        for (System.Byte i = 1; i < 100; i++)
        {
            Square(t1, t1);
        }

        Mult(t1, t1, t3);
        Square(t1, t1);

        for (System.Byte i = 1; i < 50; i++)
        {
            Square(t1, t1);
        }

        Mult(t2, t1, t2);
        Square(t2, t2);
        Square(t2, t2);
        Mult(output, t2, z);
    }

    #endregion Private Methods
}