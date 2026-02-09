// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using Xunit;

namespace Nalix.Analyzers.Tests;

public sealed class AdvancedPacketAnalyzerTests
{
    [Fact]
    public async Task InvalidDeserializeSignature_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using System;
using Nalix.Framework.DataFrames;

public sealed class BadPacket : PacketBase<BadPacket>
{
    public static int Deserialize(ReadOnlySpan<byte> buffer) => 0;
}
""";

        await Verifier<CodeFixes.PacketDeserializeCodeFixProvider>.VerifyAnalyzerAsync(
            source,
            "NALIX017");
    }

    [Fact]
    public async Task RegisterPacketWithAbstractType_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using Nalix.Framework.DataFrames;

public abstract class AbstractPacket : PacketBase<AbstractPacket>
{
}

public sealed class Example
{
    public void Run()
    {
        new PacketRegistryFactory().RegisterPacket<AbstractPacket>();
    }
}
""";

        await Verifier<CodeFixes.PacketRegistryDeserializerCodeFixProvider>.VerifyAnalyzerAsync(
            source,
            "NALIX018");
    }

    [Fact]
    public async Task WithBufferMiddlewareTypeMismatch_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Routing;

public sealed class SomePacket : Nalix.Framework.DataFrames.PacketBase<SomePacket>
{
    public static new SomePacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<SomePacket>.Deserialize(buffer);
}

public sealed class NotBufferMiddleware
{
}

public sealed class Example
{
    public void Run()
    {
        new PacketDispatchOptions<SomePacket>().WithBufferMiddleware((Nalix.Network.Middleware.INetworkBufferMiddleware)(object)new NotBufferMiddleware());
    }
}
""";

        await Verifier<CodeFixes.PacketRegistryDeserializerCodeFixProvider>.VerifyAnalyzerAsync(
            source,
            "NALIX019");
    }
}
