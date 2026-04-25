#if DEBUG
using System;
using Nalix.Framework.Random;
using Xunit;

namespace Nalix.Framework.Tests.Random;

public sealed class OsRandomTests
{
    [Fact]
    public void Fill_ValidBuffer_FillsWithData()
    {
        byte[] buffer = new byte[32];
        OsRandom.Fill(buffer);

        // Very unlikely to be all zeros
        bool allZero = true;
        foreach (byte b in buffer)
        {
            if (b != 0)
            {
                allZero = false;
                break;
            }
        }
        Assert.False(allZero);
    }

    [Fact]
    public void Fill_EmptyBuffer_DoesNotThrow()
    {
        OsRandom.Fill(Span<byte>.Empty);
    }

    [Fact]
    public void Fill_MultipleCalls_ReturnsDifferentData()
    {
        byte[] b1 = new byte[16];
        byte[] b2 = new byte[16];

        OsRandom.Fill(b1);
        OsRandom.Fill(b2);

        Assert.NotEqual(b1, b2);
    }

    [Fact]
    public void Fill_SmallBuffer_FillsCorrectly()
    {
        byte[] buffer = new byte[3];
        OsRandom.Fill(buffer);
        
        bool allZero = true;
        foreach (byte b in buffer)
        {
            if (b != 0)
            {
                allZero = false;
                break;
            }
        }
        Assert.False(allZero);
    }
}
#endif
