// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Network.Connections;
using Nalix.Network.Routing.Metadata;
using Nalix.Network.Routing.Results;

namespace Nalix.Network.Routing;

public sealed partial class PacketDispatchOptions<TPacket>
{
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private async ValueTask ExecuteHandlerAsync(PacketHandler<TPacket> descriptor, PacketContext<TPacket> context)
    {
        // ------------------------------------------------------------------
        // Concrete-type guard — fast path: only active for legacy-style handlers
        // whose first parameter is a concrete type (e.g. LoginPacket) rather
        // than the TPacket interface itself.
        //
        // Why this matters:
        //   When TPacket = IPacket (the common case in PacketDispatchChannel),
        //   the expression tree compiled by HandlerCompiler contains an
        //   Expression.Convert(packetExpr, concreteType).
        //   If the packet deserialized from the wire is *not* an instance of
        //   that concrete type, the expression tree throws InvalidCastException
        //   with a message that gives no hint about which handler or opCode
        //   caused the failure.
        //
        //   This check fires *before* the expression tree runs, logs an
        //   actionable warning, and sends a FAIL frame to the client — all
        //   in O(1) time via a single dictionary lookup that was already done
        //   in the caller (TryGetExpectedPacketType).
        //
        // Performance:
        //   TryGetExpectedPacketType is AggressiveInlining + a Dictionary
        //   lookup (one hash + one reference compare). The IsInstanceOfType
        //   call is also ~1ns for sealed/concrete types. Total overhead on the
        //   happy path (type matches) is negligible compared to async machinery.
        // ------------------------------------------------------------------
        if (this.TryGetExpectedPacketType(descriptor.OpCode, out Type expectedType)
            && !expectedType.IsInstanceOfType(context.Packet))
        {
            Type? actualType = context.Packet?.GetType();
            IPacket packet = context.Packet ?? throw new InternalErrorException("Packet context contains a null packet.");

            this.Logging?.Warn(
                $"[NW.{nameof(PacketDispatchOptions<>)}:{nameof(ExecuteHandlerAsync)}] " +
                $"type-mismatch opcode=0x{descriptor.OpCode:X4} " +
                $"expected={expectedType.Name} actual={actualType?.Name ?? "null"} — skipping handler");

            await context.Connection.SendAsync(
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

        async Task InvokeHandlerAsync(CancellationToken ct = default)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                if (!descriptor.CanExecute(context))
                {
                    await context.Connection.SendAsync(
                        controlType: ControlType.FAIL,
                        reason: ProtocolReason.RATE_LIMITED,
                        action: ProtocolAdvice.RETRY,
                        options: new ControlDirectiveOptions(Flags: ControlFlags.IS_TRANSIENT, SequenceId: context.Packet.SequenceId, Arg0: descriptor.OpCode)).ConfigureAwait(false);

                    return;
                }

                // Execute the handler and await the ValueTask once
                object result = await descriptor.ExecuteAsync(context)
                                                       .AsTask()
                                                       .WaitAsync(ct)
                                                       .ConfigureAwait(false);

                // Handle the result
                if (!context.SkipOutbound)
                {
                    IReturnHandler<TPacket> returnHandler = ReturnTypeHandlerFactory<TPacket>.ResolveHandler(descriptor.ReturnType);
                    await returnHandler.HandleAsync(result, context)
                                       .AsTask()
                                       .WaitAsync(ct)
                                       .ConfigureAwait(false);
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
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private async ValueTask HandleDispatchExceptionAsync(
        PacketHandler<TPacket> descriptor,
        PacketContext<TPacket> context, Exception exception)
    {
        this.Logging?.Error($"[{nameof(PacketDispatchOptions<>)}:{this.HandleDispatchExceptionAsync}] " +
                            $"handler-failed opcode={descriptor.OpCode}", exception);

        _errorHandler?.Invoke(exception, descriptor.OpCode);

        (ProtocolReason reason, ProtocolAdvice action, ControlFlags flags) = MapExceptionToProtocol(exception);

        await context.Connection.SendAsync(
              controlType: ControlType.FAIL,
              reason: reason,
              action: action,
              options: new ControlDirectiveOptions(Flags: flags, SequenceId: context.Packet.SequenceId, Arg0: descriptor.OpCode)).ConfigureAwait(false);
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


    /// <summary>
    /// Tries to retrieve the concrete packet type registered for the given opcode.
    /// Returns <see langword="null"/> for context-style handlers or when no mapping exists.
    /// </summary>
    /// <param name="opCode"></param>
    /// <param name="expectedType"></param>
    /// <remarks>
    /// Hot path — called once per dispatch. The dictionary lookup is O(1) with a small constant.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool TryGetExpectedPacketType(
        ushort opCode,
        [NotNullWhen(true)] out Type expectedType)
    {
        if (_packetTypeMap.TryGetValue(opCode, out Type? mappedType)
            && mappedType is not null)
        {
            expectedType = mappedType;
            return true;
        }

        expectedType = null!;
        return false;
    }

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
