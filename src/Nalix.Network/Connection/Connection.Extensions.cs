// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Protocols;
using Nalix.Network.Throttling;
using Nalix.Shared.Injection;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Messaging.Controls;

namespace Nalix.Network.Connection;

/// <summary>
/// Provides extension methods for the <see cref="IConnection"/> interface to support connection management operations.
/// </summary>
public static class ConnectionExtensions
{
    /// <summary>
    /// Registers a callback to enforce a connection limiter when the specified connection is closed.
    /// The callback is executed with aggressive inlining for performance optimization.
    /// </summary>
    /// <param name="connection">The connection to monitor for closure.</param>
    /// <param name="limiter">The connection limiter to notify when the connection is closed.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="connection"/> or <paramref name="limiter"/> is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static void EnforceLimiterOnClose(
        this IConnection connection,
        ConnectionLimiter limiter)
    {
        // Handles the connection close event by notifying the limiter and unregistering the callback.
        // <param name="_">The sender of the event (ignored).</param>
        // <param name="__">The event arguments (ignored).</param>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        void Callback(System.Object? _, IConnectEventArgs __)
        {
            _ = limiter.ConnectionClosed(connection.RemoteEndPoint.ToIPAddress()!);
            connection.OnCloseEvent -= Callback;
        }

        connection.OnCloseEvent += Callback;
    }

    /// <summary>
    /// Converts an <see cref="System.Net.EndPoint"/> to an <see cref="System.Net.IPAddress"/>.
    /// </summary>
    /// <param name="endPoint">The endpoint to convert.</param>
    /// <returns>The <see cref="System.Net.IPAddress"/> corresponding to the provided endpoint.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="endPoint"/> is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown if the <paramref name="endPoint"/> type is not supported.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown if the IP address 
    /// cannot be resolved for a <see cref="System.Net.DnsEndPoint"/>.</exception>
    internal static System.Net.IPAddress ToIPAddress(this System.Net.EndPoint endPoint)
    {
        System.ArgumentNullException.ThrowIfNull(endPoint);

        return endPoint switch
        {
            System.Net.IPEndPoint ipEndPoint => ipEndPoint.Address,
            System.Net.DnsEndPoint dnsEndPoint => System.Linq.Enumerable
                .FirstOrDefault(System.Net.Dns.GetHostEntry(dnsEndPoint.Host).AddressList)
                ?? throw new System.InvalidOperationException($"Unable to resolve IP address for host {dnsEndPoint.Host}"),
            _ => throw new System.ArgumentException($"EndPoint type not supported: {endPoint.GetType()}")
        };
    }


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
    public static async System.Threading.Tasks.Task SendAsync(
        this IConnection connection,
        ControlType controlType,
        ReasonCode reason,
        SuggestedAction action,
        System.UInt32 sequenceId = 0,
        ControlFlags flags = ControlFlags.NONE,
        System.UInt32 arg0 = 0,
        System.UInt32 arg1 = 0,
        System.UInt16 arg2 = 0)
    {
        System.ArgumentNullException.ThrowIfNull(connection);

        ObjectPoolManager pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();
        Directive pkt = pool.Get<Directive>();

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
            System.Byte[] rented = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(len);
            try
            {
                pkt.Serialize(System.MemoryExtensions.AsSpan(rented, 0, len));
                _ = await connection.Tcp.SendAsync(System.MemoryExtensions.AsMemory(rented, 0, len)).ConfigureAwait(false);
            }
            finally
            {
                System.Buffers.ArrayPool<System.Byte>.Shared.Return(rented);
            }
        }
        finally
        {
            pool.Return(pkt);
        }
    }

    /// <summary>
    /// Sends a control directive asynchronously over the connection (no correlation).
    /// </summary>
    public static System.Threading.Tasks.Task SendAsync(
        this IConnection connection,
        ControlType controlType,
        ReasonCode reason,
        SuggestedAction action,
        ControlFlags flags = ControlFlags.NONE,
        System.UInt32 arg0 = 0,
        System.UInt32 arg1 = 0,
        System.UInt16 arg2 = 0)
        => SendAsync(
            connection,
            controlType,
            reason,
            action,
            sequenceId: 0,
            flags: flags,
            arg0: arg0,
            arg1: arg1,
            arg2: arg2);

    /// <summary>
    /// Sends a control directive echoing the SequenceId from a sequenced request.
    /// </summary>
    public static System.Threading.Tasks.Task SendAsync(
        this IConnection connection,
        ControlType controlType,
        ReasonCode reason,
        SuggestedAction action,
        IPacketSequenced request,
        ControlFlags flags = ControlFlags.NONE,
        System.UInt32 arg0 = 0,
        System.UInt32 arg1 = 0,
        System.UInt16 arg2 = 0)
    {
        System.ArgumentNullException.ThrowIfNull(request);
        return SendAsync(
            connection,
            controlType,
            reason,
            action,
            sequenceId: request.SequenceId,
            flags: flags,
            arg0: arg0,
            arg1: arg1,
            arg2: arg2);
    }

}