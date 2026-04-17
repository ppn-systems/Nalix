// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Text;
using Nalix.Common.Primitives;
using Nalix.Framework.Security;
using Nalix.Framework.Security.Hashing;
using Nalix.Framework.Security.Primitives;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

public sealed class CryptoUtilityTests
{
    [Fact]
    public void BitwiseFixedTimeEqualsReturnsExpectedValues()
    {
        ReadOnlySpan<byte> a = [1, 2, 3];
        ReadOnlySpan<byte> b = [1, 2, 3];
        ReadOnlySpan<byte> c = [1, 2, 4];
        ReadOnlySpan<byte> d = [1, 2];

        Assert.True(BitwiseOperations.FixedTimeEquals(a, b));
        Assert.False(BitwiseOperations.FixedTimeEquals(a, c));
        Assert.False(BitwiseOperations.FixedTimeEquals(a, d));
    }

    [Fact]
    public void BitwiseIsZeroReturnsExpectedValues()
    {
        Assert.True(BitwiseOperations.IsZero(ReadOnlySpan<byte>.Empty));
        Assert.True(BitwiseOperations.IsZero([0, 0, 0]));
        Assert.False(BitwiseOperations.IsZero([0, 1, 0]));
    }

    [Fact]
    public void MemorySecurityZeroMemoryClearsArrayAndSpan()
    {
        byte[] arr = [1, 2, 3];
        Span<byte> span = stackalloc byte[] { 4, 5, 6 };

        MemorySecurity.ZeroMemory(arr);
        MemorySecurity.ZeroMemory(span);
        MemorySecurity.ZeroMemory((byte[])null!);
        MemorySecurity.ZeroMemory(Array.Empty<byte>());

        Assert.Equal([0, 0, 0], arr);
        Assert.True(span.ToArray().SequenceEqual(new byte[] { 0, 0, 0 }));
    }

    [Fact]
    public void Keccak256HashDataMatchesKnownVectorForEmptyInput()
    {
        byte[] hash = Keccak256.HashData(ReadOnlySpan<byte>.Empty);

        Assert.Equal(
            "C5D2460186F7233C927E7DB2DCC703C0E500B653CA82273B7BFAD8045D85A470",
            Convert.ToHexString(hash));
    }

    [Fact]
    public void Keccak256HashDataMatchesKnownVectorForAbc()
    {
        byte[] hash = Keccak256.HashData(Encoding.ASCII.GetBytes("abc"));

        Assert.Equal(
            "4E03657AEA45A94FC7D47BA826C8D667C0D1E6E33A64A036EC44F58FA12D6C45",
            Convert.ToHexString(hash));
    }

    [Fact]
    public void Keccak256TryHashDataReturnsFalseWhenOutputIsTooSmall()
    {
        Span<byte> output = stackalloc byte[16];

        bool ok = Keccak256.TryHashData([1, 2, 3], output);

        Assert.False(ok);
    }

    [Fact]
    public void HmacKeccak256ComputeIsDeterministicAndSensitiveToKey()
    {
        byte[] key1 = Encoding.ASCII.GetBytes("key-one");
        byte[] key2 = Encoding.ASCII.GetBytes("key-two");
        byte[] data = Encoding.ASCII.GetBytes("payload");
        Span<byte> mac1 = stackalloc byte[32];
        Span<byte> mac2 = stackalloc byte[32];
        Span<byte> mac3 = stackalloc byte[32];

        HmacKeccak256.Compute(key1, data, mac1);
        HmacKeccak256.Compute(key1, data, mac2);
        HmacKeccak256.Compute(key2, data, mac3);

        Assert.True(mac1.SequenceEqual(mac2));
        Assert.False(mac1.SequenceEqual(mac3));
    }

    [Fact]
    public void HmacKeccak256WhenOutputTooSmallThrowsArgumentException()
    {
        Span<byte> output = stackalloc byte[31];
        ArgumentException? exception = null;
        try
        {
            HmacKeccak256.Compute([1], [2], output);
        }
        catch (ArgumentException ex)
        {
            exception = ex;
        }

        Assert.NotNull(exception);
    }

    [Fact]
    public void HandshakeComposeTranscriptBufferWritesLengthPrefixedSegments()
    {
        Bytes32 cpk = Fill(0x11);
        Bytes32 cnonce = Fill(0x22);
        Bytes32 spk = Fill(0x33);
        Bytes32 snonce = Fill(0x44);

        byte[] buffer = HandshakeX25519.ComposeTranscriptBuffer(cpk, cnonce, spk, snonce);

        Assert.Equal((4 * 4) + (Bytes32.Size * 4), buffer.Length);
        Assert.Equal(Bytes32.Size, BitConverter.ToInt32(buffer, 0));
        Assert.Equal(0x11, buffer[4]);
        Assert.Equal(Bytes32.Size, BitConverter.ToInt32(buffer, 36));
        Assert.Equal(0x22, buffer[40]);
    }

    [Fact]
    public void HandshakeProofsAndSessionKeyAreDeterministicAndDomainSeparated()
    {
        Bytes32 secret = Fill(0xAA);
        Bytes32 transcript = Fill(0xBB);
        Bytes32 cnonce = Fill(0xCC);
        Bytes32 snonce = Fill(0xDD);

        Bytes32 server1 = HandshakeX25519.ComputeServerProof(secret, transcript);
        Bytes32 server2 = HandshakeX25519.ComputeServerProof(secret, transcript);
        Bytes32 client = HandshakeX25519.ComputeClientProof(secret, transcript);
        Bytes32 finish = HandshakeX25519.ComputeServerFinishProof(secret, transcript);
        Bytes32 session1 = HandshakeX25519.DeriveSessionKey(secret, cnonce, snonce, transcript);
        Bytes32 session2 = HandshakeX25519.DeriveSessionKey(secret, cnonce, snonce, transcript);
        Bytes32 sessionDiff = HandshakeX25519.DeriveSessionKey(secret, Fill(0xCE), snonce, transcript);

        Assert.Equal(server1, server2);
        Assert.NotEqual(server1, client);
        Assert.NotEqual(server1, finish);
        Assert.Equal(session1, session2);
        Assert.NotEqual(session1, sessionDiff);
    }

    private static Bytes32 Fill(byte value)
    {
        byte[] bytes = Enumerable.Repeat(value, Bytes32.Size).Select(static x => (byte)x).ToArray();
        return new Bytes32(bytes);
    }
}
