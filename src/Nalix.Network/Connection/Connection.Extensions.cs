using Nalix.Common.Connection;
using Nalix.Network.Security.Guard;
using System;
using System.Linq;
using System.Net;

namespace Nalix.Network.Connection;

internal static class ConnectionExtensions
{
    public static void EnforceLimiterOnClose(
        this IConnection connection,
        ConnectionLimiter limiter)
    {
        void callback(Object? _, IConnectEventArgs __)
        {
            _ = limiter.ConnectionClosed(connection.RemoteEndPoint.ToIPAddress()!);
            connection.OnCloseEvent -= callback;
        }

        connection.OnCloseEvent += callback;
    }

    public static IPAddress ToIPAddress(this EndPoint endPoint)
    {
        ArgumentNullException.ThrowIfNull(endPoint);

        return endPoint switch
        {
            IPEndPoint ipEndPoint => ipEndPoint.Address,
            DnsEndPoint dnsEndPoint => Dns.GetHostEntry(dnsEndPoint.Host).AddressList.FirstOrDefault()
                ?? throw new InvalidOperationException($"Unable to resolve IP address for host {dnsEndPoint.Host}"),
            _ => throw new ArgumentException($"EndPoint type not supported: {endPoint.GetType()}")
        };
    }
}