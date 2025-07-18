using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Network.Package.Engine.Internal;

/// <summary>
/// Managed buffer with ultra-optimized memory handling
/// </summary>
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct ManagedBuffer(ReadOnlyMemory<Byte> memory, PoolHandle? handle) : IDisposable
{
    public readonly ReadOnlyMemory<Byte> Memory = memory;
    public readonly PoolHandle? Handle = handle;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Handle?.Dispose();
}

/// <summary>
/// Pool handle using direct memory addresses for maximum performance
/// </summary>
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal sealed class PoolHandle(IntPtr address) : IDisposable
{
    public readonly IntPtr Address = address;
    private Int32 _disposed;

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
internal readonly struct TrackedMemory(Byte[] array, Int64 lastAccessTime, WeakReference<Byte[]> weakRef)
{
    public readonly Byte[] Array = array;
    public readonly Int64 LastAccessTime = lastAccessTime;
    public readonly WeakReference<Byte[]> WeakRef = weakRef;
}