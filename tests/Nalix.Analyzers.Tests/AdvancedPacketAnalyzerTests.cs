// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using Xunit;

namespace Nalix.Analyzers.Tests;

public sealed class AdvancedPacketAnalyzerTests
{

    [Fact]
    public async Task RegisterPacketWithAbstractType_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using Nalix.Codec.DataFrames;

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

}













