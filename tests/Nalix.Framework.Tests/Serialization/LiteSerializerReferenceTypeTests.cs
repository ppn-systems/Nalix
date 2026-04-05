// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using Nalix.Common.Exceptions;
using Nalix.Framework.Serialization;
using Xunit;

namespace Nalix.Framework.Tests.Serialization;

public sealed class LiteSerializerReferenceTypeTests
{
    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("xin chào tiếng Việt")]
    [InlineData("😀 unicode")]
    public void SerializeDeserialize_StringReference_RoundTripsValue(string input)
    {
        byte[] data = LiteSerializer.Serialize(input);
        string output = LiteSerializerTestHelper.RoundTrip(input);

        Assert.Equal(data.Length, data.Length);
        Assert.Equal(input, output);
    }

    [Fact]
    public void SerializeToProvidedBuffer_String_ThrowsSerializationFailureException()
    {
        byte[] buffer = new byte[128];
        _ = Assert.Throws<SerializationFailureException>(() => LiteSerializer.Serialize("abc", buffer));
    }
}
