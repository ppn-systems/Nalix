// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Networking.Abstractions;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.Injection;
using Nalix.Shared.Frames.Controls;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Memory.Pooling;

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
        System.ArgumentNullException.ThrowIfNull(connection);

        Directive directive = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                      .Get<Directive>();

        try
        {
            directive.Initialize(controlType, reason, action, sequenceId: sequenceId, flags: flags, arg0: arg0, arg1: arg1, arg2: arg2);

            System.Int32 len = directive.Length;

            if (len >= BufferLease.StackAllocThreshold)
            {
                using BufferLease lease = BufferLease.Rent(len + 32);

                try
                {
                    System.Int32 length = directive.Serialize(lease.Span[..len]);
                    lease.CommitLength(length);
                    _ = await connection.TCP.SendAsync(lease.Memory)
                                            .ConfigureAwait(false);
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[NW.{nameof(ConnectionExtensions)}:{nameof(SendAsync)}] directive-send-failed type={controlType} " +
                                                   $"reason={reason} action={action} seq={sequenceId} msg={ex.Message}");
                }
            }
            else
            {
                try
                {
                    System.Boolean sent = await connection.TCP.SendAsync(directive
                                                              .Serialize())
                                                              .ConfigureAwait(false);
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
                                    .Return(directive);
        }
    }
}
