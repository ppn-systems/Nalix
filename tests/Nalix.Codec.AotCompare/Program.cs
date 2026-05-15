using System.Security.Cryptography;
using System.Text;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;
using Nalix.Environment.Extensions;
using Nalix.Codec.Serialization;

namespace Nalix.Codec.AotCompare;

internal sealed class Program
{
    public static int Main()
    {
#if NALIX_AOT
        AotCompareFormatterBootstrap.Register();
#endif

        List<ScenarioResult> results =
        [
            Run("primitive-int32-roundtrip", "unmanaged", PrimitiveRoundTrip),
            Run("primitive-bool-roundtrip", "unmanaged", PrimitiveBooleanRoundTrip),
            Run("primitive-float-specials", "unmanaged", PrimitiveFloatSpecials),
            Run("enum-byte-roundtrip", "unmanaged", EnumByteRoundTrip),
            Run("span-serialize-int64", "unmanaged", SpanSerializeInt64),
            Run("empty-byte-array-roundtrip", "array", EmptyByteArrayRoundTrip),
            Run("null-int-array-roundtrip", "array", NullIntArrayRoundTrip),
            Run("large-unmanaged-array-roundtrip", "array", UnmanagedArrayRoundTrip),
            Run("malformed-primitive-buffer", "negative", MalformedPrimitiveBuffer),
            Run("tuple-roundtrip", "formatter", TupleRoundTrip),
            Run("complex-object-roundtrip", "formatter", ComplexObjectRoundTrip),
            Run("packet-complex-collections", "packet", PacketComplexCollections),
            Run("packet-nested-graph", "packet", PacketNestedGraph),
            Run("packet-large-payload", "packet", PacketLargePayload),
            Run("packet-null-stress", "packet", PacketNullStress),
            Run("packet-enum-collections", "packet", PacketEnumCollections),
            Run("packet-generated-registry", "packet", PacketGeneratedRegistry),
            Run("packet-malformed-buffer", "negative", PacketMalformedBuffer)
        ];

        Console.WriteLine(ToJson(results));
        return results.Exists(static r => !r.Passed) ? 1 : 0;
    }

    private static ScenarioResult Run(String name, String category, Func<ScenarioResult> scenario)
    {
        try
        {
            ScenarioResult result = scenario();
            return result with { Name = name, Category = category, Passed = true };
        }
        catch (Exception ex)
        {
            return new ScenarioResult(name, category, false, null, null, null, ex.ToString());
        }
    }

    private static ScenarioResult PrimitiveRoundTrip()
    {
        Int32 value = 123456789;
        Byte[] bytes = LiteSerializer.Serialize(value);
        Int32 clone = LiteSerializer.Deserialize<Int32>(bytes, out Int32 read);
        Require(read == bytes.Length, "int bytes-read mismatch");
        Require(value == clone, "int mismatch");
        return Result(bytes, value.ToString());
    }

    private static ScenarioResult PrimitiveBooleanRoundTrip()
    {
        Boolean value = true;
        Byte[] bytes = LiteSerializer.Serialize(value);
        Boolean clone = LiteSerializer.Deserialize<Boolean>(bytes, out Int32 read);
        Require(read == bytes.Length, "bool bytes-read mismatch");
        Require(value == clone, "bool mismatch");
        return Result(bytes, clone.ToString());
    }

    private static ScenarioResult PrimitiveFloatSpecials()
    {
        Single[] values = [Single.NaN, Single.PositiveInfinity, Single.NegativeInfinity, Single.Epsilon, Single.MaxValue, Single.MinValue];
        Byte[] bytes = LiteSerializer.Serialize(values);
        Single[] clone = LiteSerializer.Deserialize<Single[]>(bytes, out Int32 read)!;
        Require(read == bytes.Length, "float array bytes-read mismatch");
        Require(Single.IsNaN(clone[0]), "nan mismatch");
        Require(Single.IsPositiveInfinity(clone[1]), "+inf mismatch");
        Require(Single.IsNegativeInfinity(clone[2]), "-inf mismatch");
        Require(clone[3] == Single.Epsilon && clone[4] == Single.MaxValue && clone[5] == Single.MinValue, "float values mismatch");
        return Result(bytes, String.Join('|', clone.Select(static x => Single.IsNaN(x) ? "NaN" : x.ToString("R"))));
    }

    private static ScenarioResult EnumByteRoundTrip()
    {
        PacketPriority value = PacketPriority.URGENT;
        Byte[] bytes = LiteSerializer.Serialize(value);
        PacketPriority clone = LiteSerializer.Deserialize<PacketPriority>(bytes, out Int32 read);
        Require(read == bytes.Length, "enum bytes-read mismatch");
        Require(value == clone, "enum mismatch");
        return Result(bytes, clone.ToString());
    }

    private static ScenarioResult SpanSerializeInt64()
    {
        Int64 value = 987654321012345678L;
        Byte[] bytes = new Byte[16];
        Int32 written = LiteSerializer.Serialize(value, bytes.AsSpan());
        Require(written == sizeof(Int64), "span written mismatch");
        Byte[] used = bytes[..written];
        Int64 clone = LiteSerializer.Deserialize<Int64>(used, out Int32 read);
        Require(read == written, "span read mismatch");
        Require(value == clone, "span int64 mismatch");
        return Result(used, clone.ToString());
    }

    private static ScenarioResult EmptyByteArrayRoundTrip()
    {
        Byte[] value = [];
        Byte[] bytes = LiteSerializer.Serialize(value);
        Byte[] clone = LiteSerializer.Deserialize<Byte[]>(bytes, out Int32 read)!;
        Require(read == bytes.Length, "empty array bytes-read mismatch");
        Require(clone.Length == 0, "empty array mismatch");
        return Result(bytes, clone.Length.ToString());
    }

    private static ScenarioResult NullIntArrayRoundTrip()
    {
        Int32[]? value = null;
        Byte[] bytes = LiteSerializer.Serialize(value);
        Int32[]? clone = LiteSerializer.Deserialize<Int32[]>(bytes, out Int32 read);
        Require(read == bytes.Length, "null array bytes-read mismatch");
        Require(clone is null, "null array mismatch");
        return Result(bytes, "<null>");
    }

    private static ScenarioResult UnmanagedArrayRoundTrip()
    {
        Int32[] value = Enumerable.Range(1, 256).Select(static i => i * 3).ToArray();
        Byte[] bytes = LiteSerializer.Serialize(value);
        Int32[] clone = LiteSerializer.Deserialize<Int32[]>(bytes, out Int32 read)!;
        Require(read == bytes.Length, "array bytes-read mismatch");
        Require(value.SequenceEqual(clone), "array mismatch");
        return Result(bytes, clone.Length.ToString());
    }

    private static ScenarioResult MalformedPrimitiveBuffer()
    {
        Byte[] bytes = [0x01, 0x02, 0x03];
        Exception ex = ExpectException(() => _ = LiteSerializer.Deserialize<Int32>(bytes, out _));
        return Result(bytes, ex.GetType().Name);
    }

    private static ScenarioResult TupleRoundTrip()
    {
        (Int32 Id, String Name, Boolean Active, Double Score, Int64 Stamp) value = (42, "nalix", true, 9.75, 9876543210L);
        Byte[] bytes = LiteSerializer.Serialize(value);
        (Int32 Id, String Name, Boolean Active, Double Score, Int64 Stamp) clone = LiteSerializer.Deserialize<(Int32, String, Boolean, Double, Int64)>(bytes, out _);
        Require(value.Equals(clone), "tuple mismatch");
        return Result(bytes, $"{clone.Id}|{clone.Name}|{clone.Active}|{clone.Score}|{clone.Stamp}");
    }

    private static ScenarioResult ComplexObjectRoundTrip()
    {
        UserDetails value = new()
        {
            Username = "nalix_dev",
            Roles = ["admin", "tester", "developer"],
            Attributes = new Dictionary<String, String>
            {
                ["region"] = "asia",
                ["tier"] = "premium"
            }
        };

        Byte[] bytes = LiteSerializer.Serialize(value);
        UserDetails clone = LiteSerializer.Deserialize<UserDetails>(bytes, out _)!;
        Require(clone.Username == value.Username, "username mismatch");
        Require(clone.Roles.SequenceEqual(value.Roles), "roles mismatch");
        Require(clone.Attributes["tier"] == "premium", "attributes mismatch");
        return Result(bytes, $"{clone.Username}|{clone.Roles.Count}|{clone.Attributes.Count}");
    }

    private static ScenarioResult PacketComplexCollections()
    {
        ComplexCollectionPacket packet = new()
        {
            SequenceId = 1234,
            IntList = [1, 2, 3],
            StringLongDict = new Dictionary<String, Int64> { ["a"] = 100L, ["b"] = 200L },
            StringQueue = new Queue<String>(["q1", "q2"]),
            FloatSet = [1.1f, 2.2f],
            Tuple3 = (42, "hello", true)
        };

        Byte[] bytes = packet.Serialize();
        Require(packet.Length >= bytes.Length, "packet length underflow");
        ComplexCollectionPacket clone = ComplexCollectionPacket.Deserialize(bytes);
        Require(clone.IntList!.SequenceEqual(packet.IntList), "list mismatch");
        Require(clone.StringLongDict!["b"] == 200L, "dict mismatch");
        Require(clone.Tuple3.Equals(packet.Tuple3), "tuple field mismatch");
        return Result(bytes, $"{clone.SequenceId}|{clone.IntList.Count}|{clone.StringLongDict.Count}|{clone.Tuple3.Id}");
    }

    private static ScenarioResult PacketNestedGraph()
    {
        GraphPacket packet = new()
        {
            Name = "Root",
            Nodes =
            [
                new GraphPacket { Name = "Child1", Meta = new NodeMeta { Id = 101 } },
                new GraphPacket { Name = "Child2", Nodes = [new GraphPacket { Name = "GrandChild" }] }
            ]
        };

        Byte[] bytes = packet.Serialize();
        Require(packet.Length >= bytes.Length, "graph length underflow");
        GraphPacket clone = GraphPacket.Deserialize(bytes);
        Require(clone.Nodes![0].Meta.Id == 101, "node meta mismatch");
        Require(clone.Nodes[1].Nodes![0].Name == "GrandChild", "deep node mismatch");
        return Result(bytes, $"{clone.Name}|{clone.Nodes.Count}|{clone.Nodes[1].Nodes.Count}");
    }

    private static ScenarioResult PacketLargePayload()
    {
        LargeDataPacket packet = new()
        {
            Payload = [.. Enumerable.Range(0, 5000).Select(static i => $"String_Data_Index_{i}")]
        };

        Byte[] bytes = packet.Serialize();
        Require(packet.Length >= bytes.Length, "large length underflow");
        LargeDataPacket clone = LargeDataPacket.Deserialize(bytes);
        Require(clone.Payload![^1] == "String_Data_Index_4999", "large payload mismatch");
        return Result(bytes, clone.Payload.Count.ToString());
    }

    private static ScenarioResult PacketNullStress()
    {
        NullStressPacket packet = new() { Items = ["", " ", null!, "\t", "\n", "content"] };
        Byte[] bytes = packet.Serialize();
        Require(packet.Length >= bytes.Length, "null stress length underflow");
        NullStressPacket clone = NullStressPacket.Deserialize(bytes);
        Require(clone.Items![2] is null, "null item mismatch");
        Require(clone.Items[5] == "content", "content mismatch");
        return Result(bytes, String.Join('|', clone.Items.Select(static x => x ?? "<null>")));
    }

    private static ScenarioResult PacketEnumCollections()
    {
        EnumListPacket packet = new() { Priorities = [PacketPriority.URGENT, PacketPriority.LOW, PacketPriority.HIGH] };
        Byte[] bytes = packet.Serialize();
        Require(packet.Length >= bytes.Length, "enum length underflow");
        EnumListPacket clone = EnumListPacket.Deserialize(bytes);
        Require(clone.Priorities!.SequenceEqual(packet.Priorities), "enum mismatch");
        return Result(bytes, String.Join(',', clone.Priorities));
    }

    private static ScenarioResult PacketGeneratedRegistry()
    {
        PacketRegistry.RegisterGenerated(
            PacketRegistry.Compute(typeof(ComplexCollectionPacket)),
            typeof(ComplexCollectionPacket).FullName!,
            static raw => PacketBase<ComplexCollectionPacket>.Deserialize(raw));
        PacketRegistry.Build();

        ComplexCollectionPacket packet = new()
        {
            IntList = [1, 2, 3],
            StringLongDict = new Dictionary<String, Int64> { ["a"] = 10 },
            StringQueue = new Queue<String>(["x", "y"]),
            FloatSet = [1.5f, 2.5f],
            Tuple3 = (7, "seven", true)
        };

        Byte[] bytes = packet.Serialize();
        IPacket resolved = PacketRegistry.Deserialize(bytes);
        ComplexCollectionPacket clone = (ComplexCollectionPacket)resolved;

        Require(PacketRegistry.IsRegistered<ComplexCollectionPacket>(), "generated registry missing complex packet");
        Require(clone.IntList!.SequenceEqual(packet.IntList), "generated registry int list mismatch");
        Require(clone.Tuple3 == packet.Tuple3, "generated registry tuple mismatch");
        return Result(bytes, PacketRegistry.DeserializerCount.ToString());
    }

    private static ScenarioResult PacketMalformedBuffer()
    {
        Byte[] bytes = [0x01, 0x02, 0x03, 0x04];
        Exception ex = ExpectException(() => _ = LargeDataPacket.Deserialize(bytes));
        return Result(bytes, ex.GetType().Name);
    }

    private static ScenarioResult Result(Byte[] bytes, String details)
        => new(String.Empty, String.Empty, true, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)), details, null);

    private static String ToJson(IEnumerable<ScenarioResult> results)
    {
        StringBuilder builder = new();
        builder.AppendLine("[");
        Boolean first = true;
        foreach (ScenarioResult result in results)
        {
            if (!first)
            {
                builder.AppendLine(",");
            }
            first = false;

            builder.AppendLine("  {");
            Append(builder, "Name", result.Name, true);
            Append(builder, "Category", result.Category, true);
            Append(builder, "Passed", result.Passed ? "true" : "false", true, raw: true);
            Append(builder, "Length", result.Length?.ToString(), true, raw: true);
            Append(builder, "Sha256", result.Sha256, true);
            Append(builder, "Details", result.Details, true);
            Append(builder, "Error", result.Error, false);
            builder.Append("  }");
        }
        builder.AppendLine();
        builder.AppendLine("]");
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, String name, String? value, Boolean comma, Boolean raw = false)
    {
        builder.Append("    \"").Append(name).Append("\": ");
        builder.Append(value is null ? "null" : raw ? value : $"\"{Escape(value)}\"");
        if (comma)
        {
            builder.Append(',');
        }
        builder.AppendLine();
    }

    private static String Escape(String value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal);

    private static void Require(Boolean condition, String message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static Exception ExpectException(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            return ex;
        }

        throw new InvalidOperationException("Expected exception was not thrown.");
    }
}

internal sealed record ScenarioResult(String Name, String Category, Boolean Passed, Int32? Length, String? Sha256, String? Details, String? Error);

[GenerateFormatter]
public sealed partial class UserDetails
{
    public String Username { get; set; } = String.Empty;
    public List<String> Roles { get; set; } = [];
    public Dictionary<String, String> Attributes { get; set; } = [];

    public static UserDetails Create() => new();
}

public struct NodeMeta
{
    public Int32 Id { get; set; }
}

[Packet]
[GenerateFormatter]
[SerializePackable(SerializeLayout.Sequential)]
public sealed partial class ComplexCollectionPacket : PacketBase<ComplexCollectionPacket>
{
    [SerializeOrder(0)]
    public List<Int32>? IntList { get; set; }

    [SerializeOrder(1)]
    public Dictionary<String, Int64>? StringLongDict { get; set; }

    [SerializeOrder(2)]
    public Queue<String>? StringQueue { get; set; }

    [SerializeOrder(3)]
    public HashSet<Single>? FloatSet { get; set; }

    [SerializeOrder(4)]
    public (Int32 Id, String Name, Boolean Active) Tuple3 { get; set; }

    public static new ComplexCollectionPacket Deserialize(ReadOnlySpan<Byte> buffer)
        => PacketBase<ComplexCollectionPacket>.Deserialize(buffer);
}

[GenerateFormatter]
[SerializePackable(SerializeLayout.Sequential)]
public sealed partial class GraphPacket : PacketBase<GraphPacket>
{
    public String Name { get; set; } = String.Empty;
    public List<GraphPacket>? Nodes { get; set; }
    public NodeMeta Meta { get; set; }

    public static new GraphPacket Deserialize(ReadOnlySpan<Byte> buffer)
        => PacketBase<GraphPacket>.Deserialize(buffer);
}

[GenerateFormatter]
[SerializePackable(SerializeLayout.Sequential)]
public sealed partial class LargeDataPacket : PacketBase<LargeDataPacket>
{
    public List<String>? Payload { get; set; }

    public static new LargeDataPacket Deserialize(ReadOnlySpan<Byte> buffer)
        => PacketBase<LargeDataPacket>.Deserialize(buffer);
}

[GenerateFormatter]
[SerializePackable(SerializeLayout.Sequential)]
public sealed partial class NullStressPacket : PacketBase<NullStressPacket>
{
    public List<String>? Items { get; set; }

    public static new NullStressPacket Deserialize(ReadOnlySpan<Byte> buffer)
        => PacketBase<NullStressPacket>.Deserialize(buffer);
}

[GenerateFormatter]
[SerializePackable(SerializeLayout.Sequential)]
public sealed partial class EnumListPacket : PacketBase<EnumListPacket>
{
    public List<PacketPriority>? Priorities { get; set; }

    public static new EnumListPacket Deserialize(ReadOnlySpan<Byte> buffer)
        => PacketBase<EnumListPacket>.Deserialize(buffer);
}
