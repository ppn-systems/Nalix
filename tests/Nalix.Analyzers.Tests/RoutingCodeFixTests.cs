// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using Nalix.Analyzers.CodeFixes;
using Xunit;

namespace Nalix.Analyzers.Tests;

public sealed class RoutingCodeFixTests
{
    [Fact]
    public async Task MissingPacketOpcode_ProducesCodeFix()
    {
        const string source = """
namespace Demo;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;

[PacketController]
public sealed class ExampleController
{
    public Task Handle(LoginPacket packet, IConnection connection)
    {
        return Task.CompletedTask;
    }
}

public sealed class LoginPacket : Nalix.Codec.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}
""";

        const string fixedSource = """
namespace Demo;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;

[PacketController]
public sealed class ExampleController
{
    [PacketOpcode(0x0000)]
    public Task Handle(LoginPacket packet, IConnection connection)
    {
        return Task.CompletedTask;
    }
}

public sealed class LoginPacket : Nalix.Codec.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}
""";

        await Verifier<PacketOpcodeCodeFixProvider>.VerifyCodeFixAsync(
            source,
            fixedSource,
            "NALIX002",
            actionIndex: 0,
            expectedTitle: "Add [PacketOpcode(...)]",
            expectedEquivalenceKey: "Nalix.PacketOpcode.Add");
    }

    [Fact]
    public async Task MissingPacketController_ProducesCodeFix()
    {
        const string source = """
namespace Demo;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Network.Routing; using Nalix.Runtime.Dispatching;

public sealed class ExampleController
{
    [PacketOpcode(1)]
    public void Handle(LoginPacket packet, Nalix.Abstractions.Networking.IConnection connection)
    {
    }
}

public sealed class LoginPacket : Nalix.Codec.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}

public sealed class Example
{
    public void Run()
    {
        new PacketDispatchOptions<LoginPacket>().WithHandler<ExampleController>();
    }
}
""";

        const string fixedSource = """
namespace Demo;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Network.Routing; using Nalix.Runtime.Dispatching;

[PacketController]
public sealed class ExampleController
{
    [PacketOpcode(1)]
    public void Handle(LoginPacket packet, Nalix.Abstractions.Networking.IConnection connection)
    {
    }
}

public sealed class LoginPacket : Nalix.Codec.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}

public sealed class Example
{
    public void Run()
    {
        new PacketDispatchOptions<LoginPacket>().WithHandler<ExampleController>();
    }
}
""";

        await Verifier<PacketControllerCodeFixProvider>.VerifyCodeFixAsync(
            source,
            fixedSource,
            "NALIX008",
            actionIndex: 0,
            expectedTitle: "Add [PacketController]",
            expectedEquivalenceKey: "Nalix.PacketController.Add");
    }

    [Fact]
    public async Task MissingRegistryDeserialize_ProducesCodeFix()
    {
        const string source = """
namespace Demo;
using Nalix.Codec.DataFrames;

public sealed class RegistryPacket : PacketBase<RegistryPacket>
{
}

public sealed class Example
{
    public void Run()
    {
        new PacketRegistryFactory().RegisterPacket<RegistryPacket>();
    }
}
""";

        const string fixedSource = """
namespace Demo;
using Nalix.Codec.DataFrames;

public sealed class RegistryPacket : PacketBase<RegistryPacket>
{
    public static new RegistryPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<RegistryPacket>.Deserialize(buffer);
}

public sealed class Example
{
    public void Run()
    {
        new PacketRegistryFactory().RegisterPacket<RegistryPacket>();
    }
}
""";

        await Verifier<PacketRegistryDeserializerCodeFixProvider>.VerifyCodeFixWithSyntheticDiagnosticAsync(
            source,
            fixedSource,
            "NALIX009",
            "Packet type 'RegistryPacket' is registered with PacketRegistryFactory.RegisterPacket, but it does not implement IPacketDeserializer<RegistryPacket>.",
            "RegisterPacket<RegistryPacket>",
            actionIndex: 0,
            expectedTitle: "Add Deserialize(ReadOnlySpan<byte>)",
            expectedEquivalenceKey: "Nalix.PacketRegistry.Deserialize.Add");
    }
}













