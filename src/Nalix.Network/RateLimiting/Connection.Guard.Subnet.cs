// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Network.RateLimiting;

public sealed partial class ConnectionGuard
{
    private readonly ConcurrentDictionary<uint, SubnetLimitEntry> _subnetMapV4 = new();
    private readonly ConcurrentDictionary<long, SubnetLimitEntry> _subnetMapV6 = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint EXTRACT_IPV4_SUBNET_KEY(IPAddress address)
    {
        // /24 -> shift by 8 bits
#pragma warning disable CS0618
        uint ip = (uint)address.Address;
#pragma warning restore CS0618
        // Need to be careful with endianness. The address.Address is usually in network byte order.
        // It's just a grouping key, so any stable 24-bit extraction is fine.
        Span<byte> bytes = stackalloc byte[4];
        _ = address.TryWriteBytes(bytes, out _);
        return (uint)((bytes[0] << 16) | (bytes[1] << 8) | bytes[2]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long EXTRACT_IPV6_SUBNET_KEY(IPAddress address)
    {
        // /48 -> top 48 bits
        Span<byte> bytes = stackalloc byte[16];
        _ = address.TryWriteBytes(bytes, out _);
        long key = 0;
        for (int i = 0; i < 6; i++)
        {
            key = (key << 8) | bytes[i];
        }
        return key;
    }

    private SubnetAllowResult TRY_ACQUIRE_SUBNET_SLOT(IPAddress address, long nowTicks)
    {
        int maxConnections = _config.MaxConnectionsPerSubnet;
        int maxAttempts = _config.MaxSubnetConnectionsPerWindow;

        bool isIpv4 = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;

        while (true)
        {
            SubnetLimitEntry entry;
            if (isIpv4)
            {
                uint key = EXTRACT_IPV4_SUBNET_KEY(address);
                entry = _subnetMapV4.GetOrAdd(key, static _ => new SubnetLimitEntry());
            }
            else
            {
                long key = EXTRACT_IPV6_SUBNET_KEY(address);
                entry = _subnetMapV6.GetOrAdd(key, static _ => new SubnetLimitEntry());
            }

            _ = Interlocked.Exchange(ref entry.LastSeenAtTicks, nowTicks);

            SubnetAllowResult result;
            bool spinLockTaken = false;
            try
            {
                entry.SpinLock.Enter(ref spinLockTaken);

                if (entry.IsRemoved)
                {
                    continue;
                }

                this.TRIM_OLD_TIMESTAMPS(entry.RecentConnectionTimestamps, nowTicks);

                if (entry.RecentConnectionTimestamps.Count >= maxAttempts)
                {
                    result = new SubnetAllowResult { Allowed = false };
                }
                else if (entry.CurrentConnections >= maxConnections)
                {
                    result = new SubnetAllowResult { Allowed = false };
                }
                else
                {
                    entry.CurrentConnections++;
                    entry.RecentConnectionTimestamps.Enqueue(nowTicks);
                    result = new SubnetAllowResult { Allowed = true };
                }
            }
            finally
            {
                if (spinLockTaken)
                {
                    entry.SpinLock.Exit();
                }
            }

            return result;
        }
    }

    private void TRY_RELEASE_SUBNET_SLOT(IPAddress address, DateTime now)
    {
        bool isIpv4 = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        SubnetLimitEntry? entry;
        if (isIpv4)
        {
            uint key = EXTRACT_IPV4_SUBNET_KEY(address);
            _ = _subnetMapV4.TryGetValue(key, out entry);
        }
        else
        {
            long key = EXTRACT_IPV6_SUBNET_KEY(address);
            _ = _subnetMapV6.TryGetValue(key, out entry);
        }

        if (entry is null)
        {
            return;
        }

        bool lockTaken = false;
        try
        {
            entry.SpinLock.Enter(ref lockTaken);
            if (entry.IsRemoved)
            {
                return;
            }

            entry.CurrentConnections = Math.Max(0, entry.CurrentConnections - 1);

            if (entry.CurrentConnections == 0 && entry.RecentConnectionTimestamps.Count > _config.MaxSubnetConnectionsPerWindow * 2)
            {
                entry.RecentConnectionTimestamps.Clear();
            }

            this.TRIM_OLD_TIMESTAMPS(entry.RecentConnectionTimestamps, now.Ticks);
        }
        finally
        {
            if (lockTaken)
            {
                entry.SpinLock.Exit();
            }
        }
    }
}
