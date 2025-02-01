using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;

namespace Notio.Cryptography.Ciphers.Asymmetric
{
    public sealed class Ed25519
    {
        private const int PublicKeySize = 32;
        private const int SignatureSize = 64;

        // Precomputed constants
        private static readonly BigInteger Q = BigInteger.Parse("57896044618658097711785492504343953926634992332820282019728792003956564819949");

        private static readonly BigInteger L = BigInteger.Parse("7237005577332262213973186563042994240857116359379907606001950938285454250989");
        private static readonly BigInteger D = BigInteger.Parse("-4513249062541557337682894930092624173785641285191125241628941591882900924598840740");
        private static readonly BigInteger I = BigInteger.Parse("19681161376707505956807079304988542015446066515923890162744021073123829784752");

        private static readonly Point B = new(
            BigInteger.Parse("15112221349535400772501151409588531511454012693041857206046113283949847762202").Mod(Q),
            BigInteger.Parse("46316835694926478169428394003475163141307993866256256256850187133169737347974").Mod(Q)
        );

        private readonly struct Point(BigInteger x, BigInteger y)
        {
            public readonly BigInteger X = x;
            public readonly BigInteger Y = y;
        }

        // Optimized SHA-512 with buffer reuse
        private static readonly ThreadLocal<SHA512> Sha512 = new(SHA512.Create);

        private static byte[] ComputeHash(byte[] data)
            => (Sha512.Value ?? SHA512.Create()).ComputeHash(data);

        // Fast modular inverse using precomputed Q-2
        private static BigInteger Inv(BigInteger x) => BigInteger.ModPow(x, Q - 2, Q);

        // Optimized point operations using inlined math
        private static Point Edwards(Point p, Point q)
        {
            var a = p.Y.ModAdd(p.X, Q);
            var b = q.Y.ModAdd(q.X, Q);
            var c = p.Y.ModSub(p.X, Q);
            var d = q.Y.ModSub(q.X, Q);
            var e = a.MultiplyMod(b, Q);
            var f = c.MultiplyMod(d, Q);
            var x3 = e.ModSub(f, Q).MultiplyMod(Inv(D.MultiplyMod(e.MultiplyMod(f, Q), Q).ModAdd(1, Q)), Q);
            var y3 = e.ModAdd(f, Q).MultiplyMod(Inv(BigInteger.One.ModSub(D.MultiplyMod(e.MultiplyMod(f, Q), Q), Q)), Q);
            return new Point(x3, y3);
        }

        // Double-and-add scalar multiplication
        private static Point ScalarMul(Point p, BigInteger e)
        {
            Point result = new(0, 1);
            Point current = p;
            while (e > 0)
            {
                if (!e.IsEven) result = Edwards(result, current);
                current = Edwards(current, current);
                e >>= 1;
            }
            return result;
        }

        // Memory-efficient signature generation
        public static byte[] Sign(byte[] message, byte[] privateKey)
        {
            var h = ComputeHash(privateKey);
            var a = ClampScalar(h.AsSpan(0, 32));
            var prefix = h.AsSpan(32, 32);

            var r = HashToScalar(prefix.ToArray(), message);
            var R = ScalarMul(B, r);

            var data = new byte[32 + PublicKeySize + message.Length];
            EncodePoint(R).CopyTo(data, 0);
            ScalarMul(B, a).X.ToByteArray().CopyTo(data, 32);
            message.CopyTo(data, 64);

            var s = (r + HashToScalar(data) * a).Mod(L);

            var signature = new byte[SignatureSize];
            EncodePoint(R).CopyTo(signature, 0);
            EncodeScalar(s).CopyTo(signature, 32);
            return signature;
        }

        // Optimized verification with batch operations
        public static bool Verify(byte[] signature, byte[] message, byte[] publicKey)
        {
            if (signature.Length != SignatureSize) return false;

            var R = DecodePoint(signature.AsSpan(0, 32));
            var A = DecodePoint(publicKey);
            var s = DecodeScalar(signature.AsSpan(32, 32));

            var data = new byte[64 + message.Length];
            signature.AsSpan(0, 32).CopyTo(data);
            publicKey.CopyTo(data, 32);
            message.CopyTo(data, 64);
            var h = HashToScalar(data);
            var sB = ScalarMul(B, s);
            var hA = ScalarMul(A, h);
            var RplusH = Edwards(R, hA);

            return PointEquals(sB, RplusH);
        }

        // Helper methods
        private static BigInteger ClampScalar(ReadOnlySpan<byte> s)
        {
            var scalar = new BigInteger(s, true) & (BigInteger.One << 254) - 8 | BigInteger.One << 254;
            return scalar.Mod(L);
        }

        private static BigInteger HashToScalar(ReadOnlySpan<byte> data)
            => new BigInteger(ComputeHash(data.ToArray()), true).Mod(L);

        private static BigInteger HashToScalar(byte[] prefix, byte[] message)
        {
            var data = new byte[prefix.Length + message.Length];
            prefix.CopyTo(data, 0);
            message.CopyTo(data, prefix.Length);
            return new BigInteger(ComputeHash(data), true).Mod(L);
        }

        private static byte[] EncodePoint(Point p)
        {
            var y = p.Y.ToByteArray(true, true);
            Array.Resize(ref y, 32);
            y[31] |= (byte)(p.X.IsEven ? 0 : 0x80);
            return y;
        }

        private static Point DecodePoint(ReadOnlySpan<byte> data)
        {
            var y = new BigInteger(data, true);
            var x = RecoverX(y);
            if ((x.IsEven ? 0 : 1) != data[31] >> 7) x = Q - x;
            return new Point(x, y);
        }

        private static BigInteger RecoverX(BigInteger y)
        {
            var xx = (y * y - 1) * Inv(D * y * y + 1);
            var x = BigInteger.ModPow(xx, (Q + 3) / 8, Q);
            return x * x == xx ? x : x * I % Q;
        }

        private static bool PointEquals(Point a, Point b)
            => a.X == b.X && a.Y == b.Y;

        private static byte[] EncodeScalar(BigInteger s)
            => s.ToByteArray(true, true);

        private static BigInteger DecodeScalar(ReadOnlySpan<byte> data)
            => new BigInteger(data, true).Mod(L);
    }

    internal static class BigIntegerExtensions
    {
        public static BigInteger ModAdd(this BigInteger a, BigInteger b, BigInteger mod)
        {
            a += b;
            return a >= mod ? a - mod : a < 0 ? a + mod : a;
        }

        public static BigInteger ModSub(this BigInteger a, BigInteger b, BigInteger mod)
        {
            a -= b;
            return a < 0 ? a + mod : a >= mod ? a - mod : a;
        }

        public static BigInteger MultiplyMod(this BigInteger a, BigInteger b, BigInteger mod) => a * b % mod;

        public static BigInteger Mod(this BigInteger num, BigInteger modulo)
        {
            var result = num % modulo;
            return result < 0 ? result + modulo : result;
        }
    }
}