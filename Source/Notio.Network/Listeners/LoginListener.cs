using Notio.Common.Memory;
using Notio.Network.Protocols;

namespace Notio.Network.Listeners;

public sealed class LoginListener(
    LoginProtocol protocol, IBufferPool bufferAllocator, NetworkConfig network)
    : Listener(network.Port, protocol, bufferAllocator)
{
}