// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Network.Connections;
using Nalix.Network.Internal.Routing;

namespace Nalix.Network.Routing;

public sealed partial class PacketDispatchOptions<TPacket>
{
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private async ValueTask ExecuteHandlerAsync(PacketHandler<TPacket> descriptor, PacketContext<TPacket> context)
    {
        Type? expectedType = descriptor.ExpectedPacketType;
        if (expectedType is not null && !expectedType.IsInstanceOfType(context.Packet))
        {
            Type? actualType = context.Packet?.GetType();
            IPacket? packet = context.Packet;
            if (packet is null)
            {
                return;
            }

            this.Logging?.Warn(
                $"[NW.{nameof(PacketDispatchOptions<>)}:{nameof(ExecuteHandlerAsync)}] " +
                $"type-mismatch opcode=0x{descriptor.OpCode:X4} " +
                $"expected={expectedType.Name} actual={actualType?.Name ?? "null"} — skipping handler");

            await this.TrySendControlAsync(
                context,
                descriptor.OpCode,
                controlType: ControlType.FAIL,
                reason: ProtocolReason.REQUEST_INVALID,
                action: ProtocolAdvice.FIX_AND_RETRY,
                options: new ControlDirectiveOptions(SequenceId: packet.SequenceId, Arg0: descriptor.OpCode)).ConfigureAwait(false);

            return;
        }

        context.SkipOutbound = HasNoOutboundResult(descriptor.ReturnType);

        if (!_pipeline.IsEmpty)
        {
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
                    await this.TrySendControlAsync(
                        context,
                        descriptor.OpCode,
                        controlType: ControlType.FAIL,
                        reason: ProtocolReason.RATE_LIMITED,
                        action: ProtocolAdvice.RETRY,
                        options: new ControlDirectiveOptions(
                            Flags: ControlFlags.IS_TRANSIENT,
                            SequenceId: context.Packet.SequenceId,
                            Arg0: descriptor.OpCode)).ConfigureAwait(false);

                    return;
                }

                object result = await AwaitHandlerResultAsync(descriptor.ExecuteAsync(context), ct).ConfigureAwait(false);

                if (!context.SkipOutbound)
                {
                    await AwaitReturnAsync(descriptor.ReturnHandler.HandleAsync(result, context), ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                await this.HandleDispatchExceptionAsync(descriptor, context, ex)
                          .ConfigureAwait(false);
            }
        }

        static async ValueTask<object> AwaitHandlerResultAsync(ValueTask<object> pending, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (pending.IsCompletedSuccessfully)
            {
                return pending.Result;
            }

            if (token.CanBeCanceled)
            {
                return await pending.AsTask().WaitAsync(token).ConfigureAwait(false);
            }

            return await pending.ConfigureAwait(false);
        }

        static async ValueTask AwaitReturnAsync(ValueTask pending, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (pending.IsCompletedSuccessfully)
            {
                pending.GetAwaiter().GetResult();
                return;
            }

            if (token.CanBeCanceled)
            {
                await pending.AsTask().WaitAsync(token).ConfigureAwait(false);
                return;
            }

            await pending.ConfigureAwait(false);
        }
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private async ValueTask HandleDispatchExceptionAsync(
        PacketHandler<TPacket> descriptor,
        PacketContext<TPacket> context, Exception exception)
    {
        bool teardownException = IsConnectionTeardownException(exception);
        if (teardownException)
        {
            if (this.Logging?.IsEnabled(LogLevel.Debug) == true)
            {
                this.Logging.Debug(
                    $"[{nameof(PacketDispatchOptions<>)}:{nameof(HandleDispatchExceptionAsync)}] " +
                    $"teardown-suppressed opcode={descriptor.OpCode} ex={exception.GetType().Name}");
            }
        }
        else
        {
            this.Logging?.Error($"[{nameof(PacketDispatchOptions<>)}:{nameof(HandleDispatchExceptionAsync)}] " +
                                $"handler-failed opcode={descriptor.OpCode}", exception);
        }

        if (teardownException)
        {
            return;
        }

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
                SequenceId: context.Packet.SequenceId,
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
            await context.Connection.SendAsync(
                controlType: controlType,
                reason: reason,
                action: action,
                options: options).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsConnectionTeardownException(ex))
        {
            if (this.Logging?.IsEnabled(LogLevel.Debug) == true)
            {
                this.Logging.Debug(
                    $"[{nameof(PacketDispatchOptions<>)}:{nameof(TrySendControlAsync)}] " +
                    $"control-send-skipped opcode={opCode} reason={reason} ex={ex.GetType().Name}");
            }
        }
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool IsConnectionTeardownException(Exception ex)
    {
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
    /// Inspects a handler <paramref name="method"/>'s parameter list and returns the
    /// concrete packet type it expects, or <see langword="null"/> for context-style methods.
    /// </summary>
    /// <param name="method"></param>
    /// <param name="contextType"></param>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Type? ResolveConcretePacketType(
        MethodInfo method,
        Type contextType)
    {
        ParameterInfo[] parms = method.GetParameters();

        if (parms.Length == 0)
        {
            return null;
        }

        Type firstParam = parms[0].ParameterType;

        // Context-style: (PacketContext<TPacket>[, CancellationToken])
        // The packet type is accessed through the context — no direct cast needed.
        if (firstParam == contextType)
        {
            return null;
        }

        // Legacy-style: (SomePacket, IConnection[, CancellationToken])
        // Return the concrete packet type (may equal TPacket if the handler uses the interface).
        return typeof(IPacket).IsAssignableFrom(firstParam) ? firstParam : null;
    }

    /// <summary>
    /// Map exception types to ProtocolCode/ProtocolAction/ControlFlags.
    /// </summary>
    /// <param name="ex"></param>
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
        if (ex is UnauthorizedAccessException or CipherException)
        {
            return (ProtocolReason.ACCOUNT_LOCKED, ProtocolAdvice.NONE, ControlFlags.NONE);
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
