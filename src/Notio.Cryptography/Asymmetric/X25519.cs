using Notio.Randomization;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Notio.Cryptography.Asymmetric;

/// <summary>
/// Provides methods for the X25519 elliptic curve Diffie-Hellman (ECDH) key exchange protocol.
/// </summary>
/// <remarks>
/// X25519 is a specific elliptic curve designed for use in cryptographic protocols like TLS.
/// It allows two parties to securely exchange K without needing to share a secret in advance.
/// </remarks>
public static class X25519
{
    // Prime p = 2^255 - 19
    private static readonly BigInteger P = (BigInteger.One << 255) - 19;

    // Constant A24 = (486662 - 2)/4 = 121665
    private static readonly BigInteger A24 = 121665;

    /// <summary>
    /// Generates an X25519 key pair.
    /// (Tạo cặp khóa X25519)
    /// </summary>
    /// <returns>A tuple with (privateKey, publicKey) each 32 bytes.</returns>
    public static (byte[] PrivateKey, byte[] PublicKey) GenerateKeyPair()
    {
        // Generate a random 32-byte scalar.
        byte[] privateKey = new byte[32];
        RandomizedGenerator.Fill(privateKey);
        // Clamp the scalar.
        privateKey = ClampScalar(privateKey);

        // The standard base point u = 9 (encoded as 32-byte little-endian)
        byte[] basePoint = new byte[32];
        basePoint[0] = 9;

        byte[] publicKey = ScalarMult(privateKey, basePoint);
        return (privateKey, publicKey);
    }

    /// <summary>
    /// Computes the shared secret between your private key and a peer's public key.
    /// (Tính shared secret giữa private key của bạn và public key của đối tác.)
    /// </summary>
    /// <param name="privateKey">Your 32-byte private key.</param>
    /// <param name="peerPublicKey">The peer's 32-byte public key.</param>
    /// <returns>The shared secret as a 32-byte array.</returns>
    public static byte[] ComputeSharedSecret(byte[] privateKey, byte[] peerPublicKey)
    {
        // Both K should be 32 bytes.
        if (privateKey.Length != 32 || peerPublicKey.Length != 32)
            throw new ArgumentException("Keys must be 32 bytes.");

        return ScalarMult(privateKey, peerPublicKey);
    }

    /// <summary>
    /// Clamps a 32-byte scalar for X25519 as specified in RFC 7748.
    /// (Cách này đảm bảo scalar được "clamp" theo quy định của RFC 7748.)
    /// </summary>
    private static byte[] ClampScalar(byte[] scalar)
    {
        if (scalar.Length != 32)
            throw new ArgumentException("Scalar must be 32 bytes.", nameof(scalar));

        byte[] s = (byte[])scalar.Clone();
        s[0] &= 248;     // Clear lower 3 bits
        s[31] &= 127;    // Clear high bit
        s[31] |= 64;     // Set second-highest bit
        return s;
    }

    /// <summary>
    /// Computes X25519 scalar multiplication.
    /// Computes result = scalar * u (the u-coordinate on the Montgomery curve).
    /// (Tính nhân vô hướng theo X25519.)
    /// </summary>
    /// <param name="scalar">A 32-byte scalar (after clamping)</param>
    /// <param name="uCoordinate">A 32-byte u-coordinate (for base point, u = 9 encoded as little-endian)</param>
    /// <returns>The resulting 32-byte u-coordinate.</returns>
    private static byte[] ScalarMult(byte[] scalar, byte[] uCoordinate)
    {
        if (scalar.Length != 32 || uCoordinate.Length != 32)
            throw new ArgumentException("Both scalar and u must be 32 bytes.");

        // Convert little-endian byte arrays to BigInteger.
        BigInteger uValue = new(uCoordinate, isUnsigned: true, isBigEndian: false);
        BigInteger scalarValue = new(scalar, isUnsigned: true, isBigEndian: false);

        // Montgomery ladder initialization.
        BigInteger r0X = BigInteger.One;     // Represents the x-coordinate of point R0 (initialized to 1)
        BigInteger r0Z = BigInteger.Zero;    // Represents the z-coordinate of point R0 (initialized to 0)
        BigInteger r1X = uValue;             // Represents the x-coordinate of point R1 (initialized to u)
        BigInteger r1Z = BigInteger.One;     // Represents the z-coordinate of point R1 (initialized to 1)
        int swapFlag = 0;

        // Process 255 bits (bit positions 254 down to 0)
        for (int t = 254; t >= 0; t--)
        {
            int bit = (int)(scalarValue >> t & 1);
            swapFlag ^= bit;
            ConditionalSwap(ref r0X, ref r1X, swapFlag);
            ConditionalSwap(ref r0Z, ref r1Z, swapFlag);
            swapFlag = bit;

            // A = r0X + r0Z
            BigInteger a = (r0X + r0Z) % P;
            // AA = A^2
            BigInteger aa = a * a % P;
            // B = r0X - r0Z
            BigInteger b = (r0X - r0Z + P) % P;
            // BB = B^2
            BigInteger bb = b * b % P;
            // E = AA - BB
            BigInteger e = (aa - bb + P) % P;
            // C = r1X + r1Z
            BigInteger c = (r1X + r1Z) % P;
            // D = r1X - r1Z
            BigInteger d = (r1X - r1Z + P) % P;
            // DA = D * A
            BigInteger da = d * a % P;
            // CB = C * B
            BigInteger cb = c * b % P;
            // r1X = (DA + CB)^2 mod P
            r1X = (da + cb) % P;
            r1X = r1X * r1X % P;
            // r1Z = baseX * (DA - CB)^2 mod P
            BigInteger diff = (da - cb + P) % P;
            BigInteger diffSquared = diff * diff % P;
            r1Z = uValue * diffSquared % P;
            // r0X = AA * BB mod P
            r0X = aa * bb % P;
            // r0Z = E * (AA + A24 * E) mod P
            r0Z = e * ((aa + A24 * e % P) % P) % P;
        }

        ConditionalSwap(ref r0X, ref r1X, swapFlag);
        ConditionalSwap(ref r0Z, ref r1Z, swapFlag);

        // Compute result = r0X / r0Z mod P (multiplying by modular inverse of r0Z)
        BigInteger r0ZInv = ModInverse(r0Z, P);
        BigInteger resultValue = r0X * r0ZInv % P;
        return ToLittleEndianBytes(resultValue, 32);
    }


    /// <summary>
    /// Computes the modular inverse of a modulo p using Fermat's little theorem.
    /// (Tính nghịch đảo modular của a modulo p.)
    /// </summary>
    /// Since p is prime, a^(p-2) mod p is the inverse.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BigInteger ModInverse(BigInteger a, BigInteger p) => BigInteger.ModPow(a, p - 2, p);

    /// <summary>
    /// Converts a BigInteger to a little-endian byte array of fixed length.
    /// (Chuyển BigInteger thành mảng byte little-endian có độ dài cố định.)
    /// </summary>
    private static byte[] ToLittleEndianBytes(BigInteger n, int size)
    {
        byte[] bytes = n.ToByteArray(isUnsigned: true, isBigEndian: false);
        if (bytes.Length > size)
        {
            // Truncate if needed.
            byte[] truncated = new byte[size];
            Array.Copy(bytes, 0, truncated, 0, size);
            return truncated;
        }
        else if (bytes.Length < size)
        {
            // Pad with zeros.
            byte[] padded = new byte[size];
            Array.Copy(bytes, padded, bytes.Length);
            return padded;
        }
        return bytes;
    }

    /// <summary>
    /// Conditional swap of two BigInteger variables if swap == 1.
    /// (Hàm swap có điều kiện của hai BigInteger nếu swap == 1.)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConditionalSwap(ref BigInteger a, ref BigInteger b, int swap)
        => (a, b) = swap != 0 ? (b, a) : (a, b);
}
