using System;
using System.Numerics;
using System.Security.Cryptography;

namespace Notio.Cryptography.Ciphers.Asymmetric;

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
        RandomNumberGenerator.Fill(privateKey);
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
        // Both keys should be 32 bytes.
        if (privateKey.Length != 32 || peerPublicKey.Length != 32)
            throw new ArgumentException("Keys must be 32 bytes.");

        return ScalarMult(privateKey, peerPublicKey);
    }

    /// <summary>
    /// Clamps a 32-byte scalar for X25519 as specified in RFC 7748.
    /// (Cách này đảm bảo scalar được "clamp" theo quy định của RFC 7748.)
    /// </summary>
    public static byte[] ClampScalar(byte[] scalar)
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
    /// <param name="scalarBytes">A 32-byte scalar (after clamping)</param>
    /// <param name="uBytes">A 32-byte u-coordinate (for base point, u = 9 encoded as little-endian)</param>
    /// <returns>The resulting 32-byte u-coordinate.</returns>
    public static byte[] ScalarMult(byte[] scalarBytes, byte[] uBytes)
    {
        if (scalarBytes.Length != 32 || uBytes.Length != 32)
            throw new ArgumentException("Both scalar and u must be 32 bytes.");

        // Convert little-endian byte arrays to BigInteger.
        BigInteger u = new(uBytes, isUnsigned: true, isBigEndian: false);
        BigInteger scalar = new(scalarBytes, isUnsigned: true, isBigEndian: false);

        // Montgomery ladder initialization.
        BigInteger x1 = u;
        BigInteger x2 = BigInteger.One;
        BigInteger z2 = BigInteger.Zero;
        BigInteger x3 = u;
        BigInteger z3 = BigInteger.One;
        int swap = 0;

        // Process 255 bits (bit positions 254 down to 0).
        for (int t = 254; t >= 0; t--)
        {
            int k_t = (int)(scalar >> t & 1);
            swap ^= k_t;
            ConditionalSwap(ref x2, ref x3, swap);
            ConditionalSwap(ref z2, ref z3, swap);
            swap = k_t;

            // A = x2 + z2
            BigInteger A = (x2 + z2) % P;
            // AA = A^2
            BigInteger AA = A * A % P;
            // B = x2 - z2
            BigInteger B = (x2 - z2 + P) % P;
            // BB = B^2
            BigInteger BB = B * B % P;
            // E = AA - BB
            BigInteger E = (AA - BB + P) % P;
            // C = x3 + z3
            BigInteger C = (x3 + z3) % P;
            // D = x3 - z3
            BigInteger D = (x3 - z3 + P) % P;
            // DA = D * A
            BigInteger DA = D * A % P;
            // CB = C * B
            BigInteger CB = C * B % P;
            // x3 = (DA + CB)^2
            x3 = (DA + CB) % P;
            x3 = x3 * x3 % P;
            // z3 = x1 * (DA - CB)^2
            BigInteger tmp = (DA - CB + P) % P;
            tmp = tmp * tmp % P;
            z3 = x1 * tmp % P;
            // x2 = AA * BB
            x2 = AA * BB % P;
            // z2 = E * (AA + A24 * E)
            z2 = E * ((AA + A24 * E % P) % P) % P;
        }

        ConditionalSwap(ref x2, ref x3, swap);
        ConditionalSwap(ref z2, ref z3, swap);

        // Compute result = x2 / z2 mod p. (Multiply by modular inverse of z2)
        BigInteger z2Inv = ModInverse(z2, P);
        BigInteger result = x2 * z2Inv % P;
        return ToLittleEndianBytes(result, 32);
    }

    /// <summary>
    /// Computes the modular inverse of a modulo p using Fermat's little theorem.
    /// (Tính nghịch đảo modular của a modulo p.)
    /// </summary>
    private static BigInteger ModInverse(BigInteger a, BigInteger p)
    {
        // Since p is prime, a^(p-2) mod p is the inverse.
        return BigInteger.ModPow(a, p - 2, p);
    }

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
    private static void ConditionalSwap(ref BigInteger a, ref BigInteger b, int swap)
    {
        if (swap != 0)
        {
            (b, a) = (a, b);
        }
    }
}