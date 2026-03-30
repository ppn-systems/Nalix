// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using Nalix.Common.Exceptions;
using Nalix.Framework.Serialization;
using Xunit;

namespace Nalix.Framework.Tests.Serialization;

public class LiteSerializerReferenceTypeTests
{
    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("xin chào tiếng Việt")]
    [InlineData("😀 unicode")]
    public void SerializeDeserializeStringRoundTrip(string input)
    {
        // Nhánh reference type => cần formatter cho string (thường có sẵn)
        byte[] data = LiteSerializer.Serialize(input);

        string output = null;
        int read = LiteSerializer.Deserialize(data, ref output);

        Assert.Equal(data.Length, read);
        Assert.Equal(input, output);
    }

    [Fact]
    public void SerializeToProvidedBufferStringNotSupportedThrows()
    {
        byte[] buf = new byte[128];
        _ = Assert.Throws<SerializationFailureException>(() => LiteSerializer.Serialize("abc", buf));
    }
}
