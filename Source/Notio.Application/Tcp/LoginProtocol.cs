using Notio.Common.Connection;
using Notio.Network.Protocols;
using System;

namespace Notio.Application.Tcp;

public class LoginProtocol : Protocol
{
    public override void ProcessMessage(object sender, IConnectEventArgs connection)
    {
        throw new NotImplementedException();
    }
}