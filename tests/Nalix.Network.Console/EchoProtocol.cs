using Nalix.Common.Connection;
using Nalix.Common.Infrastructure.Connection;
using Nalix.Framework.Injection;
using Nalix.Network.Connections;
using Nalix.Network.Protocols;
using System;
using System.Text;
using System.Threading;

public class EchoProtocol : Protocol
{
    public EchoProtocol()
    {
        KeepConnectionOpen = true;
        IsAccepting = true;
    }

    public override void OnAccept(IConnection connection, CancellationToken cancellationToken = default)
    {
        base.OnAccept(connection, cancellationToken);
        InstanceManager.Instance.GetOrCreateInstance<ConnectionHub>()
                                .RegisterConnection(connection);
    }


    /// <summary>
    /// Dùng để xử lý message nhận được từ client.
    /// </summary>
    public override void ProcessMessage(Object sender, IConnectEventArgs args)
    {
        try
        {
            Console.WriteLine($"[Server] Received message from client {args.Connection.EndPoint}");

            // Echo lại dữ liệu
            String message = Encoding.UTF8.GetString(args.Connection.IncomingPacket.Span);
            Console.WriteLine($"[Server] Message: {message}");

            String response = $"Server Received: {message}";
            Byte[] responseData = Encoding.UTF8.GetBytes(response);
            args.Connection.TCP.Send(responseData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in {nameof(ProcessMessage)}: {ex.Message}");
        }
    }
}