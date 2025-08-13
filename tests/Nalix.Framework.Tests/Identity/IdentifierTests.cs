using Nalix.Common.Security.Identity;
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
        var id = Identifier.CreateEmpty(); // all zero
        Assert.True(id.IsEmpty()); // not empty => false
        Assert.False(id.IsValid());

        Span<Byte> buf = stackalloc Byte[7];
        Assert.True(id.TrySerialize(buf, out var written));
        Assert.Equal(7, written);
        Assert.Equal(new Byte[7], buf.ToArray());
    }

    [Fact]
    public void NewId_WithFixedComponents_SerializesLittleEndian_And_RoundTrips()
    {
        const UInt32 value = 0x11223344;
        const UInt16 machine = 0x5566;
        var type = (IdentifierType)0x77;

        var id = Identifier.NewId(value, machine, type);
        Assert.True(id.IsValid());
        Assert.Equal(value, id.Value);
        Assert.Equal(machine, id.MachineId);
        Assert.Equal(type, id.Type);

        Span<Byte> buf = stackalloc Byte[7];
        Assert.True(id.TrySerialize(buf, out var written));
        Assert.Equal(7, written);

        // little-endian layout: [0..3]=Value, [4..5]=Machine, [6]=Type  (core impl)
        Assert.Equal(0x44, buf[0]);
        Assert.Equal(0x33, buf[1]);
        Assert.Equal(0x22, buf[2]);
        Assert.Equal(0x11, buf[3]);
        Assert.Equal(0x66, buf[4]);
        Assert.Equal(0x55, buf[5]);
        Assert.Equal(0x77, buf[6]);

        var back = Identifier.Deserialize(buf);
        Assert.Equal(id, back);
        Assert.True(id == back);
    }

    [Fact]
    public void TrySerialize_ToByteSpan_TooSmall_ReturnsFalse()
    {
        var id = Identifier.NewId(0xAABBCCDD, 0xEEFF, (IdentifierType)0x12);
        Span<Byte> small = stackalloc Byte[6];
        Assert.False(id.TrySerialize(small, out var written));
        Assert.Equal(0, written);
    }

    [Fact]
    public void Serialize_ReturnsNew7ByteArray()
    {
        var id = Identifier.NewId(0x01020304, 0x0506, (IdentifierType)0x07);
        var arr = id.Serialize();
        Assert.Equal(7, arr.Length);
        Assert.Equal(new Byte[] { 0x04, 0x03, 0x02, 0x01, 0x06, 0x05, 0x07 }, arr);
    }

    [Fact]
    public void Deserialize_FromByteArray_InvalidLength_Throws()
    {
        _ = Assert.Throws<ArgumentException>(() => Identifier.Deserialize([]));
        _ = Assert.Throws<ArgumentException>(() => Identifier.Deserialize(new Byte[6]));
        _ = Assert.Throws<ArgumentException>(() => Identifier.Deserialize(new Byte[8]));
    }

    [Fact]
    public void Base36_ToString_And_Deserialize_RoundTrip()
    {
        var id = Identifier.NewId(0xDEADBEEF, 0xBEEF, (IdentifierType)0xAB);
        String s1 = id.ToBase36String(); // Base36 [0-9A-Z], compact (impl)
        var back1 = Identifier.Deserialize(s1);
        Assert.Equal(id, back1);

        // TrySerialize to char span
        Span<Char> dst = stackalloc Char[13];
        Assert.True(id.TrySerialize(dst, out Byte len));
        Assert.InRange(len, 1, 13);
        Assert.Equal(s1, new String(dst[..len]));
    }

    [Fact]
    public void TryDeserialize_AllowsLowercase_And_RejectsNonBase36()
    {
        var id = Identifier.NewId(0x00ABCDEF, 0x1234, (IdentifierType)0x5A);
        String up = id.ToBase36String();
        String low = up.ToLowerInvariant();

        Assert.True(Identifier.TryDeserialize(low.AsSpan(), out var lowerParsed));
        Assert.Equal(id, lowerParsed);

        Assert.False(Identifier.TryDeserialize("*$%".AsSpan(), out _));
    }

    [Fact]
    public void TryDeserialize_TooLongOver13_ReturnsFalse() =>
        // 14 chars => invalid per implementation max 13 for 56-bit Base36
        Assert.False(Identifier.TryDeserialize(new String('Z', 14).AsSpan(), out _));

    [Fact]
    public void HexString_MatchesSerializedBytes_Length14()
    {
        var id = Identifier.NewId(0x00112233, 0x4455, (IdentifierType)0x66);
        Span<Byte> buf = stackalloc Byte[7];
        Assert.True(id.TrySerialize(buf, out _));
        var hex = id.ToHexString(); // Convert.ToHexString(7 bytes) => 14 hex chars
        Assert.Equal(Convert.ToHexString(buf.ToArray()), hex);
        Assert.Equal(14, hex.Length);
    }

    [Fact]
    public void Equality_And_HashCode_UseAllComponents()
    {
        var id1 = Identifier.NewId(0xAAAAAAAA, 0xBBBB, (IdentifierType)0xCC);
        var id2 = Identifier.NewId(0xAAAAAAAA, 0xBBBB, (IdentifierType)0xCC);
        var id3 = Identifier.NewId(0xAAAAAAAA, 0xBBBB, (IdentifierType)0xCD);

        // Same components => equal
        Assert.True(id1.Equals(id2));
        Assert.True(id1 == id2);
        Assert.False(id1 != id2);
        Assert.Equal(id1.GetHashCode(), id2.GetHashCode());

        // Different type => not equal
        Assert.False(id1.Equals(id3));
        Assert.True(id1 != id3);

        // Dictionary key usage stable
        var dict = new Dictionary<Identifier, Int32> { [id1] = 42 };
        Assert.True(dict.TryGetValue(id2, out var v));
        Assert.Equal(42, v);
    }

    [Fact]
    public void MaxSevenBytesValue_RoundTrip_Base36()
    {
        // Compose the absolute max allowed 56-bit value: FF_FFFF_FFFF_FFFF (56 bits)
        var id = Identifier.NewId(0xFFFFFFFF, 0xFFFF, (IdentifierType)0xFF);
        String s = id.ToBase36String();
        Assert.True(Identifier.TryDeserialize(s.AsSpan(), out var back));
        Assert.Equal(id, back);
    }

    [Fact]
    public void Random_NewId_ByTypeAndMachine_RoundTripsFromBytes()
    {
        // NewId(type, machineId) uses SecureRandom for Value; we just check round-trip
        var id = Identifier.NewId((IdentifierType)0x11, machineId: 0x2222);
        Span<Byte> buf = stackalloc Byte[7];
        Assert.True(id.TrySerialize(buf, out _));
        var back = Identifier.Deserialize(buf);
        Assert.Equal(id, back);
        Assert.True(id.IsValid());
    }

    [Fact]
    public void Unsafe_SizeOf_Is7_ForExplicitLayout()
    {
        Int32 size = Unsafe.SizeOf<Identifier>();
        // StructLayout(Size=7) => managed size must be 7
        Assert.Equal(7, size);
    }
}
