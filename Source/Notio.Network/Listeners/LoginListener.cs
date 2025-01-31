using Notio.Common.Logging;
using Notio.Common.Memory;
using Notio.Network.Protocols;

namespace Notio.Network.Listeners;

public sealed class LoginListener(LoginProtocol loginProtocol, IBufferPool bufferPool, ILogger? logger)
    : Listener(loginProtocol, bufferPool, logger)
{
}