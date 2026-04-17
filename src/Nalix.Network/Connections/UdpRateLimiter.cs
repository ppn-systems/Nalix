// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Time;
using Nalix.Network.Options;

namespace Nalix.Network.Connections;

/// <summary>
/// A fast, lock-based (Fixed Window) rate limiter specifically designed for UDP traffic.
/// This prevents DDOS floods at the very edge by enforcing a strict Max Packets-Per-Second per IP.
/// Designed to be registered as a Singleton in the DI container.
/// </summary>
public sealed class UdpRateLimiter : IUdpRateLimiter, IDisposable
{
    private sealed class Window
    {
        public long SecondOffset;
        public int Count;
    }

    private readonly int _maxPacketsPerSecond;
    private readonly ConcurrentDictionary<IPAddress, Window> _map;
    private readonly CancellationTokenSource _cts;
    private ILogger? _logger;
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="UdpRateLimiter"/> with the specified limit.
    /// If no limit is provided, it falls back to 1000 datagrams per second per IP.
    /// </summary>
    /// <param name="maxPacketsPerSecond">The max packet count allowed from a single IP within a 1-second window.</param>
    public UdpRateLimiter(int maxPacketsPerSecond = 1000)
    {
        _maxPacketsPerSecond = maxPacketsPerSecond;
        _map = new ConcurrentDictionary<IPAddress, Window>();
        _cts = new CancellationTokenSource();
        _logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

        // Start background cleanup loop
        _ = Task.Run(() => this.CleanupLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Associates a logger with this limiter.
    /// </summary>
    public UdpRateLimiter WithLogging(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        return this;
    }

    /// <inheritdoc/>
    public bool TryAccept(IPEndPoint endPoint)
    {
        if (Volatile.Read(ref _disposed) != 0 || endPoint == null)
            return false;

        // Use global ticketing (milliseconds / 1000) to get current second.
        long currentSecond = Environment.TickCount64 / 1000;
        
        // Use the IP address directly as the key
        IPAddress key = endPoint.Address;
        Window window = _map.GetOrAdd(key, static _ => new Window());

        lock (window)
        {
            if (window.SecondOffset != currentSecond)
            {
                // New distinct second window
                window.SecondOffset = currentSecond;
                window.Count = 1;
                return true;
            }

            if (window.Count >= _maxPacketsPerSecond)
            {
                return false;
            }

            window.Count++;
            return true;
        }
    }

    private async Task CleanupLoopAsync(CancellationToken token)
    {
        // Cleanup interval every 1 minute
        TimeSpan cleanupInterval = TimeSpan.FromMinutes(1);

        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(cleanupInterval, token).ConfigureAwait(false);

                long currentSecond = Environment.TickCount64 / 1000;
                int removed = 0;
                
                // Keep tracks of IPs to remove (do not mutate dictionary during iteration inside lock).
                List<IPAddress> toRemove = new List<IPAddress>();

                foreach (var kvp in _map)
                {
                    bool shouldRemove;
                    lock (kvp.Value)
                    {
                        // Remove if idle for more than 10 seconds.
                        shouldRemove = (currentSecond - kvp.Value.SecondOffset) > 10;
                    }

                    if (shouldRemove)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in toRemove)
                {
                    if (_map.TryRemove(key, out _))
                        removed++;
                }

                if (removed > 0)
                {
                    _logger?.Debug($"[NW.{nameof(UdpRateLimiter)}] Evicted {removed} idle UDP tracking windows. Count={_map.Count}");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (InvalidOperationException ex)
            {
                _logger?.Warn($"[NW.{nameof(UdpRateLimiter)}] Cleanup loop error.", ex);
            }
        }
    }

    /// <summary>
    /// Disposes the limiter and cancels the background cleanup loop.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _cts.Cancel();
            _cts.Dispose();
            _map.Clear();
        }
    }
}
