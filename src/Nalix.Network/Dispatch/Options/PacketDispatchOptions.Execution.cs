// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Common.Messaging.Protocols;
using Nalix.Network.Connections;
using Nalix.Network.Dispatch.Delegates;
using Nalix.Network.Dispatch.Results;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket>
{
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private async System.Threading.Tasks.ValueTask ExecuteHandlerAsync(PacketHandler<TPacket> descriptor, PacketContext<TPacket> context)
    {
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
                    System.UInt32 sequenceId1 = context.Packet is IPacketSequenced sequenced1
                        ? sequenced1.SequenceId
                        : 0;

                    await context.Connection.SendAsync(
                        controlType: ControlType.FAIL,
                        reason: ProtocolReason.RATE_LIMITED,
                        action: ProtocolAdvice.RETRY,
                        sequenceId: sequenceId1,
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
        this.Logger?.Error($"[{nameof(PacketDispatchOptions<>)}:{HandleDispatchExceptionAsync}] " +
                           $"handler-failed opcode={descriptor.OpCode}", exception);

        _errorHandler?.Invoke(exception, descriptor.OpCode);

        (ProtocolReason reason, ProtocolAdvice action, ControlFlags flags) = MapExceptionToProtocol(exception);

        System.UInt32 sequenceId2 = context.Packet is IPacketSequenced sequenced2
            ? sequenced2.SequenceId
            : 0;

        await context.Connection.SendAsync(
              controlType: ControlType.FAIL,
              reason: reason,
              action: action,
              sequenceId: sequenceId2,
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
    private static T ThrowIfNull<T>(T value, System.String param) where T : class => value ?? throw new System.ArgumentNullException(param);

    /// <summary>
    /// Map exception types to ProtocolCode/ProtocolAction/ControlFlags.
    /// Adjust mappings to match your enum set.
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
        if (ex is System.UnauthorizedAccessException or System.Security.SecurityException)
        {
            return (ProtocolReason.ACCOUNT_LOCKED, ProtocolAdvice.NONE, ControlFlags.NONE);
        }

        // 4) Unsupported / not implemented
        if (ex is System.NotSupportedException or System.NotImplementedException)
        {
            return (ProtocolReason.OPERATION_UNSUPPORTED, ProtocolAdvice.NONE, ControlFlags.NONE);
        }

        // 5) IEndpointKey /O / socket => phần lớn transient
        if (ex is System.IO.IOException ioEx && ioEx.InnerException is System.Net.Sockets.SocketException se1)
        {
            return MapSocketExceptionToProtocol(se1);
        }

        if (ex is System.Net.Sockets.SocketException se)
        {
            return MapSocketExceptionToProtocol(se);
        }

        // 6) ObjectDisposed trong giai đoạn teardown/shutdown: coi như transient nhẹ
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
                // thường do peer reset/close hay mạng chập chờn
                System.Net.Sockets.SocketError.ConnectionReset or
                System.Net.Sockets.SocketError.ConnectionAborted or
                System.Net.Sockets.SocketError.TimedOut or
                System.Net.Sockets.SocketError.HostDown or
                System.Net.Sockets.SocketError.HostUnreachable or
                System.Net.Sockets.SocketError.NetworkDown or
                System.Net.Sockets.SocketError.NetworkUnreachable
                => (ProtocolReason.NETWORK_ERROR, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),
                // local cancellation / interrupted
                System.Net.Sockets.SocketError.Interrupted or
                System.Net.Sockets.SocketError.OperationAborted
                => (ProtocolReason.NETWORK_ERROR, ProtocolAdvice.RETRY, ControlFlags.IS_TRANSIENT),
                // default
                _ => (ProtocolReason.NETWORK_ERROR, ProtocolAdvice.RETRY, ControlFlags.NONE),
            };
        }
    }
}
