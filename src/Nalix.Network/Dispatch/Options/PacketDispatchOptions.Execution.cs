// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection.Protocols;
using Nalix.Network.Connection;
using Nalix.Network.Dispatch.Delegates;
using Nalix.Network.Dispatch.Results;
using System;
using System.IO;
using System.Net.Sockets;

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

        await context.Connection.SendAsync(
              controlType: ControlType.FAIL,
              reason: reason,
              action: action,
              flags: flags,
              arg0: descriptor.OpCode, arg1: 0, arg2: 0).ConfigureAwait(false);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static T EnsureNotNull<T>(T value, System.String paramName) where T : class
        => value ?? throw new System.ArgumentNullException(paramName);

    /// <summary>
    /// Map exception types to ReasonCode/SuggestedAction/ControlFlags.
    /// Adjust mappings to match your enum set.
    /// </summary>
    private static (ReasonCode reason, SuggestedAction action, ControlFlags flags) ClassifyException(Exception ex)
    {
        // Timeouts / cancellations -> transient, client can retry
        if (ex is OperationCanceledException or TimeoutException)
        {
            return (ReasonCode.TIMEOUT, SuggestedAction.RETRY, ControlFlags.IS_TRANSIENT);
        }

        // Validation / bad input -> client should fix
        if (ex is ArgumentException || ex.GetType().Name.Contains("Validation", StringComparison.OrdinalIgnoreCase))
        {
            return (ReasonCode.REQUEST_INVALID, SuggestedAction.FIX_AND_RETRY, ControlFlags.NONE);
        }

        // Unsupported features at runtime
        if (ex is NotSupportedException)
        {
            return (ReasonCode.OPERATION_UNSUPPORTED, SuggestedAction.NONE, ControlFlags.NONE);
        }

        // I/O / network glitches -> transient
        if (ex is IOException or SocketException)
        {
            return (ReasonCode.NETWORK_ERROR, SuggestedAction.RETRY, ControlFlags.IS_TRANSIENT);
        }

        // Default: internal error (could be non-transient)
        return (ReasonCode.INTERNAL_ERROR, SuggestedAction.NONE, ControlFlags.NONE);
    }

    #endregion Private Methods
}
