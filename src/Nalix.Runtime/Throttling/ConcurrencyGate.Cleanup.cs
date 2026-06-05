// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Diagnostics;
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

                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Information))
                {
                    DiagnosticsEvents.Source.Write(
                        DiagnosticsEvents.Internal.Information,
                        new DiagnosticLog(
                            "RT.ConcurrencyGate:Internal",
                            "circuit-breaker closed"));
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

                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
                {
                    DiagnosticsEvents.Source.Write(
                        DiagnosticsEvents.Internal.Error,
                        new DiagnosticLog(
                            "RT.ConcurrencyGate:Internal",
                            $"circuit-breaker opened rejection_rate={rejectionRate} attempts={totalAttempts}"));
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
            THROW_MAX_OUT_OF_RANGE(attr.Max);
        }

        if (attr.QueueMax < 0)
        {
            THROW_QUEUE_MAX_NEGATIVE(attr.QueueMax);
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
            static (_, arg) => new Entry(arg.Max, arg.Queue, arg.QueueMax), attr);
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
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                {
                    DiagnosticsEvents.Source.Write(
                        DiagnosticsEvents.Internal.Debug,
                        new DiagnosticLog(
                            "RT.ConcurrencyGate:Internal",
                            $"cleanup removed={removed} remaining={_table.Count}"));
                }
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
            {
                DiagnosticsEvents.Source.Write(
                    DiagnosticsEvents.Internal.Error,
                    new DiagnosticLog(
                        "RT.ConcurrencyGate:Internal",
                        "cleanup-error",
                        ex));
            }
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void THROW_MAX_OUT_OF_RANGE(int max) => throw new ArgumentException($"Concurrency max must be > 0, got {max}", "attr");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void THROW_QUEUE_MAX_NEGATIVE(int queueMax) => throw new ArgumentException($"Queue max cannot be negative, got {queueMax}", "attr");

    #endregion Private Methods
}

