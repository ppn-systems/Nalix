using Nalix.Framework.Random.Generators;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Nalix.Framework.Tests.Randomization;

public class Rand32Tests
{
    [Fact]
    public void Seed_Roundtrip_Works()
    {
        var r = new Rand32(123);
        Assert.Equal(123, r.GetSeed());

        r.Seed(456);
        Assert.Equal(456, r.GetSeed());
    }

    [Fact]
    public void Next_NoArgs_WithinRange()
    {
        var r = new Rand32(1);
        for (Int32 i = 0; i < 1000; i++)
        {
            Int32 v = r.Next();
            Assert.InRange(v, 0, Rand32.RandMax);
        }
    }

    [Fact]
    public void Next_Max_Invalid_Throws()
    {
        var r = new Rand32(1);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => r.Next(0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => r.Next(-5));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(31)]
    [InlineData(256)]
    public void Next_Max_WithinRange(Int32 max)
    {
        var r = new Rand32(42);
        for (Int32 i = 0; i < 2000; i++)
        {
            Int32 v = r.Next(max);
            Assert.InRange(v, 0, max - 1);
        }
    }

    [Fact]
    public void Next_MinMax_Equal_ReturnsMin()
    {
        var r = new Rand32(1);
        Assert.Equal(5, r.Next(5, 5));
    }

    [Fact]
    public void Next_MinMax_Invalid_Throws()
    {
        var r = new Rand32(1);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => r.Next(10, 5));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(10f)]
    public void NextFloat_Max_Range(Single max)
    {
        var r = new Rand32(7);
        for (Int32 i = 0; i < 2000; i++)
        {
            Single v = r.NextFloat(max);
            Assert.True(v >= 0f && v <= max, $"v={v}, max={max}");
        }
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(10.0)]
    public void NextDouble_Max_Range(Double max)
    {
        var r = new Rand32(9);
        for (Int32 i = 0; i < 2000; i++)
        {
            Double v = r.NextDouble(max);
            Assert.True(v >= 0.0 && v <= max + Double.Epsilon);
        }
    }

    [Fact]
    public void NextFloat_Naked_Range01()
    {
        var r = new Rand32(3);
        for (Int32 i = 0; i < 5000; i++)
        {
            Single v = r.NextFloat();
            Assert.True(v is >= 0f and <= 1f);
        }
    }

    [Fact]
    public void NextDouble_Naked_Range01()
    {
        var r = new Rand32(3);
        for (Int32 i = 0; i < 5000; i++)
        {
            Double v = r.NextDouble();
            Assert.True(v is >= 0.0 and <= 1.0);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(37)]
    public void NextPct_Boundaries_DoNotThrow(Int32 pct)
    {
        var r = new Rand32(1);
        Boolean _ = r.NextPct(pct);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(1.1)]
    public void NextProbability_Boundaries(Double p)
    {
        var r = new Rand32(1);
        Boolean result = r.NextProbability(p);
        // Với p<=0 luôn false, p>=1 luôn true (theo code)
        if (p <= 0)
        {
            Assert.False(result);
        }

        if (p >= 1)
        {
            Assert.True(result);
        }
    }

    [Fact]
    public void ShuffleList_Permutation()
    {
        var r = new Rand32(123);
        var list = Enumerable.Range(0, 100).ToList();
        var before = list.ToArray();

        r.ShuffleList(list);

        Assert.Equal(100, list.Count);
        Assert.True(before.OrderBy(x => x).SequenceEqual(list.OrderBy(x => x))); // cùng tập phần tử
        Assert.False(before.SequenceEqual(list)); // xác suất rất cao khác thứ tự
    }
    private static readonly Int32[] collection = [10, 20, 30];

    [Fact]
    public void Choose_List_And_Span()
    {
        var r = new Rand32(1);
        var list = new List<Int32> { 1, 2, 3 };
        Int32 x = r.Choose(list);
        Assert.Contains(x, list);

        ReadOnlySpan<Int32> span = [10, 20, 30];
        Int32 y = r.Choose(span);
        Assert.Contains(y, collection);
    }

    [Fact]
    public void Choose_Empty_Throws()
    {
        var r = new Rand32(1);
        _ = Assert.Throws<ArgumentException>(() => r.Choose(new List<Int32>()));
        _ = Assert.Throws<ArgumentException>(() => r.Choose(ReadOnlySpan<Int32>.Empty));
    }

    [Fact]
    public void NextBytes_FillsData()
    {
        var r = new Rand32(99);
        Byte[] buf = new Byte[64];
        r.NextBytes(buf);
        Assert.Contains(buf, b => true);
        Assert.NotEqual(new Byte[64], buf);
    }

    [Fact]
    public void ToString_ContainsSeed()
    {
        var r = new Rand32(777);
        Assert.Contains("Seed=777", r.ToString());
    }
}
