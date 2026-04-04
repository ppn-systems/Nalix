// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using Xunit;

namespace Nalix.Framework.Tests.Serialization;

public sealed class LiteSerializerStringTests
{
    [Theory]
    [InlineData("Hello")]
    [InlineData("")]
    public void SerializeDeserialize_String_RoundTripsValue(string input)
    {
        string output = LiteSerializerTestHelper.RoundTrip(input);
        Assert.Equal(input, output);
    }
}
