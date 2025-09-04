// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Protocols;
using Nalix.Network.Connection;
using Nalix.Network.Dispatch.Delegates;
using Nalix.Network.Dispatch.Results;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket>
{
    #region Private Methods

    private async System.Threading.Tasks.ValueTask ExecuteHandlerAsync(
        PacketHandler<TPacket> descriptor,
        PacketContext<TPacket> context)
    {
        if (this._pipeline is not null)
        {
            await this._pipeline.ExecuteAsync(context, Terminal)
                                .ConfigureAwait(false);
        }
        else
        {
            await Terminal().ConfigureAwait(false);
        }

        async System.Threading.Tasks.Task Terminal()
        {
            try
            {
                // Execute the handler and await the ValueTask once
                System.Object? result = await descriptor.ExecuteAsync(context)
                                                        .ConfigureAwait(false);

                // Handle the result
                IReturnHandler<TPacket> returnHandler = ReturnTypeHandlerFactory<TPacket>.GetHandler(descriptor.ReturnType);
                await returnHandler.HandleAsync(result, context)
                                   .ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
                await this.HandleExecutionExceptionAsync(descriptor, context, ex)
                          .ConfigureAwait(false);
            }
        }
    }

    private async System.Threading.Tasks.ValueTask HandleExecutionExceptionAsync(
        PacketHandler<TPacket> descriptor,
        PacketContext<TPacket> context,
        System.Exception exception)
    {
        this.Logger?.Error($"Handler execution failed for OpCode={descriptor.OpCode}: {exception.Message}");
        this._errorHandler?.Invoke(exception, descriptor.OpCode);

        var (reason, action, flags) = ClassifyException(exception);

        System.UInt32 sequenceId = 0;
        if (context.Packet is IPacketSequenced s)
        {
            sequenceId = s.SequenceId;
        }

        await context.Connection.SendAsync(
              controlType: ControlType.FAIL,
              reason: reason,
              action: action,
              sequenceId: sequenceId,
              flags: flags,
              arg0: descriptor.OpCode, arg1: 0, arg2: 0).ConfigureAwait(false);
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
        // Timeouts / cancellations -> transient, client can retry
        if (ex is System.OperationCanceledException or System.TimeoutException)
        {
            return (ProtocolCode.TIMEOUT, ProtocolAction.RETRY, ControlFlags.IS_TRANSIENT);
        }

        // Validation / bad input -> client should fix
        if (ex is System.ArgumentException || ex.GetType().Name.Contains("Validation", System.StringComparison.OrdinalIgnoreCase))
        {
            return (ProtocolCode.REQUEST_INVALID, ProtocolAction.FIX_AND_RETRY, ControlFlags.NONE);
        }

        // Unsupported features at runtime
        if (ex is System.NotSupportedException)
        {
            return (ProtocolCode.OPERATION_UNSUPPORTED, ProtocolAction.NONE, ControlFlags.NONE);
        }

        // I/O / network glitches -> transient
        if (ex is System.IO.IOException or System.Net.Sockets.SocketException)
        {
            return (ProtocolCode.NETWORK_ERROR, ProtocolAction.RETRY, ControlFlags.IS_TRANSIENT);
        }

        // Default: internal error (could be non-transient)
        return (ProtocolCode.INTERNAL_ERROR, ProtocolAction.NONE, ControlFlags.NONE);
    }

    #endregion Private Methods
}
