using Notio.Common.Logging.Interfaces;
using Notio.Common.Memory.Pools;
using Notio.Network.Protocols;

namespace Notio.Network.Listeners;

public sealed class ServerListener(ServerProtocol loginProtocol, IBufferPool bufferPool, ILogger? logger)
    : Listener(loginProtocol, bufferPool, logger)
{
}