using System;
using System.Collections.Generic;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;
using Nalix.Codec.Extensions;

Console.WriteLine("=== Test FieldCache field discovery ===\n");

// Test: dùng facade properties, KHÔNG gán Header
ComplexCollectionPacket cp = new();
cp.IntList = [1, 2, 3];
cp.SequenceId = 1234;

Console.WriteLine($"Magic: 0x{cp.Header.MagicNumber:X8}");
Console.WriteLine($"SeqId: {cp.Header.SequenceId}");

byte[] bytes = cp.Serialize();
Console.WriteLine($"Serialized ({bytes.Length}): {Convert.ToHexString(bytes)}");

PacketHeader h = bytes.AsSpan().ReadHeaderLE();
Console.WriteLine($"ReadHeader.Magic: 0x{h.MagicNumber:X8}");
Console.WriteLine($"ReadHeader.SeqId: {h.SequenceId}");

Console.WriteLine(h.MagicNumber == cp.Header.MagicNumber ? "\nOK!" : $"\nFAIL! Expected 0x{cp.Header.MagicNumber:X8}");

[SerializePackable(SerializeLayout.Sequential)]
public sealed class ComplexCollectionPacket : PacketBase<ComplexCollectionPacket>
{
    [SerializeOrder(0)] public List<int>? IntList { get; set; }
    [SerializeOrder(1)] public Dictionary<string, long>? StringLongDict { get; set; }
}
