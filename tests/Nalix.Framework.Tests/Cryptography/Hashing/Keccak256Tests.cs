// Copyright (c) 2025 PPN Corporation. All rights reserved.

using System;
using System.Text;
using FluentAssertions;
using Nalix.Framework.Cryptography.Hashing;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography.Hashing;

/// <summary>
/// Property-based and edge-case tests for the custom Keccak256 (non-standard).
/// These tests validate internal consistency, not equivalence to FIPS 202.
/// </summary>
public sealed class Keccak256Tests
{
    #region Helpers

    /// <summary>
    /// Computes hash using one-shot API.
    /// </summary>
    private static Byte[] OneShot(Byte[] data)
    {
        return Keccak256.HashData(data);
    }

    /// <summary>
    /// Computes hash using incremental Update() with arbitrary chunking.
    /// </summary>
    private static Byte[] Chunked(Byte[] data, Int32[] chunkSizes)
    {
        using var h = new Keccak256();
        Int32 offset = 0;
        foreach (Int32 size in chunkSizes)
        {
            Int32 take = Math.Min(size, data.Length - offset);
            if (take <= 0)
            {
                break;
            }

            h.Update(new ReadOnlySpan<Byte>(data, offset, take));
            offset += take;
        }
        if (offset < data.Length)
        {
            h.Update(new ReadOnlySpan<Byte>(data, offset, data.Length - offset));
        }
        return h.Finish();
    }

    /// <summary>
    /// Generates a deterministic pseudo-random byte array for test reproducibility.
    /// </summary>
    private static Byte[] MakeBytes(Int32 len, Int32 seed)
    {
        var rng = new Random(seed);
        var b = new Byte[len];
        rng.NextBytes(b);
        return b;
    }

    /// <summary>
    /// Splits length into pseudo-random chunk sizes (to exercise partial-block logic).
    /// </summary>
    private static Int32[] MakeChunks(Int32 totalLen, Int32 seed)
    {
        var rnd = new Random(seed);
        var sizes = new System.Collections.Generic.List<Int32>();
        Int32 left = totalLen;
        while (left > 0)
        {
            // Favor a mix around rate (136), small tails, and large bursts.
            Int32 size = rnd.Next(1, 300);
            sizes.Add(size);
            left -= size;
        }
        return sizes.ToArray();
    }

    #endregion

    #region Determinism & Basic Equivalence

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(15)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(135)] // RateBytes - 1
    [InlineData(136)] // RateBytes
    [InlineData(137)] // RateBytes + 1
    [InlineData(1024)]
    [InlineData(4096)]
    public void OneShot_Is_Deterministic_And_Stable(Int32 len)
    {
        var data = MakeBytes(len, seed: 12345);
        var a = OneShot(data);
        var b = OneShot(data);
        a.Should().Equal(b, "same input must yield identical digest");
        a.Length.Should().Be(32);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(135)]
    [InlineData(136)]
    [InlineData(137)]
    [InlineData(999)]
    public void OneShot_Equals_Chunked(Int32 len)
    {
        var data = MakeBytes(len, seed: 2025);
        var oneShot = OneShot(data);
        var chunked = Chunked(data, MakeChunks(len, seed: 7));
        chunked.Should().Equal(oneShot, "incremental Update must match one-shot");
    }

    [Fact]
    public void HashData_Span_Overload_Writes_Into_Provided_Buffer()
    {
        var data = Encoding.ASCII.GetBytes("Nalix.Keccak256 custom variant");
        Span<Byte> buf = stackalloc Byte[32];
        Keccak256.HashData(data, buf);
        buf.ToArray().Should().Equal(OneShot(data));
    }

    #endregion

    #region Padding Edge Cases

    /// <summary>
    /// Ensures padding works across boundaries:
    /// - exact multiple of rate
    /// - rate - 1 (forces the 0x80 into next block path)
    /// - rate + 0/1/2 etc.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(136 - 1)]  // 135
    [InlineData(136)]      // 136
    [InlineData(136 + 1)]  // 137
    [InlineData(2 * 136 - 1)] // 271
    [InlineData(2 * 136)]     // 272
    [InlineData(2 * 136 + 1)] // 273
    public void Padding_Edges_Are_Consistent(Int32 len)
    {
        var data = MakeBytes(len, seed: 999);
        var a = OneShot(data);
        var b = Chunked(data, new[] { 1, 2, 7, 31, 64, 128, 256 }); // crafted split set
        a.Should().Equal(b);
    }

    [Fact]
    public void Empty_Input_Is_Stable()
    {
        var a = OneShot(Array.Empty<Byte>());
        var b = Chunked(Array.Empty<Byte>(), Array.Empty<Int32>());
        a.Should().Equal(b);
    }

    #endregion

    #region Finish, Initialize, Dispose Semantics

    [Fact]
    public void Finish_Is_Idempotent_And_Cached()
    {
        using var h = new Keccak256();
        h.Update(Encoding.ASCII.GetBytes("hello"));
        var x = h.Finish();
        var y = h.Finish(); // should return cached clone
        x.Should().Equal(y);
        // Mutating returned array must not affect internal cache
        x[0] ^= 0xFF;
        h.Finish().Should().NotEqual(x);
    }

    [Fact]
    public void Update_After_Finish_Throws()
    {
        using var h = new Keccak256();
        h.Update(Encoding.ASCII.GetBytes("abc"));
        _ = h.Finish();
        Action act = () => h.Update(new global::System.Byte[] { 1, 2, 3 });
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Initialize_Resets_State_For_Reuse()
    {
        using var h = new Keccak256();

        h.Update(Encoding.ASCII.GetBytes("first"));
        var first = h.Finish();

        h.Initialize(); // reuse instance for new message
        h.Update(Encoding.ASCII.GetBytes("second"));
        var second = h.Finish();

        first.Should().NotEqual(second, "different messages after Initialize must yield different digests");
        // Recompute with new one-shot to ensure determinism
        second.Should().Equal(OneShot(Encoding.ASCII.GetBytes("second")));
    }

    [Fact]
    public void Dispose_Clears_And_Blocks_Further_Use()
    {
        var h = new Keccak256();
        h.Update(Encoding.ASCII.GetBytes("dispose-me"));
        h.Dispose();

        Action act1 = () => h.Update(new global::System.Byte[] { 1, 2, 3 });
        Action act2 = () => h.Finish();

        act1.Should().Throw<ObjectDisposedException>();
        act2.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Consistency Across Overloads

    [Theory]
    [InlineData("abc")]
    [InlineData("The quick brown fox jumps over the lazy dog")]
    [InlineData("Keccak256 (Nalix variant)")]
    public void ComputeHash_Equals_Finish_And_Static(String text)
    {
        var data = Encoding.UTF8.GetBytes(text);

        using var h = new Keccak256();
        h.Update(data);
        var viaFinish = h.Finish();

        using var h2 = new Keccak256();
        var viaCompute = h2.ComputeHash(data);
        var viaStatic = Keccak256.HashData(data);

        viaCompute.Should().Equal(viaFinish);
        viaStatic.Should().Equal(viaFinish);
    }

    [Fact]
    public void Finish_Span_And_Array_Produce_Same_Result()
    {
        var data = MakeBytes(777, seed: 42);

        using var h1 = new Keccak256();
        h1.Update(data);
        var arr = h1.Finish();

        using var h2 = new Keccak256();
        h2.Update(data);
        Span<Byte> buf = stackalloc Byte[32];
        h2.Finish(buf);

        buf.ToArray().Should().Equal(arr);
    }

    #endregion

    #region “Endian-stability” sanity

    /// <summary>
    /// We cannot switch machine endianness in tests, but we still assert:
    /// - Output size is always 32 bytes.
    /// - The function is stable for the same input on this architecture.
    /// (Implementation aims to be deterministic across endianness.)
    /// </summary>
    [Fact]
    public void Output_Is_Always_32_Bytes_And_Stable_On_This_Arch()
    {
        var data = MakeBytes(3333, seed: 1337);
        var a = OneShot(data);
        var b = OneShot(data);
        a.Length.Should().Be(32);
        a.Should().Equal(b);
    }

    #endregion

    #region Larger Regression Sweep

    [Theory]
    [InlineData(5, 0)]
    [InlineData(5, 1)]
    [InlineData(5, 2)]
    [InlineData(5, 3)]
    [InlineData(5, 4)]
    public void Random_Sweep_Various_Lengths_And_Chunkings(Int32 seedBase, Int32 k)
    {
        // Sweep multiple lengths including near rate boundaries and larger sizes.
        Int32[] lengths = { 0, 1, 2, 7, 15, 31, 63, 64, 127, 135, 136, 137, 255, 511, 1024, 4096 };
        foreach (var len in lengths)
        {
            var data = MakeBytes(len, (seedBase * 1000) + k);
            var a = OneShot(data);
            var b = Chunked(data, MakeChunks(len, seedBase * 100 + k));
            a.Should().Equal(b, $"len={len}");
        }
    }

    #endregion

}
