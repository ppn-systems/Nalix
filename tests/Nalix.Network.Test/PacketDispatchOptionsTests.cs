// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nalix.Common.Abstractions;
using Nalix.Common.Identity;
using Nalix.Common.Middleware;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Injection;
using Nalix.Network.Internal.Compilation;
using Nalix.Network.Routing;
using Xunit;

namespace Nalix.Network.Tests;

// ---------------------------------------------------------------------------
// Mock Connection for testing failures/responses.
// ---------------------------------------------------------------------------
internal sealed class DispatchTestMockConnection : IConnection
{
    public DispatchTestMockTransport MockTCP { get; } = new();
    public IConnection.ITransport TCP => this.MockTCP;
    public IConnection.ITransport UDP => throw new NotImplementedException();

    public ISnowflake ID => throw new NotImplementedException();
    public long UpTime => 0;
    public long BytesSent => 0;
    public long LastPingTime => 0;
    public INetworkEndpoint NetworkEndpoint => throw new NotImplementedException();
    public IObjectMap<string, object> Attributes => throw new NotImplementedException();
    public byte[] Secret { get; set; } = [];
    public PermissionLevel Level { get; set; }
    public CipherSuiteType Algorithm { get; set; }

    public int ErrorCount { get; private set; }

    public event EventHandler<IConnectEventArgs>? OnCloseEvent;
    public event EventHandler<IConnectEventArgs>? OnProcessEvent;
    public event EventHandler<IConnectEventArgs>? OnPostProcessEvent;

    public void Close(bool force = false) { }
    public void Disconnect(string? reason = null) { }
    public void Dispose() { }
    public void IncrementErrorCount() => this.ErrorCount++;
}

internal sealed class DispatchTestMockTransport : IConnection.ITransport
{
    public List<ReadOnlyMemory<byte>> SentMessages { get; } = new();

    public void Send(IPacket packet) { }
    public void Send(ReadOnlySpan<byte> message) { }
    public Task SendAsync(IPacket packet, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
    {
        this.SentMessages.Add(message);
        return Task.CompletedTask;
    }

    public void BeginReceive(CancellationToken cancellationToken = default) { }
}

// ---------------------------------------------------------------------------
// Controller with custom packet handlers using real packets.
// ---------------------------------------------------------------------------
[PacketController]
public sealed class RealPacketController
{
    public bool Handled { get; private set; }
    public Handshake? ReceivedHandshake { get; private set; }
    public Exception? ThrownException { get; set; }

    [PacketOpcode(0x0001)]
    public void HandleHandshake(Handshake packet, IConnection connection)
    {
        this.Handled = true;
        this.ReceivedHandshake = packet;
        if (this.ThrownException != null)
        {
            throw this.ThrownException;
        }
    }

    [PacketOpcode(0x0002)]
    public async Task HandleHandshakeAsync(Handshake packet, IConnection connection, CancellationToken ct)
    {
        this.Handled = true;
        this.ReceivedHandshake = packet;
        await Task.CompletedTask.ConfigureAwait(false);
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------
public sealed class PacketDispatchOptionsTests
{
    private static void EnsureLogger()
    {
        _ = InstanceManager.Instance.WithLogging(NullLogger.Instance);
        InstanceManager.Instance.Register<ILogger>(NullLogger.Instance);
    }

    [Fact]
    public void WithHandler_RegistersRealPacketHandlersCorrectly()
    {
        EnsureLogger();
        PacketDispatchOptions<IPacket> options = new();

        _ = options.WithHandler<RealPacketController>();

        _ = options.RegisteredHandlerCount.Should().Be(2);
        _ = options.TryResolveHandler(0x0001, out _).Should().BeTrue();
        _ = options.TryResolveHandler(0x0002, out _).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteResolvedHandlerAsync_CallsHandlerWithRealHandshakePacket()
    {
        EnsureLogger();
        RealPacketController controller = new();
        PacketDispatchOptions<IPacket> options = new();
        _ = options.WithHandler(controller);

        // OpCode 0x0001 is mapped to HandleHandshake
        Handshake packet = new() { OpCode = 0x0001, Stage = HandshakeStage.CLIENT_HELLO };
        DispatchTestMockConnection connection = new();

        _ = options.TryResolveHandler(0x0001, out PacketHandler<IPacket> handler).Should().BeTrue();

        await options.ExecuteResolvedHandlerAsync(handler, packet, connection);

        _ = controller.Handled.Should().BeTrue();
        _ = controller.ReceivedHandshake.Should().Be(packet);
        _ = controller.ReceivedHandshake.Stage.Should().Be(HandshakeStage.CLIENT_HELLO);
    }

    [Fact]
    public async Task ExecuteResolvedHandlerAsync_HandlesTypeMismatch_WithRealPacket()
    {
        EnsureLogger();
        RealPacketController controller = new();
        PacketDispatchOptions<IPacket> options = new();
        _ = options.WithHandler(controller);

        // Handler 0x0001 expects Handshake, but we send a different IPacket implementation
        FakePacket packet = new() { OpCode = 0x0001 };
        DispatchTestMockConnection connection = new();

        _ = options.TryResolveHandler(0x0001, out PacketHandler<IPacket> handler).Should().BeTrue();

        await options.ExecuteResolvedHandlerAsync(handler, packet, connection);

        _ = controller.Handled.Should().BeFalse();
        // Should have attempted to send a failure response.
        _ = connection.MockTCP.SentMessages.Should().NotBeEmpty();
    }

    private sealed class FakePacket : IPacket
    {
        public int Length => 0;
        public uint MagicNumber { get; set; }
        public ushort OpCode { get; set; }
        public PacketFlags Flags { get; set; }
        public PacketPriority Priority { get; set; }
        public ProtocolType Protocol { get; set; }
        public uint SequenceId { get; set; }
        public byte[] Serialize() => [];
        public int Serialize(Span<byte> buffer) => 0;
    }

    [Fact]
    public async Task ExecuteResolvedHandlerAsync_InvokesCustomErrorHandler_WithRealPacket()
    {
        EnsureLogger();
        RealPacketController controller = new() { ThrownException = new InvalidOperationException("Real Error") };
        Exception? capturedException = null;
        ushort capturedOpCode = 0;

        PacketDispatchOptions<IPacket> options = new PacketDispatchOptions<IPacket>()
            .WithHandler(controller)
            .WithErrorHandling((ex, opcode) =>
            {
                capturedException = ex;
                capturedOpCode = opcode;
            });

        Handshake packet = new() { OpCode = 0x0001 };
        DispatchTestMockConnection connection = new();

        _ = options.TryResolveHandler(0x0001, out PacketHandler<IPacket> handler).Should().BeTrue();

        await options.ExecuteResolvedHandlerAsync(handler, packet, connection);

        _ = capturedException.Should().BeOfType<InvalidOperationException>();
        _ = capturedException.Message.Should().Be("Real Error");
        _ = capturedOpCode.Should().Be(0x0001);
    }

    [Fact]
    public async Task ExecuteResolvedHandlerAsync_RunsMiddleware_WithRealPacket()
    {
        EnsureLogger();
        RealPacketController controller = new();
        bool middlewareExecuted = false;

        MockMiddleware<IPacket> mockMiddleware = new(() => middlewareExecuted = true);

        PacketDispatchOptions<IPacket> options = new PacketDispatchOptions<IPacket>()
            .WithHandler(controller)
            .WithMiddleware(mockMiddleware);

        Handshake packet = new() { OpCode = 0x0001 };
        DispatchTestMockConnection connection = new();

        _ = options.TryResolveHandler(0x0001, out PacketHandler<IPacket> handler).Should().BeTrue();

        await options.ExecuteResolvedHandlerAsync(handler, packet, connection);

        _ = middlewareExecuted.Should().BeTrue();
        _ = controller.Handled.Should().BeTrue();
    }

    private sealed class MockMiddleware<TPacket> : IPacketMiddleware<TPacket> where TPacket : IPacket
    {
        private readonly Action _onExecute;
        public MockMiddleware(Action onExecute) => _onExecute = onExecute;

        public ValueTask InvokeAsync(IPacketContext<TPacket> context, Func<CancellationToken, ValueTask> next)
        {
            _onExecute();
            return next(context.CancellationToken);
        }
    }
}
