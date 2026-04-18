using System;
using System.Security.Cryptography;
using Nalix.Common.Primitives;
using Nalix.Framework.Security.Asymmetric;
using Nalix.Framework.Security.Hashing;
using Nalix.Framework.Security.Symmetric;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math.EC.Rfc7748;
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
        byte[] nalixTag = Poly1305.Compute(key, message);

        // BouncyCastle
        var bc = new Org.BouncyCastle.Crypto.Macs.Poly1305();
        bc.Init(new KeyParameter(key));
        bc.BlockUpdate(message, 0, message.Length);
        byte[] bcTag = new byte[16];
        bc.DoFinal(bcTag, 0);

        Assert.Equal(bcTag, nalixTag);
    }

    [Fact]
    public void X25519_MatchesBouncyCastle()
    {
        // Generate private keys
        byte[] alicePriv = GenerateRandom(32);
        byte[] bobPriv = GenerateRandom(32);

        // Nalix Keys
        var aliceNalix = X25519.GenerateKeyFromPrivateKey(new Bytes32(alicePriv));
        var bobNalix = X25519.GenerateKeyFromPrivateKey(new Bytes32(bobPriv));

        // Nalix Agreement
        byte[] aliceSecret = X25519.Agreement(aliceNalix.PrivateKey, bobNalix.PublicKey).ToArray();

        // BouncyCastle Keys
        byte[] alicePubBC = new byte[32];
        byte[] bobPubBC = new byte[32];
        
        // BC generates public key from private
        Org.BouncyCastle.Math.EC.Rfc7748.X25519.ScalarMultBase(alicePriv, 0, alicePubBC, 0);
        Org.BouncyCastle.Math.EC.Rfc7748.X25519.ScalarMultBase(bobPriv, 0, bobPubBC, 0);

        // Verify public keys match
        Assert.Equal(alicePubBC, aliceNalix.PublicKey.ToArray());
        Assert.Equal(bobPubBC, bobNalix.PublicKey.ToArray());

        // BC Agreement
        byte[] bcSecret = new byte[32];
        Org.BouncyCastle.Math.EC.Rfc7748.X25519.ScalarMult(alicePriv, 0, bobPubBC, 0, bcSecret, 0);

        Assert.Equal(bcSecret, aliceSecret);
    }
}
