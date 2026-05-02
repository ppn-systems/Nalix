// Licensed under the Apache License, Version 2.0.
using System;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.Serialization;
using Nalix.Abstractions.Primitives;
using Nalix.Framework.Identifiers;
using Xunit;

namespace Nalix.Codec.Tests.Serialization;

public sealed class LiteSerializerObjectTests
{
    [Fact]
    public void SerializeDeserialize_Handshake_RoundTripsState()
    {
        Span<byte> proofArr = stackalloc byte[32]; proofArr[0] = 9; proofArr[1] = 8; proofArr[2] = 7; proofArr[3] = 6;
        Span<byte> hashArr = stackalloc byte[32]; "nalix-handshake"u8.CopyTo(hashArr);

        Bytes32 proof = new(proofArr);
        Bytes32 hash = new(hashArr);

        Handshake input = new(
            stage: HandshakeStage.CLIENT_HELLO,
            publicKey: new Bytes32([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32]),
            nonce: new Bytes32([32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1]),
            proof: proof,
            flags: PacketFlags.SYSTEM | PacketFlags.RELIABLE);
        input.TranscriptHash = hash;
        input.SessionToken = Snowflake.NewId(0x01020304, 0x0506, (Nalix.Abstractions.Identity.SnowflakeType)0x07).ToUInt64();

        Handshake? output = null;
        _ = LiteSerializerTestHelper.RoundTrip(input, ref output);

        Assert.NotNull(output);
        Assert.Equal(input.Header.OpCode, output.Header.OpCode);
        Assert.Equal(input.Stage, output.Stage);
        Assert.Equal(input.PublicKey, output.PublicKey);
        Assert.Equal(input.Nonce, output.Nonce);
        Assert.Equal(input.Proof, output.Proof);
        Assert.Equal(input.TranscriptHash, output.TranscriptHash);
        Assert.Equal(input.SessionToken, output.SessionToken);
        Assert.Equal(input.Header.Flags, output.Header.Flags);
        Assert.Equal(input.Header.Priority, output.Header.Priority);
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















