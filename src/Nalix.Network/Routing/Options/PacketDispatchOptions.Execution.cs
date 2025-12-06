// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Network.Connections;
using Nalix.Network.Routing.Metadata;
using Nalix.Network.Routing.Results;

namespace Nalix.Network.Routing.Options;

public sealed partial class PacketDispatchOptions<TPacket>
{
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private async System.Threading.Tasks.ValueTask ExecuteHandlerAsync(PacketHandler<TPacket> descriptor, PacketContext<TPacket> context)
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
        if (TryGetExpectedPacketType(descriptor.OpCode, out System.Type expectedType)
            && !expectedType.IsInstanceOfType(context.Packet))
        {
            System.Type actualType = context.Packet?.GetType();

            this.Logging?.Warn(
                $"[NW.{nameof(PacketDispatchOptions<>)}:{nameof(ExecuteHandlerAsync)}] " +
                $"type-mismatch opcode=0x{descriptor.OpCode:X4} " +
                $"expected={expectedType.Name} actual={actualType?.Name ?? "null"} — skipping handler");

            await context.Connection.SendAsync(
                controlType: ControlType.FAIL,
                reason: ProtocolReason.REQUEST_INVALID,
                action: ProtocolAdvice.FIX_AND_RETRY,
                sequenceId: context.Packet.SequenceId,
                flags: ControlFlags.NONE,
                arg0: descriptor.OpCode, arg1: 0, arg2: 0).ConfigureAwait(false);

            return;
        }

        context.SkipOutbound = HasNoOutboundResult(descriptor.ReturnType);

        if (!_pipeline.IsEmpty)
        {
            await this._pipeline.ExecuteAsync(context, InvokeHandlerAsync, context.CancellationToken)
                                .ConfigureAwait(false);
        }
        else
        {
            await InvokeHandlerAsync(context.CancellationToken).ConfigureAwait(false);
        }

        async System.Threading.Tasks.Task InvokeHandlerAsync(System.Threading.CancellationToken ct = default)
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
                        sequenceId: context.Packet.SequenceId,
                        flags: ControlFlags.IS_TRANSIENT,
                        arg0: descriptor.OpCode, arg1: 0, arg2: 0).ConfigureAwait(false);

                    return;
                }

                // Execute the handler and await the ValueTask once
                System.Object result = await descriptor.ExecuteAsync(context)
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
            catch (System.OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (System.Exception ex)
            {
                await this.HandleDispatchExceptionAsync(descriptor, context, ex)
                          .ConfigureAwait(false);
            }
        }
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private async System.Threading.Tasks.ValueTask HandleDispatchExceptionAsync(
        PacketHandler<TPacket> descriptor,
        PacketContext<TPacket> context, System.Exception exception)
    {
        this.Logging?.Error($"[{nameof(PacketDispatchOptions<>)}:{HandleDispatchExceptionAsync}] " +
                            $"handler-failed opcode={descriptor.OpCode}", exception);

        _errorHandler?.Invoke(exception, descriptor.OpCode);

        (ProtocolReason reason, ProtocolAdvice action, ControlFlags flags) = MapExceptionToProtocol(exception);

        await context.Connection.SendAsync(
              controlType: ControlType.FAIL,
              reason: reason,
              action: action,
              sequenceId: context.Packet.SequenceId,
              flags: flags,
              arg0: descriptor.OpCode, arg1: 0, arg2: 0).ConfigureAwait(false);
    }

    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Boolean HasNoOutboundResult(System.Type returnType)
        => returnType == typeof(void)
        || returnType == typeof(System.Threading.Tasks.Task)
        || returnType == typeof(System.Threading.Tasks.ValueTask);

    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static T ThrowIfNull<T>(T value, System.String param) where T : class
        => value ?? throw new System.ArgumentNullException(param);


    /// <summary>
    /// Tries to retrieve the concrete packet type registered for the given opcode.
    /// Returns <see langword="null"/> for context-style handlers or when no mapping exists.
    /// </summary>
    /// <remarks>
    /// Hot path — called once per dispatch. The dictionary lookup is O(1) with a small constant.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private System.Boolean TryGetExpectedPacketType(
        System.UInt16 opCode,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Type expectedType)
        => _packetTypeMap.TryGetValue(opCode, out expectedType) && expectedType is not null;

    /// <summary>
    /// Inspects a handler <paramref name="method"/>'s parameter list and returns the
    /// concrete packet type it expects, or <see langword="null"/> for context-style methods.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Type ResolveConcretePacketType(
        System.Reflection.MethodInfo method,
        System.Type contextType)
    {
        System.Reflection.ParameterInfo[] parms = method.GetParameters();

        if (parms.Length == 0)
        {
            return null;
        }

        System.Type firstParam = parms[0].ParameterType;

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
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static (ProtocolReason reason, ProtocolAdvice action, ControlFlags flags) MapExceptionToProtocol(System.Exception ex)
    {
        // 1) Cancellation/Timeout => transient
        if (ex is System.OperationCanceledException or System.TimeoutException)
        {
            return (ProtocolReason.TIMEOUT, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT);
        }

        // 2) Validation/Bad input
        if (ex is System.ArgumentException or System.FormatException ||
            ex.GetType().Name.Contains("Validation", System.StringComparison.OrdinalIgnoreCase))
        {
            return (ProtocolReason.REQUEST_INVALID, ProtocolAdvice.FIX_AND_RETRY, ControlFlags.NONE);
        }

        // 3) Unauthorized / security
        if (ex is System.UnauthorizedAccessException or CryptographyException)
        {
            return (ProtocolReason.ACCOUNT_LOCKED, ProtocolAdvice.NONE, ControlFlags.NONE);
        }

        // 4) Unsupported / not implemented
        if (ex is System.NotSupportedException or System.NotImplementedException)
        {
            return (ProtocolReason.OPERATION_UNSUPPORTED, ProtocolAdvice.NONE, ControlFlags.NONE);
        }

        // 5) IO / socket => mostly transient
        if (ex is System.IO.IOException ioEx && ioEx.InnerException is System.Net.Sockets.SocketException se1)
        {
            return MapSocketExceptionToProtocol(se1);
        }

        if (ex is System.Net.Sockets.SocketException se)
        {
            return MapSocketExceptionToProtocol(se);
        }

        // 6) ObjectDisposed trong teardown: coi như transient nhẹ
        if (ex is System.ObjectDisposedException)
        {
            return (ProtocolReason.NETWORK_ERROR, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT);
        }

        // 7) Default: internal error
        return (ProtocolReason.INTERNAL_ERROR, ProtocolAdvice.NONE, ControlFlags.NONE);

        static (ProtocolReason, ProtocolAdvice, ControlFlags) MapSocketExceptionToProtocol(System.Net.Sockets.SocketException se)
        {
            return se.SocketErrorCode switch
            {
                System.Net.Sockets.SocketError.TimedOut
                => (ProtocolReason.TIMEOUT, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),

                System.Net.Sockets.SocketError.ConnectionReset or
                System.Net.Sockets.SocketError.ConnectionAborted or
                System.Net.Sockets.SocketError.HostDown or
                System.Net.Sockets.SocketError.HostUnreachable or
                System.Net.Sockets.SocketError.NetworkDown or
                System.Net.Sockets.SocketError.NetworkUnreachable
                => (ProtocolReason.NETWORK_ERROR, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),

                System.Net.Sockets.SocketError.Interrupted or
                System.Net.Sockets.SocketError.OperationAborted
                => (ProtocolReason.NETWORK_ERROR, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),

                _ => (ProtocolReason.NETWORK_ERROR, ProtocolAdvice.RETRY, ControlFlags.NONE),
            };
        }
    }
}