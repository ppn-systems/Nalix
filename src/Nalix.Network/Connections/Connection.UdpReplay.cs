// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Nalix.Network.Connections;

public sealed partial class Connection
{
    #region Constants

    private const int UdpReplaySoftLimit = 4_096;
    private const long UdpReplayCleanupIntervalMs = 5_000;

    #endregion Constants

    #region Fields

    private long _udpReplayLastCleanupMs;
    private readonly ConcurrentDictionary<ulong, long> _udpReplayNonces = new();

    #endregion Fields

    internal bool TryAcceptUdpNonce(ulong nonce, long timestamp, long maxReplayWindowMs)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long expiryCutoff = now - maxReplayWindowMs;

        if (!_udpReplayNonces.TryAdd(nonce, timestamp))
        {
            return false;
        }

        if (_udpReplayNonces.Count >= UdpReplaySoftLimit ||
            now - Interlocked.Read(ref _udpReplayLastCleanupMs) >= UdpReplayCleanupIntervalMs)
        {
            CLEANUP_UDP_REPLAY_NONCES(expiryCutoff, now);
        }

        return true;

        void CLEANUP_UDP_REPLAY_NONCES(long expiryCutoff, long now)
        {
            if (Interlocked.Exchange(ref _udpReplayLastCleanupMs, now) > now - UdpReplayCleanupIntervalMs)
            {
                return;
            }

            foreach (KeyValuePair<ulong, long> entry in _udpReplayNonces)
            {
                if (entry.Value < expiryCutoff)
                {
                    _ = _udpReplayNonces.TryRemove(entry.Key, out _);
                }
            }
        }
    }
}
