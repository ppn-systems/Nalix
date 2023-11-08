using Nalix.Shared.Messaging.Binary;
using System;
using Xunit;

namespace Nalix.Shared.Tests.Messaging;

public sealed class Binary256Tests
{
    [Fact]
    public void DynamicSize_Const_Is_256() =>
        // Compile-time constant on subclass is indeed 256
        Assert.Equal(256, Binary256.DynamicSize);

    [Fact]
    public void Initialize_With_UpTo128_Bytes_Succeeds()
    {
        var pkt = new Binary256();
        var data = new Byte[128]; // boundary of base Binary128
        pkt.Initialize(data);     // uses base Initialize

        Assert.Equal(128, pkt.Data.Length);
    }

    [Fact]
    public void Initialize_With_200_Bytes_Throws_Because_Base_Still_128()
    {
        var pkt = new Binary256();
        var data = new Byte[200];

        // EXPECTED: throws, since Binary128.Initialize checks against 128
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => pkt.Initialize(data));
        Assert.Contains("supports at most 128 bytes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
