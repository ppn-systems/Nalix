// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Environment.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Runtime.Options;

namespace Nalix.Runtime.Throttling;

/// <summary>
/// High-performance per-opcode concurrency limiter with optional FIFO queuing.
/// Thread-safe with reference counting for safe disposal.
/// Automatically cleans up idle entries to prevent memory leaks.
/// </summary>
[DebuggerNonUserCode]
[SkipLocalsInit]
public sealed partial class ConcurrencyGate : IReportable
{
    #region Fields

    private readonly ConcurrencyOptions _options;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ushort, Entry> _table = new();

    private long _totalAcquired;
    private long _totalRejected;
    private long _totalQueued;
    private long _totalCleanedEntries;
    private long _circuitBreakerTrips;

    private int _circuitBreakerOpen; // 0 = closed, 1 = open
    private long _circuitBreakerResetTimeTicks;

    #endregion Fields

    #region Static Constructor

    /// <summary>
    /// Initializes the cleanup task.
    /// </summary>
    public ConcurrencyGate()
    {
        _options = ConfigurationManager.Instance.Get<ConcurrencyOptions>();
        _options.Validate();

        try
        {
            _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
                name: $"concurrency.gate.cleanup.{this.GetHashCode():X8}",
                interval: TimeSpan.FromMinutes(_options.CleanupIntervalMinutes),
                work: _ =>
                {
                    this.CLEANUP_IDLE_ENTRIES();
                    return ValueTask.CompletedTask;
                },
                options: new RecurringOptions
                {
                    NonReentrant = true,
                    Tag = TaskNaming.Tags.Service,
                    Jitter = TimeSpan.FromSeconds(10),
                    ExecutionTimeout = TimeSpan.FromSeconds(5)
                });
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            throw new InternalErrorException($"[RT.{nameof(ConcurrencyGate)}] initialization-error: {ex.Message}", ex);
        }
    }

    #endregion Static Constructor


    #region Public API

    /// <summary>
    /// Attempts to enter immediately without waiting.
    /// </summary>
    /// <param name="opcode"></param>
    /// <param name="attr"></param>
    /// <param name="lease"></param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool TryEnter(ushort opcode, PacketConcurrencyLimitAttribute attr, out Lease lease)
    {
        /*
         * [Concurrency Check Workflow]
         * 1. Check if the global circuit breaker is open (fail fast).
         * 2. Resolve/Create the entry for this specific opcode.
         * 3. Acquire a reference (TryAcquire) to prevent entry disposal.
         * 4. Attempt immediate entry into the semaphore.
         */
        if (this.IS_CIRCUIT_OPEN())
        {
            _ = Interlocked.Increment(ref _circuitBreakerTrips);
            lease = default;
            return false;
        }

        VALIDATE_ATTRIBUTE(attr);

        Entry entry = this.GET_OR_CREATE_ENTRY(opcode, attr);

        if (!entry.TryAcquire())
        {
            _ = Interlocked.Increment(ref _totalRejected);
            lease = default;
            return false;
        }

        bool leaseGranted = false;
        try
        {
            if (entry.Sem.Wait(0))
            {
                entry.Touch();
                _ = Interlocked.Increment(ref _totalAcquired);

                lease = new Lease(entry.Sem, entry);
                leaseGranted = true;
                return true;
            }

            _ = Interlocked.Increment(ref _totalRejected);
            lease = default;
            return false;
        }
        catch (ObjectDisposedException)
        {
            // Entry was disposed - treat as rejection
            _ = Interlocked.Increment(ref _totalRejected);
            lease = default;
            return false;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
            {
                DiagnosticsEvents.Write(
                    DiagnosticsEvents.Internal.Error,
                    new DiagnosticLog(
                        "RT.ConcurrencyGate:TryEnter",
                        $"unexpected-error opcode={opcode}",
                        ex));
            }
            lease = default;
            return false;
        }
        finally
        {
            if (!leaseGranted)
            {
                entry.Release();
            }
        }
    }

    /// <summary>
    /// Enters with optional waiting when queuing is enabled.
    /// </summary>
    /// <param name="opcode"></param>
    /// <param name="attr"></param>
    /// <param name="ct"></param>
    /// <exception cref="ConcurrencyFailureException"></exception>
    /// <exception cref="TimeoutException"></exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async ValueTask<Lease> EnterAsync(ushort opcode, PacketConcurrencyLimitAttribute attr, CancellationToken ct = default)
    {
        if (this.IS_CIRCUIT_OPEN())
        {
            _ = Interlocked.Increment(ref _circuitBreakerTrips);
            throw new ConcurrencyFailureException(
                $"Circuit breaker is open for opcode {opcode:X4}");
        }

        VALIDATE_ATTRIBUTE(attr);

        Entry entry = this.GET_OR_CREATE_ENTRY(opcode, attr);

        if (!entry.TryAcquire())
        {
            throw new ConcurrencyFailureException(
                $"Entry for opcode {opcode:X4} is being disposed");
        }

        try
        {
            // No queue: immediate attempt only
            if (!entry.Queue)
            {
                if (!entry.Sem.Wait(0, ct))
                {
                    _ = Interlocked.Increment(ref _totalRejected);
                    throw new ConcurrencyFailureException(
                        $"Concurrency limit reached for opcode {opcode:X4} (no queue)");
                }

                entry.Touch();
                _ = Interlocked.Increment(ref _totalAcquired);

                return new Lease(entry.Sem, entry);
            }

            // Queue enabled
            return await this.ENTER_WITH_QUEUE_ASYNC(entry, opcode, TimeSpan.FromSeconds(_options.WaitTimeoutSeconds), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            entry.Release();
            throw;
        }
    }

    /// <summary>
    /// Gets diagnostic statistics.
    /// </summary>
    public (long TotalAcquired, long TotalRejected, long TotalQueued, long TotalCleaned, long CircuitBreakerTrips, bool CircuitBreakerOpen, int TrackedOpcodes) GetStatistics()
    {
        return (
            Interlocked.Read(ref _totalAcquired),
            Interlocked.Read(ref _totalRejected),
            Interlocked.Read(ref _totalQueued),
            Interlocked.Read(ref _totalCleanedEntries),
            Interlocked.Read(ref _circuitBreakerTrips),
            Volatile.Read(ref _circuitBreakerOpen) == 1,
            _table.Count
        );
    }

    /// <summary>
    /// Resets statistics. For testing only.
    /// </summary>
    internal void ResetStatistics()
    {
        _ = Interlocked.Exchange(ref _totalAcquired, 0);
        _ = Interlocked.Exchange(ref _totalRejected, 0);
        _ = Interlocked.Exchange(ref _totalQueued, 0);
        _ = Interlocked.Exchange(ref _totalCleanedEntries, 0);
        _ = Interlocked.Exchange(ref _circuitBreakerTrips, 0);
        _ = Interlocked.Exchange(ref _circuitBreakerOpen, 0);
    }

    #endregion Public API


}
