// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Text;
using Nalix.Codec.Security.Hashing;
using Nalix.Codec.Security.Symmetric;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

public sealed class Poly1305Tests
{
    private static byte[] HexToBytes(string hex)
    {
        byte[] result = new byte[hex.Length / 2];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return result;
    }

    /// <summary>
    /// RFC 8439 §2.5.2 example tag.
    /// </summary>
    [Fact]
    public void ComputeMatchesRfc8439Section2_5_2Example()
    {
        byte[] key = HexToBytes(
            "85d6be7857556d337f4452fe42d506a8" +
            "0103808afb0db2fd4abff6af4149f51b");
        byte[] message = Encoding.ASCII.GetBytes("Cryptographic Forum Research Group");
        byte[] expectedTag = HexToBytes("a8061dc1305136c6c22b8baf0c0127a9");

        byte[] tag = Poly1305.Compute(key, message);

        Assert.Equal(expectedTag, tag);
    }

    /// <summary>
    /// RFC 8439 Appendix A.3, Test Vector #1: all-zero key, all-zero 64-byte message.
    /// </summary>
    [Fact]
    public void ComputeMatchesRfc8439AppendixA3TestVector1()
    {
        byte[] key = new byte[Poly1305.KeySize];
        byte[] message = new byte[64];
        byte[] expectedTag = new byte[Poly1305.TagSize];

        byte[] tag = Poly1305.Compute(key, message);

        Assert.Equal(expectedTag, tag);
    }

    /// <summary>
    /// RFC 8439 Appendix A.3, Test Vector #2: r = 0 weak-key case.
    /// </summary>
    [Fact]
    public void ComputeMatchesRfc8439AppendixA3TestVector2()
    {
        byte[] key = HexToBytes(
            "00000000000000000000000000000000" +
            "36e5f6b5c5e06070f0efca96227a863e");
        byte[] message = Encoding.ASCII.GetBytes(
            "Any submission to the IETF intended by the Contributor for " +
            "publication as all or part of an IETF Internet-Draft or RFC " +
            "and any statement made within the context of an IETF " +
            "activity is considered an \"IETF Contribution\". Such " +
            "statements include oral statements in IETF sessions, as well " +
            "as written and electronic communications made at any time or " +
            "place, which are addressed to");
        byte[] expectedTag = HexToBytes("36e5f6b5c5e06070f0efca96227a863e");

        byte[] tag = Poly1305.Compute(key, message);

        Assert.Equal(expectedTag, tag);
    }

    /// <summary>
    /// RFC 8439 Appendix A.3, Test Vector #3.
    /// </summary>
    [Fact]
    public void ComputeMatchesRfc8439AppendixA3TestVector3()
    {
        byte[] key = HexToBytes(
            "36e5f6b5c5e06070f0efca96227a863e" +
            "00000000000000000000000000000000");
        byte[] message = Encoding.ASCII.GetBytes(
            "Any submission to the IETF intended by the Contributor for " +
            "publication as all or part of an IETF Internet-Draft or RFC " +
            "and any statement made within the context of an IETF " +
            "activity is considered an \"IETF Contribution\". Such " +
            "statements include oral statements in IETF sessions, as well " +
            "as written and electronic communications made at any time or " +
            "place, which are addressed to");
        byte[] expectedTag = HexToBytes("f3477e7cd95417af89a6b8794c310cf0");

        byte[] tag = Poly1305.Compute(key, message);

        Assert.Equal(expectedTag, tag);
    }

    /// <summary>
    /// RFC 8439 Appendix A.3, Test Vector #4.
    /// </summary>
    [Fact]
    public void ComputeMatchesRfc8439AppendixA3TestVector4()
    {
        byte[] key = HexToBytes(
            "1c9240a5eb55d38af333888604f6b5f0" +
            "473917c1402b80099dca5cbc207075c0");
        byte[] message = Encoding.ASCII.GetBytes(
            "'Twas brillig, and the slithy toves\n" +
            "Did gyre and gimble in the wabe:\n" +
            "All mimsy were the borogoves,\n" +
            "And the mome raths outgrabe.");
        byte[] expectedTag = HexToBytes("4541669a7eaaee61e708dc7cbcc5eb62");

        byte[] tag = Poly1305.Compute(key, message);

        Assert.Equal(expectedTag, tag);
    }

    private static byte[] BuildKey(string rHex16Bytes, string sHex16Bytes)
    {
        byte[] key = new byte[Poly1305.KeySize];
        HexToBytes(rHex16Bytes).CopyTo(key, 0);
        HexToBytes(sHex16Bytes).CopyTo(key, 16);
        return key;
    }

    /// <summary>
    /// RFC 8439 Appendix A.3, Test Vector #5: 130-bit partial reduction edge case.
    /// </summary>
    [Fact]
    public void ComputeMatchesRfc8439AppendixA3TestVector5()
    {
        byte[] key = BuildKey(
            "02000000000000000000000000000000",
            "00000000000000000000000000000000");
        byte[] message = HexToBytes("ffffffffffffffffffffffffffffffff");
        byte[] expectedTag = HexToBytes("03000000000000000000000000000000");

        byte[] tag = Poly1305.Compute(key, message);

        Assert.Equal(expectedTag, tag);
    }

    /// <summary>
    /// RFC 8439 Appendix A.3, Test Vector #6: addition of s overflows modulo 2^128.
    /// </summary>
    [Fact]
    public void ComputeMatchesRfc8439AppendixA3TestVector6()
    {
        byte[] key = BuildKey(
            "02000000000000000000000000000000",
            "ffffffffffffffffffffffffffffffff");
        byte[] message = HexToBytes("02000000000000000000000000000000");
        byte[] expectedTag = HexToBytes("03000000000000000000000000000000");

        byte[] tag = Poly1305.Compute(key, message);

        Assert.Equal(expectedTag, tag);
    }

    /// <summary>
    /// RFC 8439 Appendix A.3, Test Vector #7: data limb all-ones with carry from lower limb.
    /// </summary>
    [Fact]
    public void ComputeMatchesRfc8439AppendixA3TestVector7()
    {
        byte[] key = BuildKey(
            "01000000000000000000000000000000",
            "00000000000000000000000000000000");
        byte[] message = HexToBytes(
            "ffffffffffffffffffffffffffffffff" +
            "f0ffffffffffffffffffffffffffffff" +
            "11000000000000000000000000000000");
        byte[] expectedTag = HexToBytes("05000000000000000000000000000000");

        byte[] tag = Poly1305.Compute(key, message);

        Assert.Equal(expectedTag, tag);
    }

    /// <summary>
    /// RFC 8439 Appendix A.3, Test Vector #8: polynomial part equals exactly 2^130-5.
    /// </summary>
    [Fact]
    public void ComputeMatchesRfc8439AppendixA3TestVector8()
    {
        byte[] key = BuildKey(
            "01000000000000000000000000000000",
            "00000000000000000000000000000000");
        byte[] message = HexToBytes(
            "ffffffffffffffffffffffffffffffff" +
            "fbfefefefefefefefefefefefefefefe" +
            "01010101010101010101010101010101");
        byte[] expectedTag = new byte[Poly1305.TagSize];

        byte[] tag = Poly1305.Compute(key, message);

        Assert.Equal(expectedTag, tag);
    }

    /// <summary>
    /// RFC 8439 Appendix A.3, Test Vector #9: polynomial part equals exactly 2^130-6.
    /// </summary>
    [Fact]
    public void ComputeMatchesRfc8439AppendixA3TestVector9()
    {
        byte[] key = BuildKey(
            "02000000000000000000000000000000",
            "00000000000000000000000000000000");
        byte[] message = HexToBytes("fdffffffffffffffffffffffffffffff");
        byte[] expectedTag = HexToBytes("faffffffffffffffffffffffffffffff");

        byte[] tag = Poly1305.Compute(key, message);

        Assert.Equal(expectedTag, tag);
    }

    /// <summary>
    /// RFC 8439 Appendix A.3, Test Vector #10: 5*H+L-type reduction, 131-bit intermediate.
    /// </summary>
    [Fact]
    public void ComputeMatchesRfc8439AppendixA3TestVector10()
    {
        byte[] key = BuildKey(
            "01000000000000000400000000000000",
            "00000000000000000000000000000000");
        byte[] message = HexToBytes(
            "e33594d7505e43b90000000000000000" +
            "3394d7505e4379cd0100000000000000" +
            "00000000000000000000000000000000" +
            "01000000000000000000000000000000");
        byte[] expectedTag = HexToBytes("14000000000000005500000000000000");

        byte[] tag = Poly1305.Compute(key, message);

        Assert.Equal(expectedTag, tag);
    }

    /// <summary>
    /// RFC 8439 Appendix A.3, Test Vector #11: 5*H+L-type reduction, 131-bit final result.
    /// </summary>
    [Fact]
    public void ComputeMatchesRfc8439AppendixA3TestVector11()
    {
        byte[] key = BuildKey(
            "01000000000000000400000000000000",
            "00000000000000000000000000000000");
        byte[] message = HexToBytes(
            "e33594d7505e43b90000000000000000" +
            "3394d7505e4379cd0100000000000000" +
            "00000000000000000000000000000000");
        byte[] expectedTag = HexToBytes("13000000000000000000000000000000");

        byte[] tag = Poly1305.Compute(key, message);

        Assert.Equal(expectedTag, tag);
    }

    /// <summary>
    /// RFC 8439 §2.6.2 example: derive the Poly1305 one-time key via
    /// ChaCha20 block counter 0.
    /// </summary>
    [Fact]
    public void GenerateKeyBlockMatchesRfc8439Section2_6_2Example()
    {
        byte[] key = HexToBytes(
            "808182838485868788898a8b8c8d8e8f" +
            "909192939495969798999a9b9c9d9e9f");
        byte[] nonce = HexToBytes("000000000001020304050607");
        byte[] expectedOneTimeKey = HexToBytes(
            "8ad5a08b905f81cc815040274ab29471" +
            "a833b637e3fd0da508dbb8e2fdd1a646");

        ChaCha20 cipher = new(key, nonce, 0u);
        byte[] block = new byte[ChaCha20.BlockSize];
        cipher.GenerateKeyBlock(block);

        Assert.Equal(expectedOneTimeKey, block.AsSpan(0, 32).ToArray());
    }

    /// <summary>
    /// RFC 8439 Appendix A.4, Test Vector #1: all-zero key/nonce.
    /// </summary>
    [Fact]
    public void GenerateKeyBlockMatchesRfc8439AppendixA4TestVector1()
    {
        byte[] key = new byte[ChaCha20.KeySize];
        byte[] nonce = new byte[ChaCha20.NonceSize];
        byte[] expectedOneTimeKey = HexToBytes(
            "76b8e0ada0f13d90405d6ae55386bd28" +
            "bdd219b8a08ded1aa836efcc8b770dc7");

        ChaCha20 cipher = new(key, nonce, 0u);
        byte[] block = new byte[ChaCha20.BlockSize];
        cipher.GenerateKeyBlock(block);

        Assert.Equal(expectedOneTimeKey, block.AsSpan(0, 32).ToArray());
    }

    /// <summary>
    /// RFC 8439 Appendix A.4, Test Vector #2: key ends in 0x01, nonce ends in 0x02.
    /// </summary>
    [Fact]
    public void GenerateKeyBlockMatchesRfc8439AppendixA4TestVector2()
    {
        byte[] key = new byte[ChaCha20.KeySize];
        key[31] = 1;
        byte[] nonce = new byte[ChaCha20.NonceSize];
        nonce[11] = 2;
        byte[] expectedOneTimeKey = HexToBytes(
            "ecfa254f845f647473d3cb140da9e876" +
            "06cb33066c447b87bc2666dde3fbb739");

        ChaCha20 cipher = new(key, nonce, 0u);
        byte[] block = new byte[ChaCha20.BlockSize];
        cipher.GenerateKeyBlock(block);

        Assert.Equal(expectedOneTimeKey, block.AsSpan(0, 32).ToArray());
    }

    /// <summary>
    /// RFC 8439 Appendix A.4, Test Vector #3.
    /// </summary>
    [Fact]
    public void GenerateKeyBlockMatchesRfc8439AppendixA4TestVector3()
    {
        byte[] key = HexToBytes(
            "1c9240a5eb55d38af333888604f6b5f0" +
            "473917c1402b80099dca5cbc207075c0");
        byte[] nonce = HexToBytes("000000000000000000000002");
        byte[] expectedOneTimeKey = HexToBytes(
            "965e3bc6f9ec7ed9560808f4d229f94b" +
            "137ff275ca9b3fcbdd59deaad23310ae");

        ChaCha20 cipher = new(key, nonce, 0u);
        byte[] block = new byte[ChaCha20.BlockSize];
        cipher.GenerateKeyBlock(block);

        Assert.Equal(expectedOneTimeKey, block.AsSpan(0, 32).ToArray());
    }

    [Fact]
    public void ComputeIsDeterministicForSameInput()
    {
        byte[] key = HexToBytes("85d6be7857556d337f4452fe42d506a80103808afb0db2fd4abff6af4149f51b");
        byte[] message = Encoding.ASCII.GetBytes("Cryptographic Forum Research Group");
        byte[] tag1 = Poly1305.Compute(key, message);
        byte[] tag2 = Poly1305.Compute(key, message);

        Assert.Equal(Poly1305.TagSize, tag1.Length);
        Assert.Equal(tag1, tag2);
        Assert.NotEqual(new byte[Poly1305.TagSize], tag1);
    }

    [Fact]
    public void IncrementalUpdateAndFinalizeMatchOneShotCompute()
    {
        byte[] key = HexToBytes("85d6be7857556d337f4452fe42d506a80103808afb0db2fd4abff6af4149f51b");
        byte[] message = Encoding.ASCII.GetBytes("Cryptographic Forum Research Group");

        byte[] oneShot = Poly1305.Compute(key, message);
        byte[] incremental = new byte[Poly1305.TagSize];

        Poly1305 poly = new(key);
        try
        {
            poly.Update(message.AsSpan(0, 10));
            poly.Update(message.AsSpan(10, 7));
            poly.Update(message.AsSpan(17));
            poly.FinalizeTag(incremental);
        }
        finally
        {
            poly.Clear();
        }

        Assert.Equal(oneShot, incremental);
    }

    [Fact]
    public void VerifyReturnsTrueForValidTagAndFalseForTamperedTag()
    {
        byte[] key = HexToBytes("85d6be7857556d337f4452fe42d506a80103808afb0db2fd4abff6af4149f51b");
        byte[] message = Encoding.ASCII.GetBytes("Cryptographic Forum Research Group");
        byte[] tag = Poly1305.Compute(key, message);

        Assert.True(Poly1305.Verify(key, message, tag));

        tag[0] ^= 0xFF;
        Assert.False(Poly1305.Verify(key, message, tag));
    }

    [Fact]
    public void VerifyWhenTagLengthInvalidThrowsArgumentException()
    {
        byte[] key = new byte[Poly1305.KeySize];
        byte[] message = [1, 2, 3];
        byte[] invalidTag = new byte[Poly1305.TagSize - 1];

        _ = Assert.Throws<ArgumentException>(() => Poly1305.Verify(key, message, invalidTag));
    }

    [Fact]
    public void ComputeWhenKeyOrDestinationInvalidThrowsArgumentException()
    {
        byte[] invalidKey = new byte[Poly1305.KeySize - 1];
        byte[] message = [1, 2, 3];
        byte[] tooSmallDestination = new byte[Poly1305.TagSize - 1];

        _ = Assert.Throws<ArgumentException>(() => Poly1305.Compute(invalidKey, message, new byte[Poly1305.TagSize]));
        _ = Assert.Throws<ArgumentException>(() => Poly1305.Compute(new byte[Poly1305.KeySize], message, tooSmallDestination));
    }

    [Fact]
    public void FinalizeTwiceOrUpdateAfterFinalizeThrowsInvalidOperationException()
    {
        byte[] key = new byte[Poly1305.KeySize];
        byte[] output = new byte[Poly1305.TagSize];

        Poly1305 poly = new(key);
        try
        {
            poly.Update([1, 2, 3]);
            poly.FinalizeTag(output);

            bool threw1 = false;
            try { poly.FinalizeTag(output); }
            catch (InvalidOperationException) { threw1 = true; }
            Assert.True(threw1);

            bool threw2 = false;
            try { poly.Update([4, 5]); }
            catch (InvalidOperationException) { threw2 = true; }
            Assert.True(threw2);
        }
        finally
        {
            poly.Clear();
        }
    }

    [Fact]
    public void OperationsAfterClearThrowObjectDisposedException()
    {
        byte[] key = new byte[Poly1305.KeySize];
        byte[] output = new byte[Poly1305.TagSize];

        Poly1305 poly = new(key);
        poly.Clear();

        bool threw1 = false;
        try { poly.ComputeTag([1, 2], output); }
        catch (ObjectDisposedException) { threw1 = true; }
        Assert.True(threw1);

        bool threw2 = false;
        try { poly.Update([1, 2]); }
        catch (ObjectDisposedException) { threw2 = true; }
        Assert.True(threw2);

        bool threw3 = false;
        try { poly.FinalizeTag(output); }
        catch (ObjectDisposedException) { threw3 = true; }
        Assert.True(threw3);
    }
}















