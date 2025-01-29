using Notio.Network;
using Notio.Network.Listeners;

namespace Notio.Application.Tcp;

public sealed class LoginListener(NetworkConfig networkConfig, LoginProtocol loginProtocol)
    : Listener(networkConfig, loginProtocol)
{
}