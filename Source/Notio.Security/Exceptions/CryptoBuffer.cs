using System;
using System.Buffers;

namespace Notio.Security.Exceptions;

public readonly struct CryptoBuffer : IDisposable
{
    private readonly IMemoryOwner<byte> _memoryOwner;
    public readonly Memory<byte> Data { get; }

    internal CryptoBuffer(IMemoryOwner<byte> owner, int length)
    {
        _memoryOwner = owner;
        Data = owner.Memory[..length];
    }

    public void Dispose() => _memoryOwner?.Dispose();
}
