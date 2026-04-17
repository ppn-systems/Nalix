// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;

namespace Nalix.Runtime.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="IConnection"/> interface to support connection management operations.
/// </summary>
public static class ConnectionExtensions
{
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    /// <summary>
    /// Sends a control directive asynchronously over the connection.
    /// </summary>
    /// <param name="connection">The connection to send the directive on.</param>
    /// <param name="controlType">The type of control message to send.</param>
    /// <param name="reason">The reason code associated with the control message.</param>
    /// <param name="action">The suggested action for the recipient.</param>
    /// <param name="options">Optional directive metadata and payload arguments.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static async Task SendAsync(this IConnection connection,
        ControlType controlType,
        ProtocolReason reason,
        ProtocolAdvice action,
        ControlDirectiveOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        Directive directive = s_pool.Get<Directive>();

        try
        {
            directive.Initialize(
                controlType, reason, action,
                sequenceId: options.SequenceId,
                flags: options.Flags,
                arg0: options.Arg0,
                arg1: options.Arg1,
                arg2: options.Arg2);

            using BufferLease lease = BufferLease.Rent(directive.Length + 32);

            int length = directive.Serialize(lease.SpanFull);
            lease.CommitLength(length);
            await connection.TCP.SendAsync(lease.Memory).ConfigureAwait(false);
        }
        finally
        {
            s_pool.Return(directive);
        }
    }
}


/// <summary>
/// Optional metadata and payload arguments for a control directive.
/// </summary>
/// <param name="Flags">Optional control flags to include with the message.</param>
/// <param name="SequenceId">
/// Correlation id to map this directive to a prior request (0 = server-initiated / no correlation).
/// </param>
/// <param name="Arg0">Optional argument 0 for the directive.</param>
/// <param name="Arg1">Optional argument 1 for the directive.</param>
/// <param name="Arg2">Optional argument 2 for the directive.</param>
public readonly record struct ControlDirectiveOptions(ControlFlags Flags = ControlFlags.NONE, ushort SequenceId = 0, uint Arg0 = 0, uint Arg1 = 0, ushort Arg2 = 0);
