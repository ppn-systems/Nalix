using Nalix.Shared.Serialization;
using System;
using Xunit;

namespace Nalix.Shared.Tests.Serialization;
public class LiteSerializer_ReferenceType_Tests
{
    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("xin chào tiếng Việt")]
    [InlineData("😀 unicode")]
    public void SerializeDeserialize_String_RoundTrip(String input)
    {
        // Nhánh reference type => cần formatter cho string (thường có sẵn)
        Byte[] data = LiteSerializer.Serialize(input);

        String output = null;
        Int32 read = LiteSerializer.Deserialize<String>(data, ref output);

        Assert.Equal(data.Length, read);
        Assert.Equal(input, output);
    }

    [Fact]
    public void Serialize_ToProvidedBuffer_String_NotSupported_Throws()
    {
        var buf = new Byte[128];
        _ = Assert.Throws<NotSupportedException>(() => LiteSerializer.Serialize("abc", buf));
    }

    [Fact]
    public void Serialize_ToSpan_String_NotSupported_Throws()
    {
        _ = Assert.Throws<NotSupportedException>(() =>
        {
            Span<Byte> span = stackalloc Byte[128];
            _ = LiteSerializer.Serialize("abc", span);
        });
    }
}
