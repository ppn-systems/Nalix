// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Logging;
using Nalix.Common.Protocols;
using Nalix.Framework.Injection;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Messaging.Controls;

namespace Nalix.Network.Connections;

/// <summary>
/// Provides extension methods for the <see cref="IConnection"/> interface to support connection management operations.
/// </summary>
public static class ConnectionExtensions
{
    /// <summary>
    /// Sends a control directive asynchronously over the connection.
    /// </summary>
    /// <param name="connection">The connection to send the directive on.</param>
    /// <param name="controlType">The type of control message to send.</param>
    /// <param name="reason">The reason code associated with the control message.</param>
    /// <param name="action">The suggested action for the recipient.</param>
    /// <param name="sequenceId">
    /// Correlation id to map this directive to a prior request (0 = server-initiated / no correlation).
    /// </param>
    /// <param name="flags">Optional control flags to include with the message.</param>
    /// <param name="arg0">Optional argument 0 for the directive (default is 0).</param>
    /// <param name="arg1">Optional argument 1 for the directive (default is 0).</param>
    /// <param name="arg2">Optional argument 2 for the directive (default is 0).</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static async System.Threading.Tasks.Task SendAsync(
        this IConnection connection,
        ControlType controlType, ProtocolReason reason, ProtocolAdvice action,
        System.UInt32 sequenceId = 0, ControlFlags flags = ControlFlags.NONE,
        System.UInt32 arg0 = 0, System.UInt32 arg1 = 0, System.UInt16 arg2 = 0)
    {
        const System.Int32 STACK_THRESHOLD = 1024;
        System.ArgumentNullException.ThrowIfNull(connection);

        Directive pkt = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                .Get<Directive>();

        try
        {
            pkt.Initialize(
                controlType,
                reason,
                action,
                sequenceId: sequenceId,
                flags: flags,
                arg0: arg0,
                arg1: arg1,
                arg2: arg2);

            System.Int32 len = pkt.Length;

            if (len >= STACK_THRESHOLD)
            {
                System.Byte[] rented = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(len + 32);

                try
                {
                    System.Int32 length = pkt.Serialize(System.MemoryExtensions.AsSpan(rented, 0, len));
                    _ = await connection.TCP.SendAsync(System.MemoryExtensions.AsMemory(rented, 0, length)).ConfigureAwait(false);
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[NW.{nameof(ConnectionExtensions)}:{nameof(SendAsync)}] directive-send-failed type={controlType} " +
                                                   $"reason={reason} action={action} seq={sequenceId} msg={ex.Message}");
                }
                finally
                {
                    System.Buffers.ArrayPool<System.Byte>.Shared.Return(rented);
                }
            }
            else
            {
                try
                {
                    System.Boolean sent = await connection.TCP.SendAsync(pkt.Serialize()).ConfigureAwait(false);
                    if (!sent)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Warn($"[NW.{nameof(ConnectionExtensions)}:{nameof(SendAsync)}] directive-send-failed (small-path) " +
                                                      $"type={controlType} reason={reason} action={action} seq={sequenceId}");
                    }
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[NW.{nameof(ConnectionExtensions)}:{nameof(SendAsync)}] directive-send-failed (small-path) " +
                                                   $"type={controlType} reason={reason} action={action} seq={sequenceId} msg={ex.Message}");
                }
            }
        }
        finally
        {
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return(pkt);
        }
    }
}