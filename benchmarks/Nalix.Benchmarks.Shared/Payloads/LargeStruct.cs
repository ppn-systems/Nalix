using System.Runtime.InteropServices;
using Nalix.Abstractions.Serialization;

namespace Nalix.Benchmarks.Shared.Payloads;

[GenerateFormatter]
[StructLayout(LayoutKind.Sequential, Size = 512)]
public struct LargeStruct
{
    // StructLayout guarantees the size is exactly 512 bytes.
    // We can define a few fields just in case, but StructLayout(Size = 512) is perfect.
    public long FirstField;
    public long LastField;
}
