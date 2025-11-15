// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Nalix.Common.Diagnostics;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames.Controls;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;

namespace Nalix.Network.Connections;

/// <summary>
/// Provides extension methods for the <see cref="IConnection"/> interface to support connection management operations.
/// </summary>
public static class ConnectionExtensions
{
    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

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
    [MethodImpl(MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    public static async Task SendAsync(
        this IConnection connection,
        ControlType controlType, ProtocolReason reason, ProtocolAdvice action,
        uint sequenceId = 0, ControlFlags flags = ControlFlags.NONE,
        uint arg0 = 0, uint arg1 = 0, ushort arg2 = 0)
    {
        ArgumentNullException.ThrowIfNull(connection);

        Directive directive = s_pool.Get<Directive>();

        try
        {
            directive.Initialize(controlType, reason, action, sequenceId: sequenceId, flags: flags, arg0: arg0, arg1: arg1, arg2: arg2);

            int len = directive.Length;

            if (len >= BufferLease.StackAllocThreshold)
            {
                using BufferLease lease = BufferLease.Rent(len + 32);

                try
                {
                    int length = directive.Serialize(lease.Span[..len]);
                    lease.CommitLength(length);
                    _ = await connection.TCP.SendAsync(lease.Memory)
                                            .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    s_logger?.Error($"[NW.{nameof(ConnectionExtensions)}:{nameof(SendAsync)}] directive-send-failed type={controlType} " +
                                    $"reason={reason} action={action} seq={sequenceId} msg={ex.Message}");
                }
            }
            else
            {
                try
                {
                    bool sent = await connection.TCP.SendAsync(directive
                                                              .Serialize())
                                                              .ConfigureAwait(false);
                    if (!sent)
                    {
                        s_logger?.Warn($"[NW.{nameof(ConnectionExtensions)}:{nameof(SendAsync)}] directive-send-failed (small-path) " +
                                       $"type={controlType} reason={reason} action={action} seq={sequenceId}");
                    }
                }
                catch (Exception ex)
                {
                    s_logger?.Error($"[NW.{nameof(ConnectionExtensions)}:{nameof(SendAsync)}] directive-send-failed (small-path) " +
                                    $"type={controlType} reason={reason} action={action} seq={sequenceId} msg={ex.Message}");
                }
            }
        }
        finally
        {
            s_pool.Return(directive);
        }
    }
}
