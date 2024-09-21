using Nalix.Shared.Serialization;
using System;
using Xunit;

namespace Nalix.Shared.Tests.Serialization;
public class LiteSerializer_Guards_Tests
{
    [Fact]
    public void Deserialize_EmptyBuffer_ThrowsArgumentException()
    {
        var empty = Array.Empty<Byte>();

        Int32 dummy = 0;
        _ = Assert.Throws<ArgumentException>(() => LiteSerializer.Deserialize<Int32>(empty, ref dummy));
    }

    [Fact]
    public void Serialize_ToProvidedBuffer_NullBuffer_Throws()
    {
        var value = 123;
        _ = Assert.Throws<ArgumentNullException>(() => LiteSerializer.Serialize(in value, (Byte[])null!));
    }
}
