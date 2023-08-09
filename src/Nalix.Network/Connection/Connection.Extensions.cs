using Nalix.Common.Connection;
using Nalix.Network.Throttling;

namespace Nalix.Network.Connection;

internal static class ConnectionExtensions
{
    public static void EnforceLimiterOnClose(
        this IConnection connection,
        ConnectionLimiter limiter)
    {
        void callback(System.Object? _, IConnectEventArgs __)
        {
            _ = limiter.ConnectionClosed(connection.RemoteEndPoint.ToIPAddress()!);
            connection.OnCloseEvent -= callback;
        }

        connection.OnCloseEvent += callback;
    }

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