// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Nalix.Common.Exceptions;
using Nalix.Common.Identity;
using Nalix.Framework.Identifiers;
using Xunit;

namespace Nalix.Framework.Tests.Identity;

public class IdentifierTests
{
    [Fact]
    public void CreateEmptyIsEmptyTrueAndSerializesTo7ZeroBytes()
    {
        Snowflake id = Snowflake.Empty; // all zero
        Assert.True(id.IsEmpty); // not empty => false

        Span<byte> buf = stackalloc byte[7];
        Assert.True(id.TryWriteBytes(buf, out int written));
        Assert.Equal(7, written);
        Assert.Equal(new byte[7], buf.ToArray());
    }

    [Fact]
    public void NewIdWithFixedComponentsSerializesLittleEndianAndRoundTrips()
    {
        const uint value = 0x11223344;
        const ushort machine = 0x5566;
        const SnowflakeType type = (SnowflakeType)0x77;

        Snowflake id = Snowflake.NewId(value, machine, type);
        Assert.Equal(value, id.Value);
        Assert.Equal(machine, id.MachineId);
        Assert.Equal(type, id.Type);

        Span<byte> buf = stackalloc byte[7];
        Assert.True(id.TryWriteBytes(buf, out int written));
        Assert.Equal(7, written);

        // little-endian layout: [0..3]=Value, [4..5]=Machine, [6]=Type  (core impl)
        Assert.Equal(0x44, buf[0]);
        Assert.Equal(0x33, buf[1]);
        Assert.Equal(0x22, buf[2]);
        Assert.Equal(0x11, buf[3]);
        Assert.Equal(0x66, buf[4]);
        Assert.Equal(0x55, buf[5]);
        Assert.Equal(0x77, buf[6]);

        Snowflake back = Snowflake.FromBytes(buf);
        Assert.Equal(id, back);
        Assert.True(id == back);
    }

    [Fact]
    public void TrySerializeToByteSpanTooSmallReturnsFalse()
    {
        Snowflake id = Snowflake.NewId(0xAABBCCDD, 0xEEFF, (SnowflakeType)0x12);
        Span<byte> small = stackalloc byte[6];
        Assert.False(id.TryWriteBytes(small, out int written));
        Assert.Equal(0, written);
    }

    [Fact]
    public void SerializeReturnsNew7ByteArray()
    {
        Snowflake id = Snowflake.NewId(0x01020304, 0x0506, (SnowflakeType)0x07);
        byte[] arr = id.ToByteArray();
        Assert.Equal(7, arr.Length);
        Assert.Equal(new byte[] { 0x04, 0x03, 0x02, 0x01, 0x06, 0x05, 0x07 }, arr);
    }

    [Fact]
    public void DeserializeFromByteArrayInvalidLengthThrows()
    {
        _ = Assert.Throws<SerializationFailureException>(() => Snowflake.FromBytes([]));
        _ = Assert.Throws<SerializationFailureException>(() => Snowflake.FromBytes(new byte[6]));
        _ = Assert.Throws<SerializationFailureException>(() => Snowflake.FromBytes(new byte[8]));
    }

    [Fact]
    public void HexStringMatchesSerializedBytesLength14()
    {
        Snowflake id = Snowflake.NewId(0x00112233, 0x4455, (SnowflakeType)0x66);
        Span<byte> buf = stackalloc byte[7];
        Assert.True(id.TryWriteBytes(buf, out _));
        string hex = id.ToString(); // Convert.ToHexString(7 bytes) => 14 hex chars
        Assert.Equal(Convert.ToHexString(buf.ToArray()), hex);
        Assert.Equal(14, hex.Length);
    }

    [Fact]
    public void EqualityAndHashCodeUseAllComponents()
    {
        Snowflake id1 = Snowflake.NewId(0xAAAAAAAA, 0xBBBB, (SnowflakeType)0xCC);
        Snowflake id2 = Snowflake.NewId(0xAAAAAAAA, 0xBBBB, (SnowflakeType)0xCC);
        Snowflake id3 = Snowflake.NewId(0xAAAAAAAA, 0xBBBB, (SnowflakeType)0xCD);

        // Same components => equal
        Assert.True(id1.Equals(id2));
        Assert.True(id1 == id2);
        Assert.False(id1 != id2);
        Assert.Equal(id1.GetHashCode(), id2.GetHashCode());

        // Different type => not equal
        Assert.False(id1.Equals(id3));
        Assert.True(id1 != id3);

        // Dictionary key usage stable
        Dictionary<Snowflake, int> dict = new() { [id1] = 42 };
        Assert.True(dict.TryGetValue(id2, out int v));
        Assert.Equal(42, v);
    }

    [Fact]
    public void RandomNewIdByTypeAndMachineRoundTripsFromBytes()
    {
        // NewId(type, machineId) uses SecureRandom for Value; we just check round-trip
        Snowflake id = Snowflake.NewId((SnowflakeType)0x11, machineId: 0x2222);
        Span<byte> buf = stackalloc byte[7];
        Assert.True(id.TryWriteBytes(buf, out _));
        Snowflake back = Snowflake.FromBytes(buf);
        Assert.Equal(id, back);
    }

    [Fact]
    public void UnsafeSizeOfIs7ForExplicitLayout()
    {
        int size = Unsafe.SizeOf<Snowflake>();
        Assert.Equal(7, size);
    }

    [Fact]
    public void TryParseWhenHexIsValidReturnsTrueAndParsedValue()
    {
        Snowflake source = Snowflake.NewId(0x12345678, 0x99AA, (SnowflakeType)0x11);
        string hex = source.ToString();

        bool ok = Snowflake.TryParse(hex, out Snowflake parsed);

        Assert.True(ok);
        Assert.Equal(source, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("ABC")]
    [InlineData("00112233445566ZZ")]
    public void TryParseWhenInputIsInvalidReturnsFalseAndEmpty(string? input)
    {
        bool ok = Snowflake.TryParse(input, out Snowflake parsed);

        Assert.False(ok);
        Assert.True(parsed.IsEmpty);
    }

    [Fact]
    public void FromBytesWhenArrayIsNullThrowsSerializationFailureException()
    {
        byte[]? bytes = null;

        _ = Assert.Throws<SerializationFailureException>(() => Snowflake.FromBytes(bytes!));
    }

    [Fact]
    public void FromUInt56RoundTripsToSameIdentifier()
    {
        Snowflake source = Snowflake.NewId(0xDEADBEEF, 0xABCD, (SnowflakeType)0x22);
        Snowflake roundTripped = Snowflake.FromUInt56(source.ToUInt56());

        Assert.Equal(source, roundTripped);
    }

    [Fact]
    public void CompareAndOperatorsFollowUnderlyingOrder()
    {
        Snowflake smaller = Snowflake.NewId(1, 1, SnowflakeType.System);
        Snowflake bigger = Snowflake.NewId(2, 1, SnowflakeType.System);

        Assert.True(smaller < bigger);
        Assert.True(smaller <= bigger);
        Assert.True(bigger > smaller);
        Assert.True(bigger >= smaller);
        Assert.Equal(-1, Math.Sign(Snowflake.Compare(smaller, bigger)));
        Assert.Equal(1, Math.Sign(Snowflake.Compare(bigger, smaller)));
        Assert.Equal(0, Snowflake.Compare(smaller, smaller));
    }

    [Fact]
    public void EqualsWithISnowflakeDifferentImplementationReturnsFalse()
    {
        Snowflake id = Snowflake.NewId(123, 4, SnowflakeType.System);
        ISnowflake other = new StubSnowflake();

        Assert.False(id.Equals(other));
    }

    private sealed class StubSnowflake : ISnowflake
    {
        public bool IsEmpty => false;
        public SnowflakeType Type => SnowflakeType.System;
        public uint Value => 1;
        public ushort MachineId => 1;
        public Nalix.Common.Primitives.UInt56 ToUInt56() => Nalix.Common.Primitives.UInt56.Zero;
        public byte[] ToByteArray() => new byte[7];
        public bool TryWriteBytes(Span<byte> destination) => false;
        public bool TryWriteBytes(Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            return false;
        }
    }
}
