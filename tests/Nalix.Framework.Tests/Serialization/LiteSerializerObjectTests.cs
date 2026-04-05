// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames.SignalFrames;
using Xunit;

namespace Nalix.Framework.Tests.Serialization;

public sealed class LiteSerializerObjectTests
{
    [Fact]
    public void SerializeDeserialize_Handshake_RoundTripsState()
    {
        Handshake input = new(
            opCode: 1,
            data: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32],
            transport: ProtocolType.TCP);

        Handshake? output = null;
        _ = LiteSerializerTestHelper.RoundTrip(input, ref output);

        Assert.NotNull(output);
        Assert.Equal(input.OpCode, output.OpCode);
        Assert.Equal(input.Data, output.Data);
        Assert.Equal(input.Protocol, output.Protocol);
        Assert.Equal(input.Flags, output.Flags);
        Assert.Equal(input.Priority, output.Priority);
    }

    [Fact]
    public void SerializeDeserialize_Object_RoundTripsState()
    {
        TestObject input = new() { Id = 7, Name = "Alice" };
        TestObject? output = null;
        _ = LiteSerializerTestHelper.RoundTrip(input, ref output);

        Assert.NotNull(output);
        Assert.Equal(input.Id, output.Id);
        Assert.Equal(input.Name, output.Name);
    }

    [Fact]
    public void SerializeDeserialize_ObjectGraphWithNullAndNestedCollection_RoundTripsState()
    {
        TestObject input = new()
        {
            Id = 9,
            Name = "Parent",
            Child = new TestObject
            {
                Id = 10,
                Name = "Child",
                Tags = ["x", "y"]
            },
            Tags = []
        };

        TestObject? output = null;
        _ = LiteSerializerTestHelper.RoundTrip(input, ref output);

        Assert.NotNull(output);
        Assert.Equal(input.Id, output.Id);
        Assert.Equal(input.Name, output.Name);
        Assert.NotNull(output.Child);
        Assert.Equal(10, output.Child.Id);
        Assert.Equal("Child", output.Child.Name);
        Assert.Equal(["x", "y"], output.Child.Tags);
        Assert.Empty(output.Tags);
    }

    [Fact]
    public void SerializeDeserialize_ValueStruct_RoundTripsState()
    {
        TestStruct input = new(42, 3.14f);
        TestStruct output = LiteSerializerTestHelper.RoundTrip(input);

        Assert.Equal(input.X, output.X);
        Assert.Equal(input.Y, output.Y, precision: 3);
    }
}
