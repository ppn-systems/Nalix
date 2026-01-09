// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Caching;
using Nalix.Common.Connection;
using Nalix.Common.Packets.Abstractions;
using Nalix.Network.Abstractions;

namespace Nalix.Network.Dispatch.Channel;

/// <inheritdoc/>
public sealed class DispatchRouter<TPacket> : IDispatchChannel<TPacket> where TPacket : IPacket
{
    private readonly System.Int32 _mask;
    private readonly DispatchChannel<TPacket>[] _shards;

    /// <inheritdoc/>
    public System.Int32 TotalPackets
    {
        get
        {
            System.Int32 total = 0;
            for (System.Int32 i = 0; i < _shards.Length; i++)
            {
                total += _shards[i].TotalPackets;
            }
            return total;
        }
    }

    /// <inheritdoc/>
    public DispatchRouter(System.Int32 shardCount)
    {
        shardCount = (System.Int32)System.Numerics.BitOperations.RoundUpToPowerOf2((System.UInt32)shardCount);

        _mask = shardCount - 1;
        _shards = new DispatchChannel<TPacket>[shardCount];

        for (System.Int32 i = 0; i < shardCount; i++)
        {
            _shards[i] = new DispatchChannel<TPacket>();
        }
    }

    /// <inheritdoc/>
    public void Push(
        [System.Diagnostics.CodeAnalysis.NotNull] IConnection connection,
        [System.Diagnostics.CodeAnalysis.NotNull] IBufferLease lease)
        => GET_SHARD(connection).Push(connection, lease);

    /// <inheritdoc/>
    public System.Boolean Pull(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IConnection connection,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IBufferLease lease)
    {
        // Simple round-robin or random over shards
        for (System.Int32 i = 0; i < _shards.Length; i++)
        {
            if (_shards[i].Pull(out connection, out lease))
            {
                return true;
            }
        }

        lease = default!;
        connection = default!;

        return false;
    }

    // Use stable per-connection id / endpoint hash
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private DispatchChannel<TPacket> GET_SHARD(IConnection connection) => _shards[connection.ID.GetHashCode() & _mask];
}

