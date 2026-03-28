// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Concurrency;
using Nalix.Common.Identity;

namespace Nalix.Framework.Tasks;

public partial class TaskManager
{
    [DebuggerDisplay("Worker {Name} (Running={IsRunning}, Runs={TotalRuns})")]
    private sealed class WorkerState(
        ISnowflake id,
        string name,
        string group,
        IWorkerOptions opt,
        CancellationTokenSource cts) : IWorkerHandle
    {
        #region Backing fields (thread-safe)

        private long _progress;

        /// <summary>
        /// 0/1
        /// </summary>
        private int _isRunning;

        /// <summary>
        /// count
        /// </summary>
        private long _totalRuns;

        /// <summary>
        /// DateTimeOffset.UtcNow.Ticks
        /// </summary>
        private long _startedUtcTicks;

        /// <summary>
        /// 0 == null
        /// </summary>
        private long _lastHeartbeatUtcTicks;

        /// <summary>
        /// 0 == null
        /// </summary>
        private long _completedUtcTicks;

        #endregion Backing fields (thread-safe)

        #region Properties

        public Task? Task;

        public ISnowflake Id { get; } = id;

        public string Name { get; } = name;

        public string Group { get; } = group;

        public IWorkerOptions Options { get; } = opt;

        public CancellationTokenSource Cts { get; } = cts;

        public string? LastNote
        {
            get => Volatile.Read(ref field);
            private set => Volatile.Write(ref field, value);
        }

        public bool IsRunning
        {
            get => Volatile.Read(ref _isRunning) != 0;
            private set => Volatile.Write(ref _isRunning, value ? 1 : 0);
        }

        public long TotalRuns
        {
            get => Interlocked.Read(ref _totalRuns);
            private set => Interlocked.Exchange(ref _totalRuns, value);
        }

        public DateTimeOffset StartedUtc
            => new(Interlocked.Read(ref _startedUtcTicks), TimeSpan.Zero);

        public DateTimeOffset? LastHeartbeatUtc
        {
            get
            {
                long t = Interlocked.Read(ref _lastHeartbeatUtcTicks);
                return t == 0 ? null : new System.DateTimeOffset(t, System.TimeSpan.Zero);
            }
            private set
            {
                long t = value?.UtcDateTime.Ticks ?? 0L;
                _ = Interlocked.Exchange(ref _lastHeartbeatUtcTicks, t);
            }
        }

        /// <summary>
        /// Mark khi worker kết thúc (thành công/huỷ/lỗi) để cleanup theo RetainFor
        /// </summary>
        internal DateTimeOffset? CompletedUtc
        {
            get
            {
                long t = Interlocked.Read(ref _completedUtcTicks);
                return t == 0 ? null : new System.DateTimeOffset(t, System.TimeSpan.Zero);
            }
            private set
            {
                long t = value?.UtcDateTime.Ticks ?? 0L;
                _ = Interlocked.Exchange(ref _completedUtcTicks, t);
            }
        }

        #endregion Properties

        #region Computed Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void MarkStart()
        {
            long nowTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks;
            _ = Interlocked.Exchange(ref _startedUtcTicks, nowTicks);
            _ = Interlocked.Exchange(ref _lastHeartbeatUtcTicks, nowTicks);
            _ = Interlocked.Exchange(ref _completedUtcTicks, 0);
            this.IsRunning = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void MarkStop()
        {
            this.IsRunning = false;
            _ = Interlocked.Increment(ref _totalRuns);
            long nowTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks;
            _ = Interlocked.Exchange(ref _completedUtcTicks, nowTicks);
            _ = Interlocked.Exchange(ref _lastHeartbeatUtcTicks, nowTicks);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void MarkError(Exception __)
        {
            this.IsRunning = false;
            long ticks = DateTimeOffset.UtcNow.UtcDateTime.Ticks;

            _ = Interlocked.Increment(ref _totalRuns);
            _ = Interlocked.Exchange(ref _completedUtcTicks, ticks);
            _ = Interlocked.Exchange(ref _lastHeartbeatUtcTicks, ticks);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void Beat()
        {
            long ticks = DateTimeOffset.UtcNow.UtcDateTime.Ticks;
            _ = Interlocked.Exchange(ref _lastHeartbeatUtcTicks, ticks);
        }

        public long Progress => Interlocked.Read(ref _progress);

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Add(long delta, string? note)
        {
            if (delta != 0)
            {
                _ = Interlocked.Add(ref _progress, delta);
            }

            if (note is not null)
            {
                this.LastNote = note;
            }

            long nowTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks;
            _ = Interlocked.Exchange(ref _lastHeartbeatUtcTicks, nowTicks);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Cancel() => this.Cts.Cancel();

        [MethodImpl(MethodImplOptions.NoInlining)]
        void IDisposable.Dispose() => this.Cancel();

        #endregion Computed Methods

        #region IWorkerHandle

        ISnowflake IWorkerHandle.Id => this.Id;

        string IWorkerHandle.Name => this.Name;

        string IWorkerHandle.Group => this.Group;

        bool IWorkerHandle.IsRunning => this.IsRunning;

        long IWorkerHandle.TotalRuns => this.TotalRuns;

        DateTimeOffset IWorkerHandle.StartedUtc => this.StartedUtc;

        [MaybeNull]
        DateTimeOffset? IWorkerHandle.LastHeartbeatUtc => this.LastHeartbeatUtc;

        long IWorkerHandle.Progress => this.Progress;

        [MaybeNull]
        string? IWorkerHandle.LastNote => this.LastNote;

        IWorkerOptions IWorkerHandle.Options => this.Options;

        #endregion IWorkerHandle
    }

    [DebuggerDisplay("Recurring {Name} (Every={Interval}, Runs={TotalRuns}, Failures={ConsecutiveFailures})")]
    private sealed class RecurringState(
        string name,
        TimeSpan iv,
        IRecurringOptions opt,
        CancellationTokenSource cts) : IRecurringHandle
    {
        #region Properties / fields

        public readonly SemaphoreSlim Gate = new(1, 1);

        public Task? Task;

        public string Name { get; } = name;

        public TimeSpan Interval { get; } = iv;

        public IRecurringOptions Options { get; } = opt;

        public CancellationTokenSource CancellationTokenSource { get; } = cts;

        public long IntervalTicks { get; } = (long)(iv.TotalSeconds * Stopwatch.Frequency) switch
        {
            <= 0 => 1,
            var x => x
        };

        /// <summary>
        /// backing fields
        /// </summary>
        private long _totalRuns;

        private int _consecutiveFailures;

        /// <summary>
        /// 0/1
        /// </summary>
        private int _isRunning;

        /// <summary>
        /// 0 == null
        /// </summary>
        private long _lastRunUtcTicks;

        public long TotalRuns
        {
            get => Interlocked.Read(ref _totalRuns);
            private set => Interlocked.Exchange(ref _totalRuns, value);
        }

        public int ConsecutiveFailures
        {
            get => Volatile.Read(ref _consecutiveFailures);
            private set => Volatile.Write(ref _consecutiveFailures, value);
        }

        public bool IsRunning
        {
            get => Volatile.Read(ref _isRunning) != 0;
            private set => Volatile.Write(ref _isRunning, value ? 1 : 0);
        }

        public DateTimeOffset? LastRunUtc
        {
            get
            {
                long t = Interlocked.Read(ref _lastRunUtcTicks);
                return t == 0 ? null : new System.DateTimeOffset(t, System.TimeSpan.Zero);
            }
            private set
            {
                long t = value?.UtcDateTime.Ticks ?? 0L;
                _ = Interlocked.Exchange(ref _lastRunUtcTicks, t);
            }
        }

        public DateTimeOffset? NextRunUtc => this.LastRunUtc?.Add(this.Interval);

        #endregion Properties / fields

        #region Computed Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void MarkStart()
        {
            this.IsRunning = true;
            this.LastRunUtc = DateTimeOffset.UtcNow;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void MarkSuccess()
        {
            this.IsRunning = false;
            this.ConsecutiveFailures = 0;
            _ = Interlocked.Increment(ref _totalRuns);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void MarkFailure()
        {
            this.IsRunning = false;
            _ = Interlocked.Increment(ref _consecutiveFailures);
            _ = Interlocked.Increment(ref _totalRuns);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Cancel() => this.CancellationTokenSource.Cancel();

        [MethodImpl(MethodImplOptions.NoInlining)]
        void IDisposable.Dispose() => this.Cancel();

        #endregion Computed Methods

        #region IRecurringHandle

        string IRecurringHandle.Name => this.Name;

        bool IRecurringHandle.IsRunning => this.IsRunning;

        long IRecurringHandle.TotalRuns => this.TotalRuns;

        int IRecurringHandle.ConsecutiveFailures => this.ConsecutiveFailures;

        [MaybeNull]
        DateTimeOffset? IRecurringHandle.LastRunUtc => this.LastRunUtc;

        [MaybeNull]
        DateTimeOffset? IRecurringHandle.NextRunUtc => this.NextRunUtc;

        TimeSpan IRecurringHandle.Interval => this.Interval;

        IRecurringOptions IRecurringHandle.Options => this.Options;

        #endregion IRecurringHandle
    }

    [SkipLocalsInit]
    private sealed class WorkerContext(
        WorkerState st,
        TaskManager owner) : IWorkerContext
    {
        private readonly WorkerState _st = st;

        [SuppressMessage("Style", "IDE0052:Remove unread private members", Justification = "<Pending>")]
        private readonly TaskManager _owner = owner;

        public ISnowflake Id => _st.Id;
        public string Name => _st.Name;
        public string Group => _st.Group;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void Beat() => _st.Beat();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Advance(long delta, string? note = null) => _st.Add(delta, note);

        public bool IsCancellationRequested => _st.Cts.IsCancellationRequested;
    }
}
