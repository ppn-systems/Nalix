using System;
using System.Buffers;

namespace Notio.Cryptography;

public sealed class MemoryBuffer(IMemoryOwner<byte> owner, int length) : IDisposable
{
    private readonly IMemoryOwner<byte> _owner = owner;
    private bool _disposed;

    public Memory<byte> Memory => _disposed
        ? throw new ObjectDisposedException(nameof(MemoryBuffer))
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