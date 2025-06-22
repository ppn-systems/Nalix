using Nalix.Framework.Random.Algorithms;
using System;
using System.Linq;
using Xunit;

namespace Nalix.Framework.Tests.Randomization;

public class SeededRandomTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(256)]
    public void Get_Int_Max_WithinRange(Int32 max)
    {
        var r = new SeededRandom(123u);
        for (Int32 i = 0; i < 2000; i++)
        {
            Int32 v = r.Get(max);
            if (max <= 0)
            {
                Assert.Equal(0, v);
            }
            else
            {
                Assert.InRange(v, 0, max - 1);
            }
        }
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    [InlineData(8u)]
    [InlineData(1024u)]
    public void Get_UInt_Max_WithinRange(UInt32 max)
    {
        var r = new SeededRandom(1u);
        for (Int32 i = 0; i < 2000; i++)
        {
            UInt32 v = r.Get(max);
            if (max == 0)
            {
                Assert.Equal<UInt32>(0, v);
            }
            else
            {
                Assert.InRange(v, 0u, max - 1);
            }
        }
    }

    [Theory]
    [InlineData(0ul)]
    [InlineData(1ul)]
    [InlineData(2ul)]
    [InlineData(3ul)]
    [InlineData(16ul)]
    [InlineData(1ul << 40)]
    public void Get_ULong_Max_WithinRange(UInt64 max)
    {
        var r = new SeededRandom(2u);
        for (Int32 i = 0; i < 2000; i++)
        {
            UInt64 v = r.Get(max);
            if (max == 0ul)
            {
                Assert.Equal<UInt64>(0, v);
            }
            else
            {
                Assert.InRange(v, 0ul, max - 1);
            }
        }
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(2L)]
    [InlineData(3L)]
    [InlineData(16L)]
    public void Get_Long_Max_WithinRange(Int64 max)
    {
        var r = new SeededRandom(3u);
        for (Int32 i = 0; i < 2000; i++)
        {
            Int64 v = r.Get(max);
            if (max <= 0)
            {
                Assert.Equal(0, v);
            }
            else
            {
                Assert.InRange(v, 0, max - 1);
            }
        }
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(10, 5)]
    public void Get_Int_MinMax_Edges(Int32 min, Int32 max)
    {
        var r = new SeededRandom(9u);
        Int32 v = r.Get(min, max);
        if (min >= max)
        {
            Assert.Equal(min, v);
        }
        else
        {
            Assert.InRange(v, min, max - 1);
        }
    }

    [Fact]
    public void Get_UInt_MinMax_Range()
    {
        var r = new SeededRandom(11u);
        UInt32 min = 100, max = 200;
        for (Int32 i = 0; i < 2000; i++)
        {
            UInt32 v = r.Get(min, max);
            Assert.InRange(v, min, max - 1);
        }
    }

    [Fact]
    public void Get_ULong_MinMax_Range()
    {
        var r = new SeededRandom(12u);
        UInt64 min = 123456789012, max = min + 1_000_000;
        for (Int32 i = 0; i < 2000; i++)
        {
            UInt64 v = r.Get(min, max);
            Assert.InRange(v, min, max - 1);
        }
    }

    [Fact]
    public void Get_Long_MinMax_Range()
    {
        var r = new SeededRandom(13u);
        Int64 min = -1000, max = 1000;
        for (Int32 i = 0; i < 2000; i++)
        {
            Int64 v = r.Get(min, max);
            Assert.InRange(v, min, max - 1);
        }
    }

    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(10f, 10f)]
    public void Get_Float_Max_Range(Single max, Single clampTo)
    {
        var r = new SeededRandom(14u);
        for (Int32 i = 0; i < 1000; i++)
        {
            Single v = r.Get(max);
            Assert.True(v >= 0f && v <= clampTo + Single.Epsilon);
        }
    }

    [Theory]
    [InlineData(-1.0, 0.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(10.0, 10.0)]
    public void Get_Double_Max_Range(Double max, Double clampTo)
    {
        var r = new SeededRandom(15u);
        for (Int32 i = 0; i < 1000; i++)
        {
            Double v = r.Get(max);
            Assert.True(v >= 0.0 && v <= clampTo + Double.Epsilon);
        }
    }

    [Fact]
    public void Get_Float_MinMax_Range()
    {
        var r = new SeededRandom(16u);
        Single min = -2.5f, max = 7.25f;
        for (Int32 i = 0; i < 1000; i++)
        {
            Single v = r.Get(min, max);
            Assert.True(v >= min - 1e-6 && v <= max + 1e-6);
        }
    }

    [Fact]
    public void Get_Double_MinMax_Range()
    {
        var r = new SeededRandom(17u);
        Double min = -2.5, max = 7.25;
        for (Int32 i = 0; i < 1000; i++)
        {
            Double v = r.Get(min, max);
            Assert.True(v >= min - 1e-12 && v <= max + 1e-12);
        }
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(1.1)]
    public void GetBool_Boundaries(Double p)
    {
        var r = new SeededRandom(18u);
        Boolean v = r.GetBool(p);
        if (p <= 0)
        {
            Assert.False(v);
        }

        if (p >= 1)
        {
            Assert.True(v);
        }
    }

    [Fact]
    public void GetFloat01_And_GetDouble01_Range()
    {
        var r = new SeededRandom(19u);
        for (Int32 i = 0; i < 5000; i++)
        {
            Single f = r.GetFloat();
            Double d = r.GetDouble();
            Assert.True(f is >= 0f and < 1f);
            Assert.True(d is >= 0.0 and < 1.0);
        }
    }

    [Fact]
    public void NextBytes_Fills()
    {
        var r = new SeededRandom(20u);
        Span<Byte> buf = stackalloc Byte[64];
        r.NextBytes(buf);
        Assert.True(buf.ToArray().Any());
        Assert.NotEqual(new Byte[64], buf.ToArray());
    }
}
