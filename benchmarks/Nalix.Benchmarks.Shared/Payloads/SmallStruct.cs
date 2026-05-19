using Nalix.Abstractions.Serialization;

namespace Nalix.Benchmarks.Shared.Payloads;

[GenerateFormatter]
public struct SmallStruct
{
    public long Field1;
    public long Field2;
    public long Field3;
    public long Field4;
}
