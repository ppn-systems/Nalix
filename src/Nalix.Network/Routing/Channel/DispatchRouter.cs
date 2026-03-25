// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Shared;

namespace Nalix.Network.Routing.Channel;

/// <inheritdoc/>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DispatchRouter<TPacket> : IDispatchChannel<TPacket> where TPacket : IPacket
{
    #region Fields

    private readonly int _mask;
    private readonly DispatchChannel<TPacket>[] _shards;

    private int _pullIndex;

    #endregion Fields

    #region Properties

    /// <inheritdoc/>
    public long TotalPackets
    {
        get
        {
            long total = 0;
            for (int i = 0; i < _shards.Length; i++)
            {
                total += _shards[i].TotalPackets;
            }
            return total;
        }
    }

    #endregion Properties

    #region Constructors

    /// <inheritdoc/>
    public DispatchRouter(int shardCount)
    {
        shardCount = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)shardCount);

        _mask = shardCount - 1;
        _shards = new DispatchChannel<TPacket>[shardCount];

        for (int i = 0; i < shardCount; i++)
        {
            _shards[i] = new DispatchChannel<TPacket>();
        }
    }

    #endregion Constructors

    #region APIs

    /// <inheritdoc/>
    public void Push(
        IConnection connection,
        IBufferLease raw)
        => GET_SHARD(connection).Push(connection, raw);

    /// <inheritdoc/>
    public bool Pull(
        [NotNullWhen(true)] out IConnection connection,
        [NotNullWhen(true)] out IBufferLease raw)
    {
        // Simple round-robin or random over shards
        int start = Interlocked.Increment(ref _pullIndex) & _mask;

        for (int i = 0; i < _shards.Length; i++)
        {
            if (_shards[(start + i) & _mask].Pull(out connection, out raw))
            {
                return true;
            }
        }

        raw = default;
        connection = default;

        return false;
    }

    #endregion APIs

    #region Private Methods

    /// <summary>
    /// Use stable per-connection id / endpoint hash
    /// </summary>
    /// <param name="connection"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DispatchChannel<TPacket> GET_SHARD(IConnection connection) => _shards[connection.ID.GetHashCode() & _mask];

    #endregion Private Methods
}
