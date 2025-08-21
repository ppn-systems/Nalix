// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Network.Throttling;

namespace Nalix.Network.Connection;

/// <summary>
/// Provides extension methods for the <see cref="IConnection"/> interface to support connection management operations.
/// </summary>
internal static class ConnectionExtensions
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
    public static void EnforceLimiterOnClose(
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
    public static System.Net.IPAddress ToIPAddress(this System.Net.EndPoint endPoint)
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
}