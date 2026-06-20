
using Nalix.Abstractions;
using Nalix.Framework.Options;

namespace Nalix.Framework.Tests.Memory;

internal static class MemoryTestSupport
{
    public static BufferOptions CreateBufferOptions(bool enableMemoryTrimming)
    {
        return new BufferOptions
        {
            EnableBufferLeakDetection = false,
            EnableBufferLeakStackTrace = false,
            SuspiciousThresholdSeconds = 30
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

internal sealed class GenericPoolable<T> : IPoolable
{
    public void ResetForPool()
    {
    }
}














