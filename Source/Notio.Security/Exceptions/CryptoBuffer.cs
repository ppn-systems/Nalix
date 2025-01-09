using System;
using System.Buffers;

namespace Notio.Security.Exceptions;

public sealed class CryptoBuffer(IMemoryOwner<byte> owner, int length) : IDisposable
{
    private readonly IMemoryOwner<byte> _owner = owner;
    private bool _disposed;

    public Memory<byte> Memory => _disposed
        ? throw new ObjectDisposedException(nameof(CryptoBuffer))
        : _owner.Memory;

    public int Length = length;

    public void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _owner.Memory.Span.Clear();
                _owner.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
