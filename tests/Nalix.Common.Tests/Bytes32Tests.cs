using System;
using Nalix.Common.Primitives;
using Xunit;

namespace Nalix.Common.Tests.Primitives;

public sealed class Bytes32Tests
{
    [Fact]
    public void Constructor_WithValidSpan_CopiesData()
    {
        byte[] data = new byte[32];
        for (int i = 0; i < 32; i++) data[i] = (byte)i;

        Bytes32 b = new(data);
        
        Assert.Equal(data, b.ToByteArray());
    }

    [Fact]
    public void Constructor_WithShortSpan_ThrowsArgumentException()
    {
        byte[] data = new byte[31];
        Assert.Throws<ArgumentException>(() => new Bytes32(data));
    }

    [Fact]
    public void Equals_BitwiseIdentical_ReturnsTrue()
    {
        byte[] data = new byte[32];
        new Random(42).NextBytes(data);

        Bytes32 b1 = new(data);
        Bytes32 b2 = new(data);

        Assert.True(b1.Equals(b2));
        Assert.True(b1 == b2);
    }

    [Fact]
    public void Equals_DifferentData_ReturnsFalse()
    {
        byte[] data1 = new byte[32];
        byte[] data2 = new byte[32];
        data1[31] = 1;
        data2[31] = 2;

        Bytes32 b1 = new(data1);
        Bytes32 b2 = new(data2);

        Assert.False(b1.Equals(b2));
        Assert.True(b1 != b2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(31)]
    public void Equals_BitFlipInEachByte_ReturnsFalse(int byteIndex)
    {
        byte[] data1 = new byte[32];
        new Random(byteIndex).NextBytes(data1);
        byte[] data2 = (byte[])data1.Clone();
        
        // Flip a bit that is NOT bit 7 (to ensure MoveMask alone would fail)
        data2[byteIndex] ^= 0x01; 

        Bytes32 b1 = new(data1);
        Bytes32 b2 = new(data2);

        Assert.False(b1.Equals(b2), $"Failed to detect difference at byte {byteIndex}");
    }

    [Theory]
    [InlineData(0, 0x80)] // Bit 7 (MoveMask would see this)
    [InlineData(15, 0x40)]
    [InlineData(31, 0x01)] // Bit 0 (MoveMask would MISS this if it was the only check)
    public void Equals_SpecificBitFlips_ReturnsFalse(int byteIndex, byte mask)
    {
        byte[] data1 = new byte[32];
        byte[] data2 = new byte[32];
        data2[byteIndex] = mask;

        Bytes32 b1 = new(data1);
        Bytes32 b2 = new(data2);

        Assert.False(b1.Equals(b2), $"Failed to detect bit mask {mask:X2} at byte {byteIndex}");
    }

    [Fact]
    public void IsZero_ExhaustiveByteCheck()
    {
        for (int i = 0; i < 32; i++)
        {
            byte[] data = new byte[32];
            data[i] = 0x01; // Smallest bit
            Bytes32 b = new(data);
            Assert.False(b.IsZero, $"IsZero returned true for non-zero byte at {i}");
            
            data[i] = 0x80; // Largest bit
            b = new(data);
            Assert.False(b.IsZero, $"IsZero returned true for non-zero byte at {i}");
        }
    }

    [Fact]
    public void IsZero_WhenAllZero_ReturnsTrue()
    {
        Bytes32 b = Bytes32.Zero;
        Assert.True(b.IsZero);
    }

    [Fact]
    public void IsZero_WhenOneBitSet_ReturnsFalse()
    {
        byte[] data = new byte[32];
        data[15] = 1;
        Bytes32 b = new(data);
        Assert.False(b.IsZero);
    }

    [Fact]
    public void Parse_ValidHexString_ReturnsCorrectBytes()
    {
        string hex = "0102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F20";
        Bytes32 b = Bytes32.Parse(hex);
        
        Assert.Equal(hex, b.ToString());
    }

    [Fact]
    public void GetHashCode_SameData_SameHashCode()
    {
        byte[] data = new byte[32];
        new Random(42).NextBytes(data);

        Bytes32 b1 = new(data);
        Bytes32 b2 = new(data);

        Assert.Equal(b1.GetHashCode(), b2.GetHashCode());
    }
}
