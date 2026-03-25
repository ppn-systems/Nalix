// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using Nalix.Analyzers.CodeFixes;
using Xunit;

namespace Nalix.Analyzers.Tests;

public sealed class PacketAnalyzerTests
{
    [Fact]
    public async Task PacketDeserializeSpanOverloadMissing_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking.Packets;

public sealed class MissingSpanPacket : IPacket
{
    public static MissingSpanPacket Deserialize(byte[] buffer) => new();
}
""";

        await Verifier<ResetForPoolCodeFixProvider>.VerifyAnalyzerAsync(
            source,
            "NALIX052");
    }

    [Fact]
    public async Task PacketBaseWithInheritedSpanDeserialize_DoesNotProduceDiagnostic()
    {
        const string source = """
namespace Demo;
using Nalix.Framework.DataFrames;

public sealed class InheritedSpanPacket : PacketBase<InheritedSpanPacket>
{
    public static InheritedSpanPacket Deserialize(byte[] buffer) => new();
}
""";

        await Verifier<ResetForPoolCodeFixProvider>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NonPacketDeserializeUtility_DoesNotProducePacketDiagnostic()
    {
        const string source = """
namespace Demo;

public static class LiteSerializerLike
{
    public static int Deserialize(byte[] buffer, out int bytesRead)
    {
        bytesRead = buffer.Length;
        return bytesRead;
    }

    public static int Deserialize(ReadOnlyMemory<byte> buffer, out int bytesRead)
    {
        bytesRead = buffer.Length;
        return bytesRead;
    }
}
""";

        await Verifier<ResetForPoolCodeFixProvider>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ResetForPoolWithoutBaseCall_ProducesDiagnosticAndFix()
    {
        const string source = """
namespace Demo;
using Nalix.Framework.DataFrames;

public sealed class MyPacket : PacketBase<MyPacket>
{
    public override void ResetForPool()
    {
    }
}
""";

        const string fixedSource = """
namespace Demo;
using Nalix.Framework.DataFrames;

public sealed class MyPacket : PacketBase<MyPacket>
{
    public override void ResetForPool()
    {
        base.ResetForPool();
    }
}
""";

        await Verifier<ResetForPoolCodeFixProvider>.VerifyCodeFixAsync(
            source,
            fixedSource,
            "NALIX020",
            actionIndex: 0,
            expectedTitle: "Add base.ResetForPool()",
            expectedEquivalenceKey: "Nalix.Packet.ResetForPool.AddBaseCall");
    }

    [Fact]
    public async Task PacketBaseSelfTypeMismatch_ProducesDiagnosticAndFix()
    {
        const string source = """
namespace Demo;
using Nalix.Framework.DataFrames;

public sealed class WrongPacket : PacketBase<OtherPacket>
{
}

public sealed class OtherPacket : PacketBase<OtherPacket>
{
}
""";

        const string fixedSource = """
namespace Demo;
using Nalix.Framework.DataFrames;

public sealed class WrongPacket : PacketBase<WrongPacket>
{
}

public sealed class OtherPacket : PacketBase<OtherPacket>
{
}
""";

        await Verifier<PacketSelfTypeCodeFixProvider>.VerifyCodeFixAsync(
            source,
            fixedSource,
            "NALIX010",
            actionIndex: 0,
            expectedTitle: "Fix PacketBase<TSelf> to use containing type",
            expectedEquivalenceKey: "Nalix.PacketBase.SelfType.Fix");
    }

    [Fact]
    public async Task PacketDeserializerSelfTypeMismatch_ProducesDiagnosticAndFix()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

public sealed class WrongPacket : PacketBase<WrongPacket>, IPacketDeserializer<OtherPacket>
{
    public static new WrongPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<WrongPacket>.Deserialize(buffer);
}

public sealed class OtherPacket : PacketBase<OtherPacket>
{
    public static new OtherPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<OtherPacket>.Deserialize(buffer);
}
""";

        const string fixedSource = """
namespace Demo;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

public sealed class WrongPacket : PacketBase<WrongPacket>, IPacketDeserializer<WrongPacket>
{
    public static new WrongPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<WrongPacket>.Deserialize(buffer);
}

public sealed class OtherPacket : PacketBase<OtherPacket>
{
    public static new OtherPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<OtherPacket>.Deserialize(buffer);
}
""";

        await Verifier<PacketSelfTypeCodeFixProvider>.VerifyCodeFixAsync(
            source,
            fixedSource,
            "NALIX011",
            actionIndex: 0,
            expectedTitle: "Fix IPacketDeserializer<T> to use containing type",
            expectedEquivalenceKey: "Nalix.PacketDeserializer.SelfType.Fix");
    }

}
