// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using Nalix.Analyzers.CodeFixes;
using Xunit;

namespace Nalix.Analyzers.Tests;

public sealed class MetadataProviderAnalyzerTests
{
    [Fact]
    public async Task Populate_ClearsOpcode_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using System.Reflection;
using Nalix.Network.Routing;

public sealed class BadProvider : IPacketMetadataProvider
{
    public void Populate(MethodInfo method, PacketMetadataBuilder builder)
    {
        builder.Opcode = null;
    }
}
""";

        await Verifier<ConfigurationIgnoreCodeFixProvider>.VerifyAnalyzerAsync(
            source,
            "NALIX025");
    }

    [Fact]
    public async Task Populate_OverwritesOpcodeWithoutGuard_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using System.Reflection;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Routing;

public sealed class BadProvider : IPacketMetadataProvider
{
    public void Populate(MethodInfo method, PacketMetadataBuilder builder)
    {
        builder.Opcode = new PacketOpcodeAttribute(7);
    }
}
""";

        await Verifier<ConfigurationIgnoreCodeFixProvider>.VerifyAnalyzerAsync(
            source,
            "NALIX026");
    }

    [Fact]
    public async Task Populate_OverwritesOpcodeWithGuard_IsSilent()
    {
        const string source = """
namespace Demo;
using System.Reflection;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Routing;

public sealed class GoodProvider : IPacketMetadataProvider
{
    public void Populate(MethodInfo method, PacketMetadataBuilder builder)
    {
        if (builder.Opcode is null)
        {
            builder.Opcode = new PacketOpcodeAttribute(7);
        }
    }
}
""";

        await Verifier<ConfigurationIgnoreCodeFixProvider>.VerifyAnalyzerAsync(source);
    }
}
