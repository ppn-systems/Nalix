﻿// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Attributes;
using Nalix.Common.Protocols;
using Nalix.Network.Connection;
using Nalix.Network.Dispatch.Delegates;
using Nalix.Network.Dispatch.Results;
using Nalix.Network.Throttling;
using static Nalix.Network.Throttling.ConcurrencyGate;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket>
{
    #region Private Methods

    private async System.Threading.Tasks.ValueTask ExecuteHandlerAsync(
        PacketHandler<TPacket> descriptor,
        PacketContext<TPacket> context)
    {
        context.SkipOutbound = IsVoidLike(descriptor.ReturnType);

        if (!_pipeline.IsEmpty)
        {
            await this._pipeline.ExecuteAsync(context, Terminal)
                                .ConfigureAwait(false);
        }
        else
        {
            await Terminal().ConfigureAwait(false);
        }

        async System.Threading.Tasks.Task Terminal(
              System.Threading.CancellationToken ct = default)
        {
            Lease lease = default;

            try
            {
                ct.ThrowIfCancellationRequested();

                if (!descriptor.CanExecute(context))
                {
                    await context.Connection.SendAsync(
                        controlType: ControlType.FAIL,
                        reason: ProtocolCode.RATE_LIMITED,
                        action: ProtocolAction.RETRY,
                        sequenceId: (context.Packet as IPacketSequenced)?.SequenceId ?? 0,
                        flags: ControlFlags.IS_TRANSIENT,
                        arg0: descriptor.OpCode,
                        arg1: 0,
                        arg2: 0).ConfigureAwait(false);

                    return;
                }

                System.Boolean acquired = false;
                PacketConcurrencyLimitAttribute? conc = descriptor.Attributes.ConcurrencyLimit;
                if (conc is not null)
                {
                    if (conc.Queue)
                    {
                        lease = await ConcurrencyGate.EnterAsync(descriptor.OpCode, conc, ct)
                                                     .ConfigureAwait(false);
                        acquired = true;
                    }
                    else
                    {
                        acquired = ConcurrencyGate.TryEnter(descriptor.OpCode, conc, out lease);

                        if (!acquired)
                        {
                            await context.Connection.SendAsync(
                                controlType: ControlType.FAIL,
                                reason: ProtocolCode.RATE_LIMITED,
                                action: ProtocolAction.RETRY,
                                sequenceId: (context.Packet as IPacketSequenced)?.SequenceId ?? 0,
                                flags: ControlFlags.IS_TRANSIENT,
                                arg0: descriptor.OpCode, arg1: 0, arg2: 0).ConfigureAwait(false);
                            return;
                        }
                    }
                }

                // Execute the handler and await the ValueTask once
                System.Object? result = await descriptor.ExecuteAsync(context)
                                                        .AsTask()
                                                        .WaitAsync(ct)
                                                        .ConfigureAwait(false);

                // Handle the result
                if (!context.SkipOutbound)
                {
                    IReturnHandler<TPacket> returnHandler = ReturnTypeHandlerFactory<TPacket>.GetHandler(descriptor.ReturnType);
                    await returnHandler.HandleAsync(result, context)
                                       .AsTask()
                                       .WaitAsync(ct)
                                       .ConfigureAwait(false);
                }
            }
            catch (ConcurrencyRejectedException)
            {
                await context.Connection.SendAsync(
                    controlType: ControlType.FAIL,
                    reason: ProtocolCode.RATE_LIMITED,
                    action: ProtocolAction.RETRY,
                    sequenceId: (context.Packet as IPacketSequenced)?.SequenceId ?? 0,
                    flags: ControlFlags.IS_TRANSIENT,
                    arg0: descriptor.OpCode, arg1: 0, arg2: 0).ConfigureAwait(false);
            }
            catch (System.OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (System.Exception ex)
            {
                await this.HandleExecutionExceptionAsync(descriptor, context, ex)
                          .ConfigureAwait(false);
            }
            finally
            {
                lease.Dispose();
            }
        }
    }

    private async System.Threading.Tasks.ValueTask HandleExecutionExceptionAsync(
        PacketHandler<TPacket> descriptor,
        PacketContext<TPacket> context,
        System.Exception exception)
    {
        this.Logger?.Error($"[{nameof(PacketDispatchOptions<TPacket>)}] handler-failed opcode={descriptor.OpCode}", exception);
        this._errorHandler?.Invoke(exception, descriptor.OpCode);

        var (reason, action, flags) = ClassifyException(exception);

        await context.Connection.SendAsync(
              controlType: ControlType.FAIL,
              reason: reason,
              action: action,
              sequenceId: (context.Packet as IPacketSequenced)?.SequenceId ?? 0,
              flags: flags,
              arg0: descriptor.OpCode, arg1: 0, arg2: 0).ConfigureAwait(false);
    }

    private static System.Boolean IsVoidLike(System.Type returnType)
    {
        return returnType == typeof(void)
            || returnType == typeof(System.Threading.Tasks.Task)
            || returnType == typeof(System.Threading.Tasks.ValueTask);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static T EnsureNotNull<T>(T value, System.String paramName) where T : class
        => value ?? throw new System.ArgumentNullException(paramName);

    /// <summary>
    /// Map exception types to ProtocolCode/ProtocolAction/ControlFlags.
    /// Adjust mappings to match your enum set.
    /// </summary>
    private static (ProtocolCode reason, ProtocolAction action, ControlFlags flags) ClassifyException(System.Exception ex)
    {
        // 1) Cancellation/Timeout => transient
        if (ex is System.OperationCanceledException or System.TimeoutException)
        {
            return (ProtocolCode.TIMEOUT, ProtocolAction.RETRY, ControlFlags.IS_TRANSIENT);
        }

        // 2) Validation/Bad input
        if (ex is System.ArgumentException or System.FormatException ||
            ex.GetType().Name.Contains("Validation", System.StringComparison.OrdinalIgnoreCase))
        {
            return (ProtocolCode.REQUEST_INVALID, ProtocolAction.FIX_AND_RETRY, ControlFlags.NONE);
        }

        // 3) Unauthorized / security
        if (ex is System.UnauthorizedAccessException or System.Security.SecurityException)
        {
            return (ProtocolCode.ACCOUNT_LOCKED, ProtocolAction.NONE, ControlFlags.NONE);
        }

        // 4) Unsupported / not implemented
        if (ex is System.NotSupportedException or System.NotImplementedException)
        {
            return (ProtocolCode.OPERATION_UNSUPPORTED, ProtocolAction.NONE, ControlFlags.NONE);
        }

        // 5) I/O / socket => phần lớn transient
        if (ex is System.IO.IOException ioEx && ioEx.InnerException is System.Net.Sockets.SocketException se1)
        {
            return ClassifySocket(se1);
        }

        if (ex is System.Net.Sockets.SocketException se)
        {
            return ClassifySocket(se);
        }

        // 6) ObjectDisposed trong giai đoạn teardown/shutdown: coi như transient nhẹ
        if (ex is System.ObjectDisposedException)
        {
            return (ProtocolCode.NETWORK_ERROR, ProtocolAction.RETRY, ControlFlags.IS_TRANSIENT);
        }

        // 7) Default: internal error
        return (ProtocolCode.INTERNAL_ERROR, ProtocolAction.NONE, ControlFlags.NONE);

        static (ProtocolCode, ProtocolAction, ControlFlags) ClassifySocket(System.Net.Sockets.SocketException se)
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
                => (ProtocolCode.NETWORK_ERROR, ProtocolAction.RETRY, ControlFlags.IS_TRANSIENT),
                // local cancellation / interrupted
                System.Net.Sockets.SocketError.Interrupted or
                System.Net.Sockets.SocketError.OperationAborted
                => (ProtocolCode.NETWORK_ERROR, ProtocolAction.RETRY, ControlFlags.IS_TRANSIENT),
                // default
                _ => (ProtocolCode.NETWORK_ERROR, ProtocolAction.RETRY, ControlFlags.NONE),
            };
        }
    }

    #endregion Private Methods
}
