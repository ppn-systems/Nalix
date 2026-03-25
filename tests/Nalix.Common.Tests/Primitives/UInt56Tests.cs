using System;
using System.Globalization;
using Nalix.Common.Primitives;
using Xunit;

namespace Nalix.Common.Tests.Primitives;

public sealed class UInt56Tests
{
    [Fact]
    public void Construction_FromUlong_RoundtripsCorrectly()
    {
        ulong original = 0x00123456789ABCDEUL;
        UInt56 value = (UInt56)original;
        
        Assert.Equal(original, (ulong)value);
    }

    [Fact]
    public void Construction_OverMaxValue_ThrowsOverflowException()
    {
        ulong tooLarge = UInt56.MaxValue + 1;
        Assert.Throws<OverflowException>(() => (UInt56)tooLarge);
    }

    [Fact]
    public void Addition_WithinRange_Succeeds()
    {
        UInt56 a = (UInt56)100;
        UInt56 b = (UInt56)200;
        UInt56 result = a + b;
        
        Assert.Equal(300UL, (ulong)result);
    }

    [Fact]
    public void Addition_Overflow_ThrowsOverflowException()
    {
        UInt56 a = (UInt56)UInt56.MaxValue;
        UInt56 b = (UInt56)1;
        
        Assert.Throws<OverflowException>(() => a + b);
    }

    [Fact]
    public void BitwiseAnd_ReturnsExpectedValue()
    {
        UInt56 a = (UInt56)0x00FF00FF00FF00FFUL;
        UInt56 b = (UInt56)0x0000FFFF0000FFFFUL;
        UInt56 result = a & b;
        
        Assert.Equal(0x000000FF000000FFUL, (ulong)result);
    }

    [Fact]
    public void BitwiseOr_ReturnsExpectedValue()
    {
        UInt56 a = (UInt56)0x00FF00FF00FF00FFUL;
        UInt56 b = (UInt56)0x0000FFFF0000FFFFUL;
        UInt56 result = a | b;
        
        Assert.Equal(0x00FFFFFF00FFFFFFUL, (ulong)result);
    }

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        UInt56 value = (UInt56)12345;
        Assert.Equal("12345", value.ToString());
    }

    [Fact]
    public void Parse_ValidString_ReturnsExpectedValue()
    {
        UInt56 value = UInt56.Parse("12345", CultureInfo.InvariantCulture);
        Assert.Equal(12345UL, (ulong)value);
    }

    [Fact]
    public void Equality_SameValue_ReturnsTrue()
    {
        UInt56 a = (UInt56)123;
        UInt56 b = (UInt56)123;
        
        Assert.True(a == b);
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Inequality_DifferentValue_ReturnsTrue()
    {
        UInt56 a = (UInt56)123;
        UInt56 b = (UInt56)456;
        
        Assert.True(a != b);
    }

    [Fact]
    public void ShiftLeft_ReturnsExpectedValue()
    {
        UInt56 a = (UInt56)0x00000000000000FFUL;
        UInt56 result = a << 8;
        
        Assert.Equal(0x000000000000FF00UL, (ulong)result);
    }

    [Fact]
    public void ShiftRight_ReturnsExpectedValue()
    {
        UInt56 a = (UInt56)0x000000000000FF00UL;
        UInt56 result = a >> 8;
        
    }
    
    [Fact]
    public void Bitwise_Operations_Exhaustive()
    {
        UInt56 a = new(0x00FF00FF00FF00UL);
        UInt56 b = new(0x0000FF00FF00FFUL);
        
        Assert.Equal(0x00FF00FF00FF00UL & 0x0000FF00FF00FFUL, (ulong)(a & b));
        Assert.Equal(0x00FF00FF00FF00UL | 0x0000FF00FF00FFUL, (ulong)(a | b));
        Assert.Equal(0x00FF00FF00FF00UL ^ 0x0000FF00FF00FFUL, (ulong)(a ^ b));
        Assert.Equal(~0x00FF00FF00FF00UL & UInt56.MaxValue, (ulong)~a);
    }

    [Fact]
    public void Shift_Operations_Exhaustive()
    {
        UInt56 val = new(0x123456789ABCDEUL);
        
        Assert.Equal((0x123456789ABCDEUL << 4) & UInt56.MaxValue, (ulong)(val << 4));
        Assert.Equal(0x123456789ABCDEUL >> 4, (ulong)(val >> 4));
    }

    [Fact]
    public void Serialization_Endianness_Exhaustive()
    {
        UInt56 val = new(0x11223344556677UL);
        Span<byte> le = stackalloc byte[7];
        Span<byte> be = stackalloc byte[7];
        
        val.WriteBytesLittleEndian(le);
        val.WriteBytesBigEndian(be);
        
        Assert.Equal(new byte[] { 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11 }, le.ToArray());
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77 }, be.ToArray());
        
        Assert.Equal(val, UInt56.ReadBytesLittleEndian(le));
        Assert.Equal(val, UInt56.ReadBytesBigEndian(be));
    }
}
