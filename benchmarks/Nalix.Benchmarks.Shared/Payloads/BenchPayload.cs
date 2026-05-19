using System.Collections.Generic;
using MessagePack;
using MemoryPack;
using Nalix.Abstractions.Serialization;

namespace Nalix.Benchmarks.Shared.Payloads;

[GenerateFormatter]
[MessagePackObject]
[MemoryPackable]
public sealed partial class BenchPayload
{
    [Key(0)]
    public int Id { get; set; }

    [Key(1)]
    public string Name { get; set; } = string.Empty;

    [Key(2)]
    public List<int> Items { get; set; } = [];

    public static BenchPayload Create() => new();
}
