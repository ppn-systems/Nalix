// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.DataFrames;
using Nalix.Network.Routing;
using Nalix.Runtime.Internal.Compilation;

namespace Nalix.Runtime.Dispatching;

/// <summary>
/// A lightweight packet dispatcher that executes handlers inline on the thread pool
/// without the overhead of a dedicated dispatch queue or background worker loops.
/// </summary>
[DebuggerNonUserCode]
[SkipLocalsInit]
public sealed class InlinePacketDispatcher
    : PacketDispatcherBase<IPacket>, IPacketDispatch, IActivatable
{
    private int _running;

    /// <summary>
    /// Initializes a new instance of the <see cref="InlinePacketDispatcher"/> class.
    /// </summary>
    /// <param name="options">Option builder.</param>
    public InlinePacketDispatcher(Action<PacketDispatchOptions<IPacket>>? options = null) : base(options)
    {
    }

    /// <inheritdoc />
    [StackTraceHidden]
    public void Activate(CancellationToken cancellationToken = default) => Volatile.Write(ref _running, 1);

    /// <inheritdoc />
    [StackTraceHidden]
    public void Deactivate(CancellationToken cancellationToken = default) => Volatile.Write(ref _running, 0);

    /// <inheritdoc />
    public void Dispose() => this.Deactivate();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void HandlePacket(IBufferLease lease, IConnection connection)
    {
        if (Volatile.Read(ref _running) == 0)
        {
            lease?.Dispose();
            return;
        }

        if (lease is null || connection is null)
        {
            lease?.Dispose();
            return;
        }

        if ((uint)lease.Length < PacketConstants.HeaderSize)
        {
            lease.Dispose();
            connection.IncrementErrorCount();
            return;
        }

        lease.Retain();

        // Queue to thread pool to avoid blocking the IO thread.
        // Uses UnsafeQueueUserWorkItem to bypass ExecutionContext flow for maximum performance.
        _ = ThreadPool.UnsafeQueueUserWorkItem(state =>
        {
            (InlinePacketDispatcher? dispatcher, IBufferLease? l, IConnection? c) = state;
            _ = dispatcher.ExecutePacketAsync(c, l, CancellationToken.None).AsTask();
        }, (this, lease, connection), preferLocal: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private ValueTask ExecutePacketAsync(IConnection connection, IBufferLease lease, CancellationToken ct)
    {
        // 1. Read the packet header directly from the raw span to determine routing
        PacketHeader header = MemoryMarshal.Read<PacketHeader>(lease.Span);

        // 2. Resolve the handler using the parsed opcode
        if (!this.Options.TryResolveHandler(header.OpCode, out PacketHandler<IPacket> handler))
        {
            if (this.Logging != null && this.Logging.IsEnabled(LogLevel.Warning))
            {
                connection.ThrottledWarn(
                    this.Logging,
                    "dispatch.execute",
                    $"[RT.{nameof(InlinePacketDispatcher)}:{nameof(ExecutePacketAsync)}] no-handler opcode={header.OpCode}");
            }

            lease.Dispose();
            connection.IncrementErrorCount();
            return ValueTask.CompletedTask;
        }

        IPacket packet;

        // 3. Bypass deserialization if the handler expects raw memory
        if (handler.ExpectedPacketType == typeof(MemoryPacket))
        {
            packet = new MemoryPacket(lease.Memory, header);
        }
        else
        {
            // 4. Normal deserialization fallback for structured packets
            if (!PacketRegistry.TryDeserialize(lease.Span, out IPacket? deserialized) || deserialized is null)
            {
                lease.Dispose();
                connection.IncrementErrorCount();
                return ValueTask.CompletedTask;
            }

            packet = deserialized;
        }

        try
        {
            /*
             * [Packet Handler Execution]
             * 1. Attempt to execute the resolved handler.
             * 2. If it completes synchronously, we can dispose resources immediately.
             * 3. If it's asynchronous, we hand off to AwaitPacketHandlerCompletionAsync.
             */
            ValueTask pending = this.Options.ExecuteResolvedHandlerAsync(in handler, packet, connection, ct);

            // Fast-path: handler completed synchronously
            if (pending.IsCompletedSuccessfully)
            {
                if (packet is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                lease.Dispose();
                return ValueTask.CompletedTask;
            }

            // Slow-path: async completion (AwaitDispatchAsync handles Return/Dispose)
            return AwaitPacketHandlerCompletionAsync(this, connection, lease, packet, pending, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // External cancellation during sync execution
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            connection.IncrementErrorCount();

            if (this.Logging != null && this.Logging.IsEnabled(LogLevel.Error))
            {
                connection.ThrottledError(
                    this.Logging,
                    "dispatch.execute",
                    $"[RT.{nameof(InlinePacketDispatcher)}:{nameof(ExecutePacketAsync)}] handler-error ep={connection.NetworkEndpoint}");
            }
        }

        // 5. Cleanup for synchronous errors/cancellation
        if (packet is IDisposable disposableSync)
        {
            disposableSync.Dispose();
        }

        lease.Dispose();
        return ValueTask.CompletedTask;
    }

    private static async ValueTask AwaitPacketHandlerCompletionAsync(
        InlinePacketDispatcher owner, IConnection connection,
        IBufferLease lease, IPacket packet, ValueTask pending, CancellationToken ct)
    {
        try
        {
            await pending.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Async cancellation
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            connection.IncrementErrorCount();
            if (owner.Logging != null && owner.Logging.IsEnabled(LogLevel.Error))
            {
                connection.ThrottledError(
                    owner.Logging,
                    "dispatch.execute",
                    $"[RT.{nameof(InlinePacketDispatcher)}:{nameof(ExecutePacketAsync)}] handler-error ep={connection.NetworkEndpoint}");
            }
        }
        finally
        {
            // Guaranteed release for async path
            if (packet is IDisposable disposable)
            {
                disposable.Dispose();
            }

            lease.Dispose();
        }
    }

    /// <inheritdoc/>
    [StackTraceHidden]
    public string GenerateReport() => $"InlinePacketDispatcher: Running={(Volatile.Read(ref _running) == 1 ? "Yes" : "No")}";

    /// <inheritdoc/>
    public void WriteReportData(System.Text.Json.Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartObject();
        writer.WriteBoolean("Running", Volatile.Read(ref _running) == 1);
        writer.WriteEndObject();
    }
}
