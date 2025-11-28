using Nalix.Common.Enums;
using Nalix.Framework.Identity;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Xunit;

namespace Nalix.Framework.Tests.Identity;

public class IdentifierTests
{
    [Fact]
    public void CreateEmpty_IsEmpty_True_And_SerializesTo7ZeroBytes()
    {
        var id = Snowflake.Empty; // all zero
        Assert.True(id.IsEmpty); // not empty => false

        Span<Byte> buf = stackalloc Byte[7];
        Assert.True(id.TryWriteBytes(buf, out var written));
        Assert.Equal(7, written);
        Assert.Equal(new Byte[7], buf.ToArray());
    }

    [Fact]
    public void NewId_WithFixedComponents_SerializesLittleEndian_And_RoundTrips()
    {
        const UInt32 value = 0x11223344;
        const UInt16 machine = 0x5566;
        const SnowflakeType type = (SnowflakeType)0x77;

        var id = Snowflake.NewId(value, machine, type);
        Assert.Equal(value, id.Value);
        Assert.Equal(machine, id.MachineId);
        Assert.Equal(type, id.Type);

        Span<Byte> buf = stackalloc Byte[7];
        Assert.True(id.TryWriteBytes(buf, out var written));
        Assert.Equal(7, written);

        // little-endian layout: [0..3]=Value, [4..5]=Machine, [6]=Type  (core impl)
        Assert.Equal(0x44, buf[0]);
        Assert.Equal(0x33, buf[1]);
        Assert.Equal(0x22, buf[2]);
        Assert.Equal(0x11, buf[3]);
        Assert.Equal(0x66, buf[4]);
        Assert.Equal(0x55, buf[5]);
        Assert.Equal(0x77, buf[6]);

        var back = Snowflake.FromBytes(buf);
        Assert.Equal(id, back);
        Assert.True(id == back);
    }

    [Fact]
    public void TrySerialize_ToByteSpan_TooSmall_ReturnsFalse()
    {
        var id = Snowflake.NewId(0xAABBCCDD, 0xEEFF, (SnowflakeType)0x12);
        Span<Byte> small = stackalloc Byte[6];
        Assert.False(id.TryWriteBytes(small, out var written));
        Assert.Equal(0, written);
    }

    [Fact]
    public void Serialize_ReturnsNew7ByteArray()
    {
        var id = Snowflake.NewId(0x01020304, 0x0506, (SnowflakeType)0x07);
        var arr = id.ToByteArray();
        Assert.Equal(7, arr.Length);
        Assert.Equal(new Byte[] { 0x04, 0x03, 0x02, 0x01, 0x06, 0x05, 0x07 }, arr);
    }

    [Fact]
    public void Deserialize_FromByteArray_InvalidLength_Throws()
    {
        _ = Assert.Throws<ArgumentException>(() => Snowflake.FromBytes([]));
        _ = Assert.Throws<ArgumentException>(() => Snowflake.FromBytes(new Byte[6]));
        _ = Assert.Throws<ArgumentException>(() => Snowflake.FromBytes(new Byte[8]));
    }

    [Fact]
    public void HexString_MatchesSerializedBytes_Length14()
    {
        var id = Snowflake.NewId(0x00112233, 0x4455, (SnowflakeType)0x66);
        Span<Byte> buf = stackalloc Byte[7];
        Assert.True(id.TryWriteBytes(buf, out _));
        var hex = id.ToString(); // Convert.ToHexString(7 bytes) => 14 hex chars
        Assert.Equal(Convert.ToHexString(buf.ToArray()), hex);
        Assert.Equal(14, hex.Length);
    }

    [Fact]
    public void Equality_And_HashCode_UseAllComponents()
    {
        var id1 = Snowflake.NewId(0xAAAAAAAA, 0xBBBB, (SnowflakeType)0xCC);
        var id2 = Snowflake.NewId(0xAAAAAAAA, 0xBBBB, (SnowflakeType)0xCC);
        var id3 = Snowflake.NewId(0xAAAAAAAA, 0xBBBB, (SnowflakeType)0xCD);

        // Same components => equal
        Assert.True(id1.Equals(id2));
        Assert.True(id1 == id2);
        Assert.False(id1 != id2);
        Assert.Equal(id1.GetHashCode(), id2.GetHashCode());

        // Different type => not equal
        Assert.False(id1.Equals(id3));
        Assert.True(id1 != id3);

        // Dictionary key usage stable
        var dict = new Dictionary<Snowflake, Int32> { [id1] = 42 };
        Assert.True(dict.TryGetValue(id2, out var v));
        Assert.Equal(42, v);
    }

    [Fact]
    public void Random_NewId_ByTypeAndMachine_RoundTripsFromBytes()
    {
        // NewId(type, machineId) uses SecureRandom for Value; we just check round-trip
        var id = Snowflake.NewId((SnowflakeType)0x11, machineId: 0x2222);
        Span<Byte> buf = stackalloc Byte[7];
        Assert.True(id.TryWriteBytes(buf, out _));
        var back = Snowflake.FromBytes(buf);
        Assert.Equal(id, back);
    }

    [Fact]
    public void Unsafe_SizeOf_Is7_ForExplicitLayout()
    {
        Int32 size = Unsafe.SizeOf<Snowflake>();
        // StructLayout(Size=7) => managed size must be 7
        Assert.Equal(7, size);
    }
}
