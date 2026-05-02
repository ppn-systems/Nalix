// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Framework.Extensions;
using Nalix.Framework.Memory.Objects;
using Nalix.Environment.Time;
using Xunit;

namespace Nalix.Framework.Tests.Extensions;

public sealed class ConnectionLoggingExtensionsTests
{
    [Fact]
    public void ThrottledWarnWhenCalledTwiceInWindowSuppressesSecondMessage()
    {
        Clock.ResetSynchronization();
        TestConnection connection = new();
        TestLogger logger = new();

        connection.ThrottledWarn(logger, "dup", "first");
        connection.ThrottledWarn(logger, "dup", "first");

        _ = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, logger.Entries[0].Level);
        Assert.Equal("first", logger.Entries[0].Message);
    }

    [Fact]
    public void ThrottledWarnWhenWindowHasElapsedLogsWithSuppressedSuffix()
    {
        Clock.ResetSynchronization();
        TestConnection connection = new();
        TestLogger logger = new();

        connection.ThrottledWarn(logger, "dup", "hello");
        connection.ThrottledWarn(logger, "dup", "hello");

        Assert.True(connection.Attributes.TryGetValue("sys.log.dup", out object? state));
        Assert.NotNull(state);

        FieldInfo? lastLogTicksField = state!.GetType().GetField("LastLogTicks");
        lastLogTicksField!.SetValue(state, DateTime.UtcNow.AddSeconds(-20).Ticks);

        connection.ThrottledWarn(logger, "dup", "hello");

        Assert.Equal(2, logger.Entries.Count);
        Assert.Contains("(+1 suppressed)", logger.Entries[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ThrottledErrorWhenExceptionProvidedLogsErrorWithException()
    {
        TestConnection connection = new();
        TestLogger logger = new();
        InvalidOperationException error = new("boom");

        connection.ThrottledError(logger, "err", "failed", error);

        _ = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, logger.Entries[0].Level);
        Assert.Equal("failed", logger.Entries[0].Message);
        Assert.Same(error, logger.Entries[0].Exception);
    }

    [Fact]
    public void ThrottledWarnWhenConnectionIsNullStillLogs()
    {
        TestLogger logger = new();

        ThrottleLogExtensions.ThrottledWarn(null!, logger, "trace", "ping");

        _ = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, logger.Entries[0].Level);
        Assert.Equal("ping", logger.Entries[0].Message);
    }

    [Fact]
    public void ThrottledMethodsWhenLoggerIsNullDoNotThrow()
    {
        TestConnection connection = new();

        Exception? exception = Record.Exception(() =>
        {
            connection.ThrottledWarn(null, "x", "w");
            connection.ThrottledError(null, "x", "e");
        });

        Assert.Null(exception);
    }

    private sealed class TestConnection : IConnection
    {
        public ISnowflake ID { get; } = new TestSnowflake();
        public long UpTime => 0;
        public long BytesSent => 0;
        public long LastPingTime => 0;
        public INetworkEndpoint NetworkEndpoint { get; } = new TestEndpoint();
        public IObjectMap<string, object> Attributes { get; } = new ObjectMap<string, object>();
        public IConnection.ITransport TCP { get; } = new TestTransport();
        public IConnection.ITransport UDP { get; } = new TestTransport();
        public Bytes32 Secret { get; set; } = Bytes32.Zero;
        public PermissionLevel Level { get; set; } = PermissionLevel.NONE;
        public CipherSuiteType Algorithm { get; set; } = CipherSuiteType.Chacha20Poly1305;
        public int ErrorCount { get; private set; }
        public int TimeoutVersion { get; set; }
        public bool IsRegisteredInWheel { get; set; }
        public bool IsDisposed => false;

        public event EventHandler<IConnectEventArgs>? OnCloseEvent { add { } remove { } }
        public event EventHandler<IConnectEventArgs>? OnProcessEvent { add { } remove { } }
        public event EventHandler<IConnectEventArgs>? OnPostProcessEvent { add { } remove { } }

        public void Disconnect(bool force = false) { }

        public void Disconnect(string? reason = null) { }

        public void Dispose() { }

        public void IncrementErrorCount() => this.ErrorCount++;
    }

    private sealed class TestTransport : IConnection.ITransport
    {
        public void BeginReceive(System.Threading.CancellationToken cancellationToken = default) { }

        public void Send(IPacket packet) { }

        public void Send(ReadOnlySpan<byte> message) { }

        public ValueTask SendAsync(IPacket packet, System.Threading.CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask SendAsync(ReadOnlyMemory<byte> message, System.Threading.CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class TestEndpoint : INetworkEndpoint
    {
        public string Address => "127.0.0.1";
        public int Port => 7777;
        public bool HasPort => true;
        public bool IsIPv6 => false;
    }

    private sealed class TestSnowflake : ISnowflake
    {
        public bool IsEmpty => false;
        public SnowflakeType Type => SnowflakeType.System;
        public uint Value => 1;
        public ushort MachineId => 1;

        public ulong ToUInt64() => 0UL;

        public byte[] ToByteArray() => new byte[8];

        public bool TryWriteBytes(Span<byte> destination)
        {
            if (destination.Length < 8)
            {
                return false;
            }

            destination[..8].Clear();
            return true;
        }

        public bool TryWriteBytes(Span<byte> destination, out int bytesWritten)
        {
            bool ok = this.TryWriteBytes(destination);
            bytesWritten = ok ? 8 : 0;
            return ok;
        }
    }

    private sealed class TestLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => this.Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    private readonly record struct LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}














