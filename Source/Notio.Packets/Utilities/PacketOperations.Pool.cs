using Notio.Shared.Memory.Pool;
using System;
using System.Runtime.CompilerServices;

namespace Notio.Packets.Utilities;

/// <summary>
/// A static class to manage a pool of reusable Packet objects.
/// </summary>
public static partial class PacketOperations
{
    // Private object pool for managing Packet instances.
    private static readonly ObjectPool _pool = new();

    /// <summary>
    /// Creates a new Packet object with the specified properties.
    /// </summary>
    /// <param name="type">The type of the packet.</param>
    /// <param name="flags">Flags associated with the packet.</param>
    /// <param name="command">The command identifier for the packet.</param>
    /// <param name="payload">The payload data of the packet.</param>
    /// <returns>A new instance of <see cref="Packet"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet Create(byte type, byte flags, byte priority, short command, ReadOnlyMemory<byte> payload)
        => new(type, flags, priority, command, payload);

    /// <summary>
    /// Retrieves a Packet instance from the pool.
    /// </summary>
    /// <returns>An instance of <see cref="Packet"/> from the pool.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet Rent() => _pool.Get<Packet>();

    /// <summary>
    /// Returns a Packet instance back to the pool for reuse.
    /// </summary>
    /// <param name="packet">The <see cref="Packet"/> instance to be returned to the pool.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(Packet packet) => _pool.Return(packet);
}