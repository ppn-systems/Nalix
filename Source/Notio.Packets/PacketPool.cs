using System;
using System.Runtime.CompilerServices;
using Notio.Shared.Memory.Pool;

namespace Notio.Packets;

public static class PacketPool
{
    private static readonly ObjectPool _pool = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet Create(byte type, byte flags, short command, ReadOnlyMemory<byte> payload) =>
        new(type, flags, command, payload);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet Rent() => _pool.Get<Packet>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(Packet packet) => _pool.Return(packet);
}