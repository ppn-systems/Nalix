// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using Nalix.Framework.Serialization;
using Xunit;

namespace Nalix.Framework.Tests.Serialization;

public sealed class LiteSerializerArrayTests
{
    [Fact]
    public void SerializeDeserialize_IntArray_RoundTripsValues()
    {
        int[] input = [1, 2, 3, 4];
        int[] output = LiteSerializerTestHelper.RoundTrip(input);

        Assert.Equal(input, output);
    }

    [Fact]
    public void SerializeDeserialize_EmptyArray_RoundTripsValues()
    {
        int[] input = [];
        int[] output = LiteSerializerTestHelper.RoundTrip(input);

        Assert.Empty(output);
    }

    [Fact]
    public void SerializeDeserialize_NullArray_RoundTripsNull()
    {
        int[]? input = null;
        byte[] buffer = LiteSerializer.Serialize(input);
        int[]? output = new int[1];

        _ = LiteSerializer.Deserialize(buffer, ref output);

        Assert.Null(output);
    }
}
