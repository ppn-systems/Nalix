
using Nalix.Common.Abstractions;
using Nalix.Framework.Options;

namespace Nalix.Framework.Tests.Memory;

internal static class MemoryTestSupport
{
    public static BufferConfig CreateBufferConfig(bool enableMemoryTrimming)
    {
        return new BufferConfig
        {
            EnableMemoryTrimming = enableMemoryTrimming,
            TotalBuffers = 32,
            AutoTuneOperationThreshold = 32,
            BufferAllocations = "256,0.50;512,0.25;1024,0.25",
            TrimIntervalMinutes = 1,
            DeepTrimIntervalMinutes = 1,
            ExpandThresholdPercent = 0.20,
            ShrinkThresholdPercent = 0.60,
            AdaptiveGrowthFactor = 2.0,
            MinimumIncrease = 2,
            MaxBufferIncreaseLimit = 16,
            MaxMemoryPercentage = 0.25,
            MaxMemoryBytes = 0,
            FallbackToArrayPool = true
        };
    }
}

internal sealed class TestPoolable : IPoolable
{
    public int Value { get; set; }

    public void ResetForPool() => this.Value = 0;
}

internal sealed class HealthCheckPoolable : IPoolable
{
    public void ResetForPool()
    {
    }
}
