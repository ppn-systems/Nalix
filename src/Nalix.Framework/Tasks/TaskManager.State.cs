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
        [NotNull] ISnowflake id,
        [NotNull] string name,
        [NotNull] string group,
        [NotNull] IWorkerOptions opt,
        [NotNull] CancellationTokenSource cts) : IWorkerHandle
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

        [MethodImpl(MethodImplOptions.AggressiveInlining |
            MethodImplOptions.AggressiveOptimization)]
        public void MarkStart()
        {
            long nowTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks;
            _ = Interlocked.Exchange(ref _startedUtcTicks, nowTicks);
            _ = Interlocked.Exchange(ref _lastHeartbeatUtcTicks, nowTicks);
            _ = Interlocked.Exchange(ref _completedUtcTicks, 0);
            IsRunning = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining |
            MethodImplOptions.AggressiveOptimization)]
        public void MarkStop()
        {
            IsRunning = false;
            _ = Interlocked.Increment(ref _totalRuns);
            long nowTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks;
            _ = Interlocked.Exchange(ref _completedUtcTicks, nowTicks);
            _ = Interlocked.Exchange(ref _lastHeartbeatUtcTicks, nowTicks);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining |
            MethodImplOptions.AggressiveOptimization)]
        public void MarkError(Exception __)
        {
            IsRunning = false;
            long ticks = DateTimeOffset.UtcNow.UtcDateTime.Ticks;

            _ = Interlocked.Increment(ref _totalRuns);
            _ = Interlocked.Exchange(ref _completedUtcTicks, ticks);
            _ = Interlocked.Exchange(ref _lastHeartbeatUtcTicks, ticks);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining |
            MethodImplOptions.AggressiveOptimization)]
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
                LastNote = note;
            }

            long nowTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks;
            _ = Interlocked.Exchange(ref _lastHeartbeatUtcTicks, nowTicks);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Cancel() => Cts.Cancel();

        [MethodImpl(MethodImplOptions.NoInlining)]
        void IDisposable.Dispose() => Cancel();

        #endregion Computed Methods

        #region IWorkerHandle

        ISnowflake IWorkerHandle.Id => Id;

        string IWorkerHandle.Name => Name;

        string IWorkerHandle.Group => Group;

        bool IWorkerHandle.IsRunning => IsRunning;

        long IWorkerHandle.TotalRuns => TotalRuns;

        DateTimeOffset IWorkerHandle.StartedUtc => StartedUtc;

        [MaybeNull]
        DateTimeOffset? IWorkerHandle.LastHeartbeatUtc => LastHeartbeatUtc;

        long IWorkerHandle.Progress => Progress;

        [MaybeNull]
        string? IWorkerHandle.LastNote => LastNote;

        IWorkerOptions IWorkerHandle.Options => Options;

        #endregion IWorkerHandle
    }

    [DebuggerDisplay("Recurring {Name} (Every={Interval}, Runs={TotalRuns}, Failures={ConsecutiveFailures})")]
    private sealed class RecurringState(
        [NotNull] string name,
        [NotNull] TimeSpan iv,
        [NotNull] IRecurringOptions opt,
        [NotNull] CancellationTokenSource cts) : IRecurringHandle
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

        public DateTimeOffset? NextRunUtc => LastRunUtc?.Add(Interval);

        #endregion Properties / fields

        #region Computed Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining |
            MethodImplOptions.AggressiveOptimization)]
        public void MarkStart()
        {
            IsRunning = true;
            LastRunUtc = DateTimeOffset.UtcNow;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining |
            MethodImplOptions.AggressiveOptimization)]
        public void MarkSuccess()
        {
            IsRunning = false;
            ConsecutiveFailures = 0;
            _ = Interlocked.Increment(ref _totalRuns);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining |
            MethodImplOptions.AggressiveOptimization)]
        public void MarkFailure()
        {
            IsRunning = false;
            _ = Interlocked.Increment(ref _consecutiveFailures);
            _ = Interlocked.Increment(ref _totalRuns);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Cancel() => CancellationTokenSource.Cancel();

        [MethodImpl(MethodImplOptions.NoInlining)]
        void IDisposable.Dispose() => Cancel();

        #endregion Computed Methods

        #region IRecurringHandle

        string IRecurringHandle.Name => Name;

        bool IRecurringHandle.IsRunning => IsRunning;

        long IRecurringHandle.TotalRuns => TotalRuns;

        int IRecurringHandle.ConsecutiveFailures => ConsecutiveFailures;

        [MaybeNull]
        DateTimeOffset? IRecurringHandle.LastRunUtc => LastRunUtc;

        [MaybeNull]
        DateTimeOffset? IRecurringHandle.NextRunUtc => NextRunUtc;

        TimeSpan IRecurringHandle.Interval => Interval;

        IRecurringOptions IRecurringHandle.Options => Options;

        #endregion IRecurringHandle
    }

    [SkipLocalsInit]
    private sealed class WorkerContext(
        [NotNull] WorkerState st,
        [NotNull] TaskManager owner) : IWorkerContext
    {
        private readonly WorkerState _st = st;

        [SuppressMessage("Style", "IDE0052:Remove unread private members", Justification = "<Pending>")]
        private readonly TaskManager _owner = owner;

        public ISnowflake Id => _st.Id;
        public string Name => _st.Name;
        public string Group => _st.Group;

        [MethodImpl(MethodImplOptions.AggressiveInlining |
            MethodImplOptions.AggressiveOptimization)]
        public void Beat() => _st.Beat();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Advance(long delta, string? note = null) => _st.Add(delta, note);

        public bool IsCancellationRequested => _st.Cts.IsCancellationRequested;
    }
}
