// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using Xunit;

namespace Nalix.Analyzers.Tests;

public sealed class CustomControllerAnalyzerTests
{
    [Fact]
    public async Task ReservedOpcode_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using Nalix.Abstractions.Networking.Packets;

[PacketController]
public sealed class MyController
{
    [PacketOpcode(0x0001)]
    public void Handle(LoginPacket packet, Nalix.Abstractions.Networking.IConnection connection) { }
}

public sealed class LoginPacket : Nalix.Codec.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}
""";

        await Verifier<CodeFixes.PacketOpcodeCodeFixProvider>.VerifyAnalyzerAsync(
            source,
            "NALIX035");
    }

    [Fact]
    public async Task GlobalDuplicateOpcode_ProducesDiagnostic()
    {
        const string source1 = """
namespace Demo;
using Nalix.Abstractions.Networking.Packets;

[PacketController]
public sealed class Controller1
{
    [PacketOpcode(0x0200)]
    public void Handle(LoginPacket packet, Nalix.Abstractions.Networking.IConnection connection) { }
}

public sealed class LoginPacket : Nalix.Codec.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}
""";

        const string source2 = """
namespace Demo;
using Nalix.Abstractions.Networking.Packets;

[PacketController]
public sealed class Controller2
{
    [PacketOpcode(0x0200)]
    public void Handle(LoginPacket packet, Nalix.Abstractions.Networking.IConnection connection) { }
}
""";

        await Verifier<CodeFixes.PacketOpcodeCodeFixProvider>.VerifyAnalyzerAsync(
            [source1, source2],
            "NALIX036");
    }

    [Fact]
    public async Task NonReservedOpcode_IsSilent()
    {
        const string source = """
namespace Demo;
using Nalix.Abstractions.Networking.Packets;

[PacketController]
public sealed class MyController
{
    [PacketOpcode(0x0150)]
    public void Handle(LoginPacket packet, Nalix.Abstractions.Networking.IConnection connection) { }
}

public sealed class LoginPacket : Nalix.Codec.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}
""";

        await Verifier<CodeFixes.PacketOpcodeCodeFixProvider>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task HotPathAllocation_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using Nalix.Abstractions.Networking.Packets;

[PacketController]
public sealed class MyController
{
    [PacketOpcode(0x0200)]
    public void Handle(LoginPacket packet, Nalix.Abstractions.Networking.IConnection connection) 
    { 
        var x = new object(); // Allocation in hot path
    }
}

public sealed class LoginPacket : Nalix.Codec.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}
""";

        await Verifier<CodeFixes.PacketOpcodeCodeFixProvider>.VerifyAnalyzerAsync(
            source,
            "NALIX037");
    }

    [Fact]
    public async Task OpCodeDocMismatch_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using Nalix.Abstractions.Networking.Packets;

[PacketController]
public sealed class MyController
{
    /// <summary>
    /// Authenticates a user. OpCode: 0x0150
    /// </summary>
    [PacketOpcode(0x0300)]
    public void Handle(LoginPacket packet, Nalix.Abstractions.Networking.IConnection connection) { }
}

public sealed class LoginPacket : Nalix.Codec.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}
""";

        await Verifier<CodeFixes.PacketOpcodeCodeFixProvider>.VerifyAnalyzerAsync(
            source,
            "NALIX038");
    }

}













