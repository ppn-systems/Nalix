using System;
using System.Security.Cryptography;
using Nalix.Common.Primitives;
using Nalix.Framework.Security.Asymmetric;
using Nalix.Framework.Security.Hashing;
using Nalix.Framework.Security.Symmetric;
using Nalix.Framework.Security.Aead;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using BCX25519 = Org.BouncyCastle.Math.EC.Rfc7748.X25519;
using BCPoly1305 = Org.BouncyCastle.Crypto.Macs.Poly1305;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

public sealed class InteroperabilityTests
{
    private static byte[] GenerateRandom(int size)
    {
        byte[] data = new byte[size];
        RandomNumberGenerator.Fill(data);
        return data;
    }

    [Fact]
    public void Keccak256_MatchesBouncyCastle()
    {
        byte[] data = GenerateRandom(1024);

        // Nalix
        byte[] nalixHash = Keccak256.HashData(data);

        // BouncyCastle
        KeccakDigest bcDigest = new KeccakDigest(256);
        bcDigest.BlockUpdate(data, 0, data.Length);
        byte[] bcHash = new byte[32];
        bcDigest.DoFinal(bcHash, 0);

        Assert.Equal(bcHash, nalixHash);
    }

    [Fact]
    public void ChaCha20_MatchesBouncyCastle()
    {
        byte[] key = GenerateRandom(32);
        byte[] nonce = GenerateRandom(12); // RFC 7539 (96-bit)
        byte[] plaintext = GenerateRandom(1024);
        byte[] nalixCipher = new byte[1024];

        // Nalix
        var nalix = new ChaCha20(key, nonce, 0);
        nalix.Encrypt(plaintext, nalixCipher);

        // BouncyCastle
        var bc = new ChaCha7539Engine();
        bc.Init(true, new ParametersWithIV(new KeyParameter(key), nonce));
        byte[] bcCipher = new byte[1024];
        bc.ProcessBytes(plaintext, 0, plaintext.Length, bcCipher, 0);

        Assert.Equal(bcCipher, nalixCipher);
    }

    [Fact]
    public void Salsa20_MatchesBouncyCastle()
    {
        byte[] key = GenerateRandom(32);
        byte[] nonce = GenerateRandom(8); // Standard 64-bit nonce
        byte[] plaintext = GenerateRandom(1024);
        byte[] nalixCipher = new byte[1024];

        // Nalix
        Salsa20.Encrypt(key, nonce, 0, plaintext, nalixCipher);

        // BouncyCastle
        var bc = new Salsa20Engine();
        bc.Init(true, new ParametersWithIV(new KeyParameter(key), nonce));
        byte[] bcCipher = new byte[1024];
        bc.ProcessBytes(plaintext, 0, plaintext.Length, bcCipher, 0);

        Assert.Equal(bcCipher, nalixCipher);
    }

    [Fact]
    public void Poly1305_MatchesBouncyCastle()
    {
        byte[] key = GenerateRandom(32);
        byte[] message = GenerateRandom(1024);

        // Nalix
        byte[] nalixTag = Nalix.Framework.Security.Hashing.Poly1305.Compute(key, message);

        // BouncyCastle
        var bc = new BCPoly1305();
        bc.Init(new KeyParameter(key));
        bc.BlockUpdate(message, 0, message.Length);
        byte[] bcTag = new byte[16];
        bc.DoFinal(bcTag, 0);

        Assert.Equal(bcTag, nalixTag);
    }

    [Fact]
    public void ChaCha20Poly1305_MatchesBouncyCastle()
    {
        byte[] key = GenerateRandom(32);
        byte[] nonce = GenerateRandom(12);
        byte[] aad = GenerateRandom(64);
        byte[] plaintext = GenerateRandom(1024);
        byte[] nalixCipher = new byte[1024];
        byte[] nalixTag = new byte[16];

        // Nalix
        Nalix.Framework.Security.Aead.ChaCha20Poly1305.Encrypt(key, nonce, plaintext, aad, nalixCipher, nalixTag);

        // BouncyCastle
        var engine = new Org.BouncyCastle.Crypto.Modes.ChaCha20Poly1305();
        engine.Init(true, new AeadParameters(new KeyParameter(key), 128, nonce, aad));
        byte[] bcOutput = new byte[plaintext.Length + 16];
        int len = engine.ProcessBytes(plaintext, 0, plaintext.Length, bcOutput, 0);
        engine.DoFinal(bcOutput, len);

        byte[] bcCipher = bcOutput[..plaintext.Length];
        byte[] bcTag = bcOutput[plaintext.Length..];

        Assert.Equal(bcCipher, nalixCipher);
        Assert.Equal(bcTag, nalixTag);
    }

    [Fact]
    public void Salsa20Poly1305_MatchesBouncyCastleConstruction()
    {
        byte[] key = GenerateRandom(32);
        byte[] nonce = GenerateRandom(8);
        byte[] aad = GenerateRandom(64);
        byte[] plaintext = GenerateRandom(1024);
        byte[] nalixCipher = new byte[1024];
        byte[] nalixTag = new byte[16];

        // Nalix
        Nalix.Framework.Security.Aead.Salsa20Poly1305.Encrypt(key, nonce, plaintext, aad, nalixCipher, nalixTag);

        // BouncyCastle Manual Construction
        // 1. Derive PolyKey (from block 0)
        var salsa0 = new Salsa20Engine();
        salsa0.Init(true, new ParametersWithIV(new KeyParameter(key), nonce));
        byte[] polyKey = new byte[32];
        salsa0.ProcessBytes(new byte[32], 0, 32, polyKey, 0);

        // 2. Encrypt (from block 1 onwards)
        var salsa1 = new Salsa20Engine();
        salsa1.Init(true, new ParametersWithIV(new KeyParameter(key), nonce));
        salsa1.ProcessBytes(new byte[64], 0, 64, new byte[64], 0); // Skip block 0
        byte[] bcCipher = new byte[1024];
        salsa1.ProcessBytes(plaintext, 0, plaintext.Length, bcCipher, 0);

        // 3. MAC Transcript: AAD || pad16 || CT || pad16 || len(AAD) || len(CT)
        var poly = new BCPoly1305();
        poly.Init(new KeyParameter(polyKey));
        
        poly.BlockUpdate(aad, 0, aad.Length);
        Pad16(poly, aad.Length);
        poly.BlockUpdate(bcCipher, 0, bcCipher.Length);
        Pad16(poly, bcCipher.Length);
        
        byte[] lens = new byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(lens, (ulong)aad.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(lens.AsSpan(8), (ulong)bcCipher.Length);
        poly.BlockUpdate(lens, 0, 16);
        
        byte[] bcTag = new byte[16];
        poly.DoFinal(bcTag, 0);

        Assert.Equal(bcCipher, nalixCipher);
        Assert.Equal(bcTag, nalixTag);
    }

    private static void Pad16(Org.BouncyCastle.Crypto.IMac mac, int length)
    {
        int rem = length % 16;
        if (rem != 0)
        {
            mac.BlockUpdate(new byte[16 - rem], 0, 16 - rem);
        }
    }

    [Fact]
    public void HmacKeccak256_MatchesBouncyCastle()
    {
        byte[] key = GenerateRandom(64);
        byte[] data = GenerateRandom(1024);
        byte[] nalixTag = new byte[32];

        // Nalix
        HmacKeccak256.Compute(key, data, nalixTag);

        // BouncyCastle
        var digest = new KeccakDigest(256);
        var hmac = new HMac(digest);
        hmac.Init(new KeyParameter(key));
        hmac.BlockUpdate(data, 0, data.Length);
        byte[] bcTag = new byte[32];
        hmac.DoFinal(bcTag, 0);

        Assert.Equal(bcTag, nalixTag);
    }

    [Fact]
    public void X25519_MatchesBouncyCastle()
    {
        // 1. Generate random private keys
        byte[] alicePriv = GenerateRandom(32);
        byte[] bobPriv = GenerateRandom(32);

        // 2. Clone for clamping (RFC 7748)
        byte[] alicePrivClamped = (byte[])alicePriv.Clone();
        alicePrivClamped[0] &= 248;
        alicePrivClamped[31] &= 127;
        alicePrivClamped[31] |= 64;

        byte[] bobPrivClamped = (byte[])bobPriv.Clone();
        bobPrivClamped[0] &= 248;
        bobPrivClamped[31] &= 127;
        bobPrivClamped[31] |= 64;

        // 3. Nalix Keys
        var aliceNalix = Nalix.Framework.Security.Asymmetric.X25519.GenerateKeyFromPrivateKey(new Bytes32(alicePrivClamped));
        var bobNalix = Nalix.Framework.Security.Asymmetric.X25519.GenerateKeyFromPrivateKey(new Bytes32(bobPrivClamped));

        // 4. Nalix Agreement
        byte[] aliceSecret = Nalix.Framework.Security.Asymmetric.X25519.Agreement(aliceNalix.PrivateKey, bobNalix.PublicKey).ToByteArray();

        // 5. BouncyCastle Keys
        byte[] alicePubBC = new byte[32];
        byte[] bobPubBC = new byte[32];
        BCX25519.ScalarMultBase(alicePrivClamped, 0, alicePubBC, 0);
        BCX25519.ScalarMultBase(bobPrivClamped, 0, bobPubBC, 0);

        // 6. Verify Public Keys Match
        Assert.Equal(alicePubBC, aliceNalix.PublicKey.ToByteArray());
        Assert.Equal(bobPubBC, bobNalix.PublicKey.ToByteArray());

        // 7. BC Agreement
        byte[] bcSecret = new byte[32];
        BCX25519.ScalarMult(alicePrivClamped, 0, bobPubBC, 0, bcSecret, 0);

        Assert.Equal(bcSecret, aliceSecret);
    }
}
