using Nalix.Common.Connection;
using Nalix.Network.Security.Guard;
using System;

namespace Nalix.Network.Connection;

internal static class ConnectionExtensions
{
    public static void EnforceLimiterOnClose(
        this IConnection connection,
        ConnectionLimiter limiter)
    {
        void callback(Object? _, IConnectEventArgs __)
        {
            limiter.ConnectionClosed(connection.RemoteEndPoint.ToString()!);
            connection.OnCloseEvent -= callback;
        }

        connection.OnCloseEvent += callback;
    }
}