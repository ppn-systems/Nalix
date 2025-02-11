using Notio.Common.Logging;
using System;

namespace Notio.Network.Handlers;

/// <summary>
/// Ultra-high performance packet router with advanced DI integration and async support
/// </summary>
public sealed class PacketHandlerRouter<TObject>(ILogger? logger = null, IServiceProvider? serviceProvider = null) : IDisposable
{
    private readonly ILogger? _logger = logger;

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
