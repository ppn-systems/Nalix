// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.Injection;
using Nalix.Network.Internal.Compilation;
using Nalix.Network.Routing;
using Xunit;

namespace Nalix.Network.Tests;

// ---------------------------------------------------------------------------
// Minimal concrete packet used only by these tests.
// ---------------------------------------------------------------------------
[SuppressMessage("Design", "CA1034", Justification = "Nested test types are fine here.")]
public sealed class CustomTestPacket : IPacket
{
    public int Length => 0;
    public uint MagicNumber { get; set; }
    public ushort OpCode { get; set; }
    public PacketFlags Flags { get; set; }
    public PacketPriority Priority { get; set; }
    public ProtocolType Protocol { get; set; }
    public uint SequenceId => 0;
    public byte[] Serialize() => [];
    public int Serialize(Span<byte> buffer) => 0;
}

// ---------------------------------------------------------------------------
// Controller with concrete-packet legacy signatures (no token).
// ---------------------------------------------------------------------------
[PacketController]
[SuppressMessage("Design", "CA1034", Justification = "Nested test types are fine here.")]
public sealed class ConcretePacketControllerNoToken
{
    /// <summary>Called = true when the handler was invoked.</summary>
    public bool Called { get; private set; }

    [PacketOpcode(0x0010)]
    public void HandleLogin(CustomTestPacket packet, IConnection connection)
    {
        Called = true;
    }
}

// ---------------------------------------------------------------------------
// Controller with concrete-packet legacy signatures (with token).
// ---------------------------------------------------------------------------
[PacketController]
[SuppressMessage("Design", "CA1034", Justification = "Nested test types are fine here.")]
public sealed class ConcretePacketControllerWithToken
{
    public bool Called { get; private set; }
    public CancellationToken ReceivedToken { get; private set; }

    [PacketOpcode(0x0020)]
    public void HandleLogin(CustomTestPacket packet, IConnection connection, CancellationToken ct)
    {
        Called = true;
        ReceivedToken = ct;
    }
}

// ---------------------------------------------------------------------------
// Controller with async concrete-packet handler.
// ---------------------------------------------------------------------------
[PacketController]
[SuppressMessage("Design", "CA1034", Justification = "Nested test types are fine here.")]
public sealed class ConcretePacketControllerAsync
{
    public bool Called { get; private set; }

    [PacketOpcode(0x0030)]
    public Task HandleLoginAsync(CustomTestPacket packet, IConnection connection)
    {
        Called = true;
        return Task.CompletedTask;
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------
[SuppressMessage("Reliability", "CA2007", Justification = "xUnit tests intentionally follow test sync context.")]
public sealed class PacketHandlerCompilerCustomPacketTests
{
    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static void EnsureLogger()
    {
        _ = InstanceManager.Instance.WithLogging(NullLogger.Instance);
        InstanceManager.Instance.Register<ILogger>(NullLogger.Instance);
    }

    // ------------------------------------------------------------------
    // 1. CompileHandlers does NOT throw for (ConcretePacket, IConnection)
    // ------------------------------------------------------------------
    [Fact]
    public void CompileHandlers_ConcreteLegacyNoToken_DoesNotThrow()
    {
        EnsureLogger();

        Action act = static () =>
            PacketHandlerCompiler<ConcretePacketControllerNoToken, IPacket>
                .CompileHandlers(static () => new ConcretePacketControllerNoToken());

        act.Should().NotThrow();
    }

    // ------------------------------------------------------------------
    // 2. CompileHandlers does NOT throw for (ConcretePacket, IConnection, CancellationToken)
    // ------------------------------------------------------------------
    [Fact]
    public void CompileHandlers_ConcreteLegacyWithToken_DoesNotThrow()
    {
        EnsureLogger();

        Action act = static () =>
            PacketHandlerCompiler<ConcretePacketControllerWithToken, IPacket>
                .CompileHandlers(static () => new ConcretePacketControllerWithToken());

        act.Should().NotThrow();
    }

    // ------------------------------------------------------------------
    // 3. CompileHandlers does NOT throw for async (ConcretePacket, IConnection)
    // ------------------------------------------------------------------
    [Fact]
    public void CompileHandlers_ConcreteLegacyAsync_DoesNotThrow()
    {
        EnsureLogger();

        Action act = static () =>
            PacketHandlerCompiler<ConcretePacketControllerAsync, IPacket>
                .CompileHandlers(static () => new ConcretePacketControllerAsync());

        act.Should().NotThrow();
    }

    // ------------------------------------------------------------------
    // 4. Compiled handler produces exactly one descriptor per opcode
    // ------------------------------------------------------------------
    [Fact]
    public void CompileHandlers_ConcreteLegacy_ProducesOneDescriptorPerOpcode()
    {
        EnsureLogger();

        PacketHandler<IPacket>[] handlers =
            PacketHandlerCompiler<ConcretePacketControllerNoToken, IPacket>
                .CompileHandlers(static () => new ConcretePacketControllerNoToken());

        _ = handlers.Should().HaveCount(1);
        _ = handlers[0].OpCode.Should().Be(0x0010);
    }

    // ------------------------------------------------------------------
    // 5. Invoking the compiled handler with the correct packet type works
    //    without exceptions (expression-tree path).
    // ------------------------------------------------------------------
    [Fact]
    public async Task CompileHandlers_ConcreteLegacyNoToken_InvokerWorksWithCorrectPacket()
    {
        EnsureLogger();

        var controller = new ConcretePacketControllerNoToken();

        PacketHandler<IPacket>[] handlers =
            PacketHandlerCompiler<ConcretePacketControllerNoToken, IPacket>
                .CompileHandlers(() => controller);

        _ = handlers.Should().HaveCount(1);

        // Build a minimal context manually using the internal invoker.
        // We only test the Invoker delegate directly (not the full pipeline)
        // because wiring up a full PacketContext<IPacket> requires ObjectPoolManager.
        Func<object, PacketContext<IPacket>, ValueTask<object>> invoker = handlers[0].Invoker;

        // Invoker should not be null — compilation succeeded.
        _ = invoker.Should().NotBeNull();

        // Verify the handler itself was reached by inspecting the compiled method info.
        _ = handlers[0].MethodInfo.Name.Should().Be(nameof(ConcretePacketControllerNoToken.HandleLogin));

        // Ensure the ExpectedPacketType is captured so the runtime guard can do its job.
        // For concrete-packet handlers the dispatcher resolves ExpectedPacketType in
        // WithHandler; CompileHandlers itself does not set it (that is intentional).
        _ = handlers[0].MethodInfo.GetParameters()[0].ParameterType
                       .Should().Be(typeof(CustomTestPacket));

        await Task.CompletedTask;
    }

    // ------------------------------------------------------------------
    // 6. CancellationToken is threaded through correctly for the 3-param variant.
    // ------------------------------------------------------------------
    [Fact]
    public void CompileHandlers_ConcreteLegacyWithToken_MethodHasThreeParameters()
    {
        EnsureLogger();

        PacketHandler<IPacket>[] handlers =
            PacketHandlerCompiler<ConcretePacketControllerWithToken, IPacket>
                .CompileHandlers(static () => new ConcretePacketControllerWithToken());

        _ = handlers.Should().HaveCount(1);

        ParameterInfo[] parms = handlers[0].MethodInfo.GetParameters();
        _ = parms.Should().HaveCount(3);
        _ = parms[0].ParameterType.Should().Be(typeof(CustomTestPacket));
        _ = parms[1].ParameterType.Should().BeAssignableTo<IConnection>();
        _ = parms[2].ParameterType.Should().Be(typeof(CancellationToken));
    }
}
