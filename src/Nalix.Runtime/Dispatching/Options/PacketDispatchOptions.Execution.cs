// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Extensions;
using Nalix.Runtime.Internal.Compilation;
using Nalix.Runtime.Internal.Pooling;

namespace Nalix.Runtime.Routing;

public sealed partial class PacketDispatchOptions<TPacket>
{
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private async ValueTask ExecuteHandlerAsync(PacketHandler<TPacket> descriptor, PacketContext<TPacket> context)
    {
        // If the packet was deserialized into the wrong runtime type, fail early and
        // send a protocol-level response instead of letting the handler crash later.
        Type? expectedType = descriptor.ExpectedPacketType;
        if (expectedType is not null && !expectedType.IsInstanceOfType(context.Packet))
        {
            Type? actualType = context.Packet?.GetType();
            IPacket? packet = context.Packet;
            if (packet is null)
            {
                return;
            }

            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
            {
                string actualName = actualType?.Name ?? "null";
                DiagnosticsEvents.Write(
                    DiagnosticsEvents.Internal.Debug,
                    new DiagnosticLog(
                        "RT.PacketDispatchOptions:ExecuteHandlerAsync",
                        $"type-mismatch opcode=0x{descriptor.OpCode:X4} expected={expectedType.Name} actual={actualName}"));
            }

            await this.TrySendControlAsync(
                context,
                descriptor.OpCode,
                controlType: ControlType.FAIL,
                reason: ProtocolReason.REQUEST_INVALID,
                action: ProtocolAdvice.FIX_AND_RETRY,
                options: new ControlDirectiveOptions(
                    SequenceId: packet.Header.SequenceId,
                    Arg0: descriptor.OpCode)).ConfigureAwait(false);

            return;
        }

        // Industrial-grade validation: if the packet implements IPacketValidatable,
        // we must ensure it's structurally and logically sound before letting
        // application code touch it.
        if (context.Packet is IPacketValidatable validatable && !validatable.Validate(out string? failureReason))
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
            {
                DiagnosticsEvents.Write(
                    DiagnosticsEvents.Internal.Warning,
                    new DiagnosticLog(
                        "RT.PacketDispatchOptions:ExecuteHandlerAsync",
                        $"validation-failed opcode=0x{descriptor.OpCode} reason={failureReason} skipping-handler"));
            }

            await this.TrySendControlAsync(
                context,
                descriptor.OpCode,
                controlType: ControlType.FAIL,
                reason: ProtocolReason.MALFORMED_PACKET,
                action: ProtocolAdvice.FIX_AND_RETRY,
                options: new ControlDirectiveOptions(
                    SequenceId: context.Packet.Header.SequenceId,
                    Arg0: descriptor.OpCode)).ConfigureAwait(false);

            return;
        }

        // Void / Task / ValueTask handlers do not produce an outbound packet payload.
        context.SkipOutbound = HasNoOutboundResult(descriptor.ReturnType);

        if (!_pipeline.IsEmpty)
        {
            // The packet pipeline runs first so middleware can transform, validate, or short-circuit
            // the context before the actual handler executes.
            await _pipeline.ExecuteAsync(context, InvokeHandlerAsync, context.CancellationToken)
                           .ConfigureAwait(false);
        }
        else
        {
            await InvokeHandlerAsync(context.CancellationToken).ConfigureAwait(false);
        }

        async ValueTask InvokeHandlerAsync(CancellationToken ct = default)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                if (!descriptor.CanExecute(context))
                {
                    // Rate limiting is treated as a protocol failure with a transient retry hint.
                    await this.TrySendControlAsync(
                        context,
                        descriptor.OpCode,
                        controlType: ControlType.FAIL,
                        reason: ProtocolReason.RATE_LIMITED,
                        action: ProtocolAdvice.RETRY,
                        options: new ControlDirectiveOptions(
                            Flags: ControlFlags.IS_TRANSIENT,
                            SequenceId: context.Packet.Header.SequenceId,
                            Arg0: descriptor.OpCode)).ConfigureAwait(false);

                    return;
                }

                object? result = await AwaitHandlerResultAsync(descriptor.ExecuteAsync(context), ct).ConfigureAwait(false);

                if (!context.SkipOutbound && result is not null)
                {
                    if (result is IPacket packetResult)
                    {
                        await AwaitReturnAsync(context.Sender.SendAsync(packetResult, ct), ct).ConfigureAwait(false);
                    }
                    else if (result is ReadOnlyMemory<byte> rom)
                    {
                        await SendRawAsync(context, rom).ConfigureAwait(false);
                    }
                    else if (result is byte[] arr)
                    {
                        await SendRawAsync(context, arr).ConfigureAwait(false);
                    }
                    else if (result is Memory<byte> mem)
                    {
                        await SendRawAsync(context, mem).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                await this.HandleDispatchExceptionAsync(descriptor, context, ex)
                          .ConfigureAwait(false);
            }
        }

        static async ValueTask<object?> AwaitHandlerResultAsync(ValueTask<object?> pending, CancellationToken token)
        {
            // Fast path: if the handler already completed, avoid an allocation and return the result directly.
            token.ThrowIfCancellationRequested();

            if (pending.IsCompletedSuccessfully)
            {
                return pending.Result;
            }

            return await CancellableValueTaskSource<object?>.Await(pending, token)
                                                           .ConfigureAwait(false);
        }

        static async ValueTask AwaitReturnAsync(ValueTask pending, CancellationToken token)
        {
            // Same fast-path pattern as above, but for handlers that only need to emit side effects.
            token.ThrowIfCancellationRequested();

            if (pending.IsCompletedSuccessfully)
            {
#pragma warning disable CA1849 // Completed-success fast path; GetResult observes synchronous exceptions without blocking or allocating an async state machine.
                pending.GetAwaiter().GetResult();
#pragma warning restore CA1849
                return;
            }

            await CancellableValueTaskSource.Await(pending, token)
                                            .ConfigureAwait(false);
        }

        static async ValueTask SendRawAsync(PacketContext<TPacket> context, ReadOnlyMemory<byte> data)
        {
            if (context.IsReliable || context.Connection.UDP is null)
            {
                await context.Connection.TCP.SendAsync(data).ConfigureAwait(false);
            }
            else
            {
                await context.Connection.UDP.SendAsync(data).ConfigureAwait(false);
            }
        }
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private async ValueTask HandleDispatchExceptionAsync(
        PacketHandler<TPacket> descriptor,
        PacketContext<TPacket> context, Exception exception)
    {
        // Connection teardown is expected during shutdown or remote disconnect, so do not
        // spam logs or send protocol failures for those paths.
        bool teardownException = IsConnectionTeardownException(exception);
        if (teardownException)
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
            {
                DiagnosticsEvents.Write(
                    DiagnosticsEvents.Internal.Debug,
                    new DiagnosticLog(
                        "RT.PacketDispatchOptions:HandleDispatchExceptionAsync",
                        $"teardown-suppressed opcode={descriptor.OpCode} ex={exception.GetType().Name}",
                        exception));
            }
        }
        else
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
            {
                DiagnosticsEvents.Write(
                    DiagnosticsEvents.Internal.Error,
                    new DiagnosticLog(
                        "RT.PacketDispatchOptions:HandleDispatchExceptionAsync",
                        $"handler-failed opcode={descriptor.OpCode}",
                        exception));
            }
        }

        if (teardownException)
        {
            return;
        }

        // Custom error hooks run before the control reply so an application can record
        // the failure even if the outbound control message later cannot be delivered.
        _errorHandler?.Invoke(exception, descriptor.OpCode);

        (ProtocolReason reason, ProtocolAdvice action, ControlFlags flags) = MapExceptionToProtocol(exception);

        await this.TrySendControlAsync(
            context,
            descriptor.OpCode,
            controlType: ControlType.FAIL,
            reason: reason,
            action: action,
            options: new ControlDirectiveOptions(
                Flags: flags,
                SequenceId: context.Packet.Header.SequenceId,
                Arg0: descriptor.OpCode)).ConfigureAwait(false);
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool HasNoOutboundResult(Type returnType)
        => returnType == typeof(void)
        || returnType == typeof(Task)
        || returnType == typeof(ValueTask);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static T ThrowIfNull<T>(T value, string param) where T : class => value ?? throw new ArgumentNullException(param);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private async ValueTask TrySendControlAsync(
        PacketContext<TPacket> context,
        ushort opCode,
        ControlType controlType,
        ProtocolReason reason,
        ProtocolAdvice action,
        ControlDirectiveOptions options)
    {
        try
        {
            // Best-effort only: if the connection is already falling apart, the control message
            // is skipped and the original failure path is preserved.
            await context.Sender.SendAsync(
                controlType: controlType,
                reason: reason,
                action: action,
                options: options).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsConnectionTeardownException(ex))
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
            {
                DiagnosticsEvents.Write(
                    DiagnosticsEvents.Internal.Debug,
                    new DiagnosticLog(
                        "RT.PacketDispatchOptions:TrySendControlAsync",
                        $"control-send-skipped opcode={opCode} reason={reason} ex={ex.GetType().Name}",
                        ex));
            }
        }
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool IsConnectionTeardownException(Exception ex)
    {
        // Cancellation and object disposal are the normal shutdown signals.
        if (ex is OperationCanceledException or ObjectDisposedException)
        {
            return true;
        }

        if (ex is IOException io && io.InnerException is SocketException ioeSocket)
        {
            return IsTeardownSocketError(ioeSocket.SocketErrorCode);
        }

        if (ex is SocketException socketEx)
        {
            return IsTeardownSocketError(socketEx.SocketErrorCode);
        }

        Exception? inner = ex.InnerException;
        if (inner is null)
        {
            return false;
        }

        return IsConnectionTeardownException(inner);
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool IsTeardownSocketError(SocketError errorCode)
        => errorCode is SocketError.OperationAborted
        or SocketError.Interrupted
        or SocketError.ConnectionAborted
        or SocketError.ConnectionReset
        or SocketError.NotConnected
        or SocketError.Shutdown;




    /// <summary>
    /// Maps an exception to the protocol-level response that should be sent back to the peer.
    /// </summary>
    /// <param name="ex">The exception raised by handler execution or dispatch plumbing.</param>
    /// <exception cref="NotImplementedException"></exception>
    [Pure]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static (ProtocolReason reason, ProtocolAdvice action, ControlFlags flags) MapExceptionToProtocol(Exception ex)
    {
        // 1) Cancellation/Timeout => transient
        if (ex is OperationCanceledException or TimeoutException)
        {
            return (ProtocolReason.TIMEOUT, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT);
        }

        // 2) Validation/Bad input
        if (ex is ArgumentException or FormatException ||
            ex.GetType().Name.Contains("Validation", StringComparison.OrdinalIgnoreCase))
        {
            return (ProtocolReason.REQUEST_INVALID, ProtocolAdvice.FIX_AND_RETRY, ControlFlags.NONE);
        }

        // 3) Unauthorized / security
        if (ex is UnauthorizedAccessException)
        {
            return (ProtocolReason.UNAUTHORIZED, ProtocolAdvice.NONE, ControlFlags.NONE);
        }

        if (ex is CipherException)
        {
            return (ProtocolReason.DECRYPTION_FAILED, ProtocolAdvice.REAUTHENTICATE, ControlFlags.NONE);
        }

        // 4) Unsupported / not implemented
        if (ex is NotSupportedException or NotImplementedException)
        {
            return (ProtocolReason.OPERATION_UNSUPPORTED, ProtocolAdvice.NONE, ControlFlags.NONE);
        }

        // 5) IO / socket => mostly transient
        if (ex is IOException ioEx && ioEx.InnerException is SocketException se1)
        {
            return MapSocketExceptionToProtocol(se1);
        }

        if (ex is SocketException se)
        {
            return MapSocketExceptionToProtocol(se);
        }

        // 6) ObjectDisposed trong teardown: coi như transient nhẹ
        if (ex is ObjectDisposedException)
        {
            return (ProtocolReason.NETWORK_ERROR, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT);
        }

        // 7) Default: internal error
        return (ProtocolReason.INTERNAL_ERROR, ProtocolAdvice.NONE, ControlFlags.NONE);

        static (ProtocolReason, ProtocolAdvice, ControlFlags) MapSocketExceptionToProtocol(SocketException se)
        {
            return se.SocketErrorCode switch
            {
                // Timeout
                SocketError.TimedOut => (ProtocolReason.TIMEOUT, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),

                // Connection lifecycle
                SocketError.ConnectionReset => (ProtocolReason.CONNECTION_RESET, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),
                SocketError.ConnectionRefused => (ProtocolReason.CONNECTION_REFUSED, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),
                SocketError.ConnectionAborted => (ProtocolReason.REMOTE_CLOSED, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),
                SocketError.Shutdown => (ProtocolReason.LOCAL_CLOSED, ProtocolAdvice.NONE, ControlFlags.NONE),
                SocketError.NotConnected => (ProtocolReason.NETWORK_ERROR, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),

                // Network
                SocketError.NetworkDown or
                SocketError.NetworkUnreachable or
                SocketError.HostDown or
                SocketError.HostUnreachable => (ProtocolReason.NETWORK_ERROR, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),
                SocketError.NetworkReset => (ProtocolReason.CONNECTION_RESET, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),

                // DNS
                SocketError.HostNotFound or
                SocketError.TryAgain or
                SocketError.NoRecovery or
                SocketError.NoData => (ProtocolReason.DNS_FAILURE, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),

                //  Flow / buffer
                SocketError.NoBufferSpaceAvailable => (ProtocolReason.RESOURCE_LIMIT, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),
                SocketError.WouldBlock or
                SocketError.IOPending or
                SocketError.InProgress or
                SocketError.AlreadyInProgress => (ProtocolReason.TEMPORARY_FAILURE, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),

                // Size / framing
                SocketError.MessageSize => (ProtocolReason.MESSAGE_TOO_LARGE, ProtocolAdvice.FIX_AND_RETRY, ControlFlags.NONE),

                // Permission
                SocketError.AccessDenied => (ProtocolReason.FORBIDDEN, ProtocolAdvice.NONE, ControlFlags.NONE),

                // Address
                SocketError.AddressAlreadyInUse => (ProtocolReason.CONNECTION_LIMIT, ProtocolAdvice.RETRY, ControlFlags.NONE),
                SocketError.AddressNotAvailable => (ProtocolReason.REQUEST_INVALID, ProtocolAdvice.FIX_AND_RETRY, ControlFlags.NONE),

                // Programming / misuse
                SocketError.InvalidArgument or
                SocketError.NotSocket or
                SocketError.OperationNotSupported => (ProtocolReason.REQUEST_INVALID, ProtocolAdvice.FIX_AND_RETRY, ControlFlags.NONE),

                // System
                SocketError.SystemNotReady or
                SocketError.NotInitialized => (ProtocolReason.SERVICE_UNAVAILABLE, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),
                SocketError.VersionNotSupported => (ProtocolReason.VERSION_UNSUPPORTED, ProtocolAdvice.NONE, ControlFlags.NONE),
                SocketError.SocketError => (ProtocolReason.NETWORK_ERROR, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),

                SocketError.Success => (ProtocolReason.UNKNOWN, ProtocolAdvice.NONE, ControlFlags.NONE),

                SocketError.OperationAborted => (ProtocolReason.CANCELLED, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),
                SocketError.Interrupted => (ProtocolReason.CANCELLED, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),
                SocketError.Fault => (ProtocolReason.INTERNAL_ERROR, ProtocolAdvice.NONE, ControlFlags.NONE),
                SocketError.TooManyOpenSockets => (ProtocolReason.FD_LIMIT, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),
                SocketError.DestinationAddressRequired => (ProtocolReason.MISSING_REQUIRED_FIELD, ProtocolAdvice.FIX_AND_RETRY, ControlFlags.NONE),
                SocketError.ProtocolType => (ProtocolReason.PROTOCOL_ERROR, ProtocolAdvice.NONE, ControlFlags.NONE),
                SocketError.ProtocolOption => (ProtocolReason.PROTOCOL_ERROR, ProtocolAdvice.NONE, ControlFlags.NONE),
                SocketError.ProtocolNotSupported => (ProtocolReason.PROTOCOL_ERROR, ProtocolAdvice.NONE, ControlFlags.NONE),
                SocketError.SocketNotSupported => (ProtocolReason.OPERATION_UNSUPPORTED, ProtocolAdvice.NONE, ControlFlags.NONE),
                SocketError.ProtocolFamilyNotSupported => (ProtocolReason.PROTOCOL_ERROR, ProtocolAdvice.NONE, ControlFlags.NONE),
                SocketError.AddressFamilyNotSupported => (ProtocolReason.PROTOCOL_ERROR, ProtocolAdvice.NONE, ControlFlags.NONE),
                SocketError.IsConnected => (ProtocolReason.STATE_VIOLATION, ProtocolAdvice.FIX_AND_RETRY, ControlFlags.NONE),
                SocketError.ProcessLimit => (ProtocolReason.RESOURCE_LIMIT, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),
                SocketError.Disconnecting => (ProtocolReason.LOCAL_CLOSED, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),
                SocketError.TypeNotFound => (ProtocolReason.PROTOCOL_ERROR, ProtocolAdvice.NONE, ControlFlags.NONE),

                // fallback
                _ => (ProtocolReason.NETWORK_ERROR, ProtocolAdvice.RETRY, ControlFlags.NONE),
            };
        }
    }
}
