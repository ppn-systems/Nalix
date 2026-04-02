#if DEBUG
using System;
using System.Threading.Tasks;
using Nalix.Environment.Random;
using Xunit;

namespace Nalix.Environment.Tests.Random;

public class OsRandomTests
{
    [Fact]
    public void Fill_FillsBufferWithNonZeroData()
    {
        Span<byte> buffer = stackalloc byte[100];
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

    [Fact]
    public void ReseedTimer_CanStartAndStop()
    {
        // This test ensures no exceptions are thrown during timer lifecycle
        OsRandom.Reseed(TimeSpan.FromMinutes(10));
        OsRandom.StopReseed();
        OsRandom.StopReseed(); // Should be safe to call twice
    }

    [Fact]
    public async Task Fill_Multithreaded_DoesNotCrash()
    {
        const int ThreadCount = 10;
        const int Iterations = 1000;
        
        Task[] tasks = new Task[ThreadCount];
        for (int i = 0; i < ThreadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                Span<byte> buffer = stackalloc byte[64];
                for (int j = 0; j < Iterations; j++)
                {
                    OsRandom.Fill(buffer);
                }
            });
        }

        await Task.WhenAll(tasks);
    }
    
    [Fact]
    public void Fill_Unaligned_DoesNotCrash()
    {
        byte[] buffer = new byte[100];
        // Test various unaligned offsets
        for (int i = 1; i < 8; i++)
        {
            OsRandom.Fill(buffer.AsSpan(i));
        }
    }
}
#endif
