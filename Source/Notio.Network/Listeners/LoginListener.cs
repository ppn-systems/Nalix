using Notio.Common.IMemory;
using Notio.Network.Protocols;

namespace Notio.Network.Listeners;

public sealed class LoginListener(
    LoginProtocol protocol, IArrayPool bufferAllocator, NetworkConfig network) 
    : Listener(network.Port, protocol, bufferAllocator)
{
}