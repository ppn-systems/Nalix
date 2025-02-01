using Notio.Common.Connection;
using Notio.Common.Logging.Interfaces;
using Notio.Common.Memory.Pools;
using System;
using System.Collections.Concurrent;

namespace Notio.Network.Connection;

public class ConnectionPool(IBufferPool bufferPool, ILogger logger) : IDisposable
{
    private readonly ConcurrentBag<IConnection> _pool = new();
    private readonly IBufferPool _bufferPool = bufferPool;
    private readonly ILogger _logger = logger;

    public void Dispose()
    {
        foreach (var connection in _pool)
        {
            connection.Dispose();
        }
        _pool.Clear();

        GC.SuppressFinalize(this);
    }
}