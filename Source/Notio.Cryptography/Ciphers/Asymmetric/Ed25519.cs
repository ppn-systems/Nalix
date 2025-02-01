using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;

namespace Notio.Cryptography.Ciphers.Asymmetric;

public sealed class Ed25519
{
    private const int PublicKeySize = 32;
    private const int SignatureSize = 64;

    // Precomputed constants
    private static readonly BigInteger Q = BigInteger.Parse("57896044618658097711785492504343953926634992332820282019728792003956564819949");

    private static readonly BigInteger L = BigInteger.Parse("7237005577332262213973186563042994240857116359379907606001950938285454250989");
    private static readonly BigInteger D = BigInteger.Parse("-4513249062541557337682894930092624173785641285191125241628941591882900924598840740");
    private static readonly BigInteger I = BigInteger.Parse("19681161376707505956807079304988542015446066515923890162744021073123829784752");

    // Base point B
    private static readonly Point B = new(
        BigInteger.Parse("15112221349535400772501151409588531511454012693041857206046113283949847762202").Mod(Q),
        BigInteger.Parse("46316835694926478169428394003475163141307993866256256256850187133169737347974").Mod(Q)
    );

    // Point struct (immutable)
    private readonly struct Point(BigInteger x, BigInteger y)
    {
        public readonly BigInteger X = x;
        public readonly BigInteger Y = y;
    }

    // Optimized SHA-512 with buffer reuse (thread-local instance)
    private static readonly ThreadLocal<SHA512> Sha512 = new(() => SHA512.Create());

    private static byte[] ComputeHash(ReadOnlySpan<byte> data)
        => (Sha512.Value ?? SHA512.Create()).ComputeHash(data.ToArray());

    // Fast modular inverse using Fermat’s little theorem
    private static BigInteger Inv(BigInteger x) => BigInteger.ModPow(x, Q - 2, Q);

    // Optimized point addition on Edwards curve
    private static Point Edwards(Point p, Point q)
    {
        var a = p.Y.ModAdd(p.X, Q);
        var b = q.Y.ModAdd(q.X, Q);
        var c = p.Y.ModSub(p.X, Q);
        var d = q.Y.ModSub(q.X, Q);
        var e = a.MultiplyMod(b, Q);
        var f = c.MultiplyMod(d, Q);

        // Precompute factor for x3 and y3
        var factor = D.MultiplyMod(e.MultiplyMod(f, Q), Q);
        var inv1 = Inv(factor.ModAdd(BigInteger.One, Q));
        var inv2 = Inv(BigInteger.One.ModSub(factor, Q));

        var x3 = e.ModSub(f, Q).MultiplyMod(inv1, Q);
        var y3 = e.ModAdd(f, Q).MultiplyMod(inv2, Q);
        return new Point(x3, y3);
    }

    // Double-and-add scalar multiplication
    private static Point ScalarMul(Point p, BigInteger e)
    {
        Point result = new(BigInteger.Zero, BigInteger.One);
        Point current = p;
        while (e > 0)
        {
            if (!e.IsEven)
                result = Edwards(result, current);
            current = Edwards(current, current);
            e >>= 1;
        }
        return result;
    }

    // Memory-efficient signature generation
    public static byte[] Sign(byte[] message, byte[] privateKey)
    {
        // Compute the hash of the private key and split into two halves
        var h = ComputeHash(privateKey);
        var a = ClampScalar(h.AsSpan(0, 32));
        ReadOnlySpan<byte> prefix = h.AsSpan(32, 32);

        // r = Hash(prefix || message) mod L, using Span overload
        var r = HashToScalar(prefix, message);
        var R = ScalarMul(B, r);

        // Compute public key A = ScalarMul(B, a) and encode it
        var A = ScalarMul(B, a);
        Span<byte> AEncoded = stackalloc byte[PublicKeySize];
        EncodePoint(A, AEncoded);

        // Build the data: R (32 bytes) || AEncoded (32 bytes) || message
        byte[] data = new byte[32 + PublicKeySize + message.Length];
        EncodePoint(R, data.AsSpan(0, 32));
        AEncoded.CopyTo(data.AsSpan(32, PublicKeySize));
        message.CopyTo(data, 64);

        // s = (r + Hash(data) * a) mod L
        var s = (r + HashToScalar(data.AsSpan())) * a;
        s %= L; // Using Mod extension below

        // Create signature: R (32 bytes) || s (32 bytes)
        byte[] signature = new byte[SignatureSize];
        EncodePoint(R, signature.AsSpan(0, 32));
        EncodeScalar(s, signature.AsSpan(32, 32));
        return signature;
    }

    // Optimized verification with batch operations
    public static bool Verify(byte[] signature, byte[] message, byte[] publicKey)
    {
        if (signature.Length != SignatureSize)
            return false;

        // Decode R, A, and s from the signature and publicKey
        var R = DecodePoint(signature.AsSpan(0, 32));
        var A = DecodePoint(publicKey);
        var s = DecodeScalar(signature.AsSpan(32, 32));

        // Build data: R (32 bytes) || publicKey (32 bytes) || message
        byte[] data = new byte[64 + message.Length];
        signature.AsSpan(0, 32).CopyTo(data.AsSpan(0, 32));
        publicKey.CopyTo(data, 32);
        message.CopyTo(data, 64);

        var h = HashToScalar(data.AsSpan());
        var sB = ScalarMul(B, s);
        var hA = ScalarMul(A, h);
        var RplusH = Edwards(R, hA);
        return PointEquals(sB, RplusH);
    }

    // Helper: Clamp the scalar as per Ed25519 specifications
    private static BigInteger ClampScalar(ReadOnlySpan<byte> s)
    {
        // Create a 32-byte buffer to modify bits as needed
        Span<byte> scalarBytes = stackalloc byte[32];
        s.CopyTo(scalarBytes);
        // Clear/Set bits: clear lowest 3 bits, clear highest bit, set second highest bit
        scalarBytes[0] &= 0xF8;
        scalarBytes[31] &= 0x7F;
        scalarBytes[31] |= 0x40;
        return new BigInteger(scalarBytes, isUnsigned: true, isBigEndian: true) % L;
    }

    // Hash data into a scalar value modulo L
    private static BigInteger HashToScalar(ReadOnlySpan<byte> data)
        => new BigInteger(ComputeHash(data), isUnsigned: true, isBigEndian: true) % L;

    private static BigInteger HashToScalar(ReadOnlySpan<byte> prefix, byte[] message)
    {
        Span<byte> buffer = message.Length <= 1024
            ? stackalloc byte[prefix.Length + message.Length]
            : new byte[prefix.Length + message.Length];

        prefix.CopyTo(buffer);
        message.CopyTo(buffer[prefix.Length..]);
        return new BigInteger(ComputeHash(buffer), isUnsigned: true, isBigEndian: true) % L;
    }

    // Encode a point to a fixed 32-byte representation using TryWriteBytes to avoid allocations.
    private static void EncodePoint(Point p, Span<byte> destination)
    {
        // Encode y coordinate as 32 bytes in big-endian order
        if (!p.Y.TryWriteBytes(destination, out _, isUnsigned: true, isBigEndian: true))
        {
            throw new InvalidOperationException("Failed to encode point y coordinate.");
        }
        // Set the most significant bit to indicate x's parity
        if (!p.X.IsEven)
            destination[^1] |= 0x80;
    }

    private static Point DecodePoint(ReadOnlySpan<byte> data)
    {
        // Decode y coordinate (32 bytes big-endian)
        var y = new BigInteger(data, isUnsigned: true, isBigEndian: true);
        var x = RecoverX(y);
        // Use the high bit of the last byte to recover x parity
        bool xParity = (data[^1] & 0x80) != 0;
        if (x.IsEven != !xParity)
            x = Q - x;
        return new Point(x, y);
    }

    private static BigInteger RecoverX(BigInteger y)
    {
        // Recover x using curve equation: x^2 = (y^2 - 1) / (D*y^2 + 1)
        var numerator = (y * y - BigInteger.One).Mod(Q);
        var denominator = (D * y * y + BigInteger.One).Mod(Q);
        var xx = numerator * Inv(denominator) % Q;
        var x = BigInteger.ModPow(xx, (Q + 3) / 8, Q);
        return (x * x % Q == xx) ? x : (x * I) % Q;
    }

    private static bool PointEquals(Point a, Point b)
        => a.X == b.X && a.Y == b.Y;

    // Encode a scalar to a fixed 32-byte representation
    private static void EncodeScalar(BigInteger s, Span<byte> destination)
    {
        if (!s.TryWriteBytes(destination, out int bytesWritten, isUnsigned: true, isBigEndian: true))
            throw new InvalidOperationException("Failed to encode scalar.");
        // If fewer than 32 bytes were written, pad the beginning with zeros.
        if (bytesWritten < destination.Length)
        {
            destination[..^bytesWritten].Clear();
        }
    }

    private static BigInteger DecodeScalar(ReadOnlySpan<byte> data)
        => new BigInteger(data, isUnsigned: true, isBigEndian: true) % L;
}

internal static class BigIntegerExtensions
{
    public static BigInteger ModAdd(this BigInteger a, BigInteger b, BigInteger mod)
    {
        a += b;
        if (a >= mod)
            a -= mod;
        else if (a < 0)
            a += mod;
        return a;
    }

    public static BigInteger ModSub(this BigInteger a, BigInteger b, BigInteger mod)
    {
        a -= b;
        if (a < 0)
            a += mod;
        else if (a >= mod)
            a -= mod;
        return a;
    }

    public static BigInteger MultiplyMod(this BigInteger a, BigInteger b, BigInteger mod)
        => (a * b) % mod;

    public static BigInteger Mod(this BigInteger num, BigInteger modulo)
    {
        var result = num % modulo;
        return result < 0 ? result + modulo : result;
    }
}