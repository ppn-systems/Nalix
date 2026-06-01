// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;

namespace Nalix.Runtime.Throttling;

public sealed partial class ConcurrencyGate
{
    #region Private Methods

    /// <summary>
    /// Checks if circuit breaker is open and attempts to close if timeout expired.
    /// </summary>
    private bool IS_CIRCUIT_OPEN()
    {
        /*
         * [Circuit Breaker Logic]
         * If the rejection rate (rejected / total) exceeds the threshold 
         * over a minimum sample size, the circuit opens.
         * While open, ALL requests are rejected immediately to allow the 
         * system to recover. The circuit closes automatically after a timeout.
         */
        // Check if already open
        if (Volatile.Read(ref _circuitBreakerOpen) == 1)
        {
            // Try to close if reset time passed
            long resetTimeTicks = Volatile.Read(ref _circuitBreakerResetTimeTicks);
            long nowTicks = DateTime.UtcNow.Ticks;

            if (nowTicks >= resetTimeTicks && Interlocked.CompareExchange(ref _circuitBreakerOpen, 0, 1) == 1)
            {
                // Reset counters
                _ = Interlocked.Exchange(ref _totalAcquired, 0);
                _ = Interlocked.Exchange(ref _totalRejected, 0);

                if (_logger != null && _logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("[RT.ConcurrencyGate] circuit breaker closed");
                }
            }

            return Volatile.Read(ref _circuitBreakerOpen) == 1;
        }

        // Check if should open
        long totalAttempts = Volatile.Read(ref _totalAcquired) +
                                     Volatile.Read(ref _totalRejected);

        if (totalAttempts < _options.CircuitBreakerMinSamples)
        {
            return false;
        }

        double rejectionRate = (double)Volatile.Read(ref _totalRejected) / totalAttempts;

        if (rejectionRate > _options.CircuitBreakerThreshold)
        {
            if (Interlocked.CompareExchange(ref _circuitBreakerOpen, 1, 0) == 0)
            {
                long resetTime = DateTime.UtcNow.AddSeconds(_options.CircuitBreakerResetAfterSeconds).Ticks;
                _ = Interlocked.Exchange(ref _circuitBreakerResetTimeTicks, resetTime);

                if (_logger != null && _logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError("[RT.ConcurrencyGate] circuit breaker opened (rejection_rate={RejectionRate}, attempts={TotalAttempts})", rejectionRate, totalAttempts);
                }
            }

            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void VALIDATE_ATTRIBUTE(PacketConcurrencyLimitAttribute attr)
    {
        ArgumentNullException.ThrowIfNull(attr);

        if (attr.Max <= 0)
        {
            throw new ArgumentException(
                $"Concurrency max must be > 0, got {attr.Max}",
                nameof(attr));
        }

        if (attr.QueueMax < 0)
        {
            throw new ArgumentException(
                $"Queue max cannot be negative, got {attr.QueueMax}",
                nameof(attr));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Entry GET_OR_CREATE_ENTRY(ushort opcode, PacketConcurrencyLimitAttribute attr)
    {
        return _table.TryGetValue(opcode, out Entry? entry)
            ? entry
            : this.GET_OR_CREATE_ENTRY_SLOW(opcode, attr);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Entry GET_OR_CREATE_ENTRY_SLOW(ushort opcode, PacketConcurrencyLimitAttribute attr)
    {
        return _table.GetOrAdd(opcode,
            static (_, arg) => new Entry(arg.attr.Max, arg.attr.Queue, arg.attr.QueueMax, arg.logger), (attr, logger: _logger));
    }

    private async ValueTask<Lease> ENTER_WITH_QUEUE_ASYNC(
        Entry entry,
        ushort opcode,
        TimeSpan timeout,
        CancellationToken ct)
    {
        if (!entry.TryIncrementQueue())
        {
            _ = Interlocked.Increment(ref _totalRejected);
            throw new ConcurrencyFailureException(
                $"Concurrency queue is full for opcode {opcode:X4} (limit={entry.QueueMax}, current={entry.QueueCount})");
        }

        _ = Interlocked.Increment(ref _totalQueued);

        try
        {
            bool acquired = await entry.Sem.WaitAsync(timeout, ct).ConfigureAwait(false);
            if (!acquired)
            {
                _ = Interlocked.Increment(ref _totalRejected);
                throw new TimeoutException(
                    $"Concurrency gate timeout after {timeout.TotalSeconds}s for opcode {opcode:X4}");
            }

            entry.Touch();
            _ = Interlocked.Increment(ref _totalAcquired);

            return new Lease(entry.Sem, entry);
        }
        catch (OperationCanceledException)
        {
            _ = Interlocked.Increment(ref _totalRejected);
            throw;
        }
        finally
        {
            entry.DecrementQueue();
        }
    }

    [StackTraceHidden]
    private void CLEANUP_IDLE_ENTRIES()
    {
        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            int removed = 0;

            foreach (KeyValuePair<ushort, Entry> kvp in _table)
            {
                ushort opcode = kvp.Key;
                Entry entry = kvp.Value;

                if (!entry.IsIdle)
                {
                    continue;
                }

                TimeSpan age = now - entry.LastUsedUtc;
                if (age < TimeSpan.FromMinutes(_options.MinIdleAgeMinutes))
                {
                    continue;
                }


#pragma warning disable CA2000
                // Remove before disposal to prevent new usage
                if (_table.TryRemove(opcode, out Entry? removedEntry)
                    && removedEntry is not null)
                {
                    removedEntry.Dispose();
                    removed++;
                    _ = Interlocked.Increment(ref _totalCleanedEntries);
                }
#pragma warning restore CA2000

            }

            if (removed > 0)
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("[RT.ConcurrencyGate] cleanup removed={Removed} remaining={TableCount}", removed, _table.Count);
                }
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "[RT.ConcurrencyGate] cleanup-error");
            }
        }
    }

    #endregion Private Methods
}

