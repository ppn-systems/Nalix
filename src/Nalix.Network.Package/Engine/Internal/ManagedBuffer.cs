using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Network.Package.Engine.Internal;

/// <summary>
/// Managed buffer with ultra-optimized memory handling
/// </summary>
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct ManagedBuffer(ReadOnlyMemory<byte> memory, PoolHandle? handle) : IDisposable
{
    public readonly ReadOnlyMemory<byte> Memory = memory;
    public readonly PoolHandle? Handle = handle;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Handle?.Dispose();
    }
}

/// <summary>
/// Pool handle using direct memory addresses for maximum performance
/// </summary>
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal sealed class PoolHandle(nint address) : IDisposable
{
    public readonly nint Address = address;
    private int _disposed;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            MemoryVault.ReturnToPool(this);
        }
    }
}

/// <summary>
/// Tracked memory entry using weak references for automatic cleanup
/// </summary>
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct TrackedMemory(byte[] array, long lastAccessTime, WeakReference<byte[]> weakRef)
{
    public readonly byte[] Array = array;
    public readonly long LastAccessTime = lastAccessTime;
    public readonly WeakReference<byte[]> WeakRef = weakRef;
}