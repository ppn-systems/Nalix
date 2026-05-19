using System.Collections.Generic;
using Nalix.Benchmarks.Shared.Payloads;

namespace Nalix.Benchmarks.Shared.Helpers;

public static class PayloadGenerator
{
    public static BenchPayload Generate(int itemCount)
    {
        var items = new List<int>(itemCount);
        for (int i = 0; i < itemCount; i++)
        {
            items.Add(i * 17);
        }

        return new BenchPayload
        {
            Id = 42,
            Name = "Benchmarking Nalix high-performance public APIs with MessagePack, MemoryPack, and System.Text.Json comparison.",
            Items = items
        };
    }
}
