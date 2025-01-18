using Notio.Common.Memory;
using Notio.Network;
using Notio.Network.Listeners;

namespace Notio.Application.Main;

public sealed class LoginListener(
    LoginProtocol protocol, IBufferPool bufferAllocator, NetworkConfig network)
    : Listener(network.Port, protocol, bufferAllocator)
{
}