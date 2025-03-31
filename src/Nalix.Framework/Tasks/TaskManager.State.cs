// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Concurrency;

namespace Nalix.Framework.Tasks;

public partial class TaskManager
{
    [System.Diagnostics.DebuggerDisplay("Worker {Name} (Running={IsRunning}, Runs={TotalRuns})")]
    private sealed class WorkerState(
        [System.Diagnostics.CodeAnalysis.NotNull] ISnowflake id,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String name,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String group,
        [System.Diagnostics.CodeAnalysis.NotNull] IWorkerOptions opt,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationTokenSource cts) : IWorkerHandle
    {
        #region Backing fields (thread-safe)

        private System.Int64 _progress;
        private System.Int32 _isRunning;                  // 0/1
        private System.Int64 _totalRuns;                  // count
        private System.Int64 _startedUtcTicks;            // DateTimeOffset.UtcNow.Ticks
        private System.Int64 _lastHeartbeatUtcTicks;      // 0 == null
        private System.Int64 _completedUtcTicks;          // 0 == null
        private System.String? _lastNote;                 // Volatile read/write

        #endregion

        #region Properties

        public System.Threading.Tasks.Task? Task;

        public ISnowflake Id { get; } = id;

        public System.String Name { get; } = name;

        public System.String Group { get; } = group;

        public IWorkerOptions Options { get; } = opt;

        public System.Threading.CancellationTokenSource Cts { get; } = cts;

        public System.String? LastNote
        {
            get => System.Threading.Volatile.Read(ref _lastNote);
            private set => System.Threading.Volatile.Write(ref _lastNote, value);
        }

        public System.Boolean IsRunning
        {
            get => System.Threading.Volatile.Read(ref _isRunning) != 0;
            private set => System.Threading.Volatile.Write(ref _isRunning, value ? 1 : 0);
        }

        public System.Int64 TotalRuns
        {
            get => System.Threading.Interlocked.Read(ref _totalRuns);
            private set => System.Threading.Interlocked.Exchange(ref _totalRuns, value);
        }

        public System.DateTimeOffset StartedUtc
            => new(System.Threading.Interlocked.Read(ref _startedUtcTicks), System.TimeSpan.Zero);

        public System.DateTimeOffset? LastHeartbeatUtc
        {
            get
            {
                var t = System.Threading.Interlocked.Read(ref _lastHeartbeatUtcTicks);
                return t == 0 ? null : new System.DateTimeOffset(t, System.TimeSpan.Zero);
            }
            private set
            {
                var t = value?.UtcDateTime.Ticks ?? 0L;
                _ = System.Threading.Interlocked.Exchange(ref _lastHeartbeatUtcTicks, t);
            }
        }

        // Mark khi worker kết thúc (thành công/huỷ/lỗi) để cleanup theo RetainFor
        internal System.DateTimeOffset? CompletedUtc
        {
            get
            {
                var t = System.Threading.Interlocked.Read(ref _completedUtcTicks);
                return t == 0 ? null : new System.DateTimeOffset(t, System.TimeSpan.Zero);
            }
            private set
            {
                var t = value?.UtcDateTime.Ticks ?? 0L;
                _ = System.Threading.Interlocked.Exchange(ref _completedUtcTicks, t);
            }
        }

        #endregion Properties

        #region Computed Methods

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public void MarkStart()
        {
            var nowTicks = System.DateTimeOffset.UtcNow.UtcDateTime.Ticks;
            _ = System.Threading.Interlocked.Exchange(ref _startedUtcTicks, nowTicks);
            _ = System.Threading.Interlocked.Exchange(ref _lastHeartbeatUtcTicks, nowTicks);
            _ = System.Threading.Interlocked.Exchange(ref _completedUtcTicks, 0);
            IsRunning = true;
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public void MarkStop()
        {
            IsRunning = false;
            _ = System.Threading.Interlocked.Increment(ref _totalRuns);
            var nowTicks = System.DateTimeOffset.UtcNow.UtcDateTime.Ticks;
            _ = System.Threading.Interlocked.Exchange(ref _completedUtcTicks, nowTicks);
            _ = System.Threading.Interlocked.Exchange(ref _lastHeartbeatUtcTicks, nowTicks);
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public void MarkError(System.Exception __)
        {
            IsRunning = false;
            System.Int64 ticks = System.DateTimeOffset.UtcNow.UtcDateTime.Ticks;

            _ = System.Threading.Interlocked.Increment(ref _totalRuns);
            _ = System.Threading.Interlocked.Exchange(ref _completedUtcTicks, ticks);
            _ = System.Threading.Interlocked.Exchange(ref _lastHeartbeatUtcTicks, ticks);
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public void Beat()
        {
            System.Int64 ticks = System.DateTimeOffset.UtcNow.UtcDateTime.Ticks;
            _ = System.Threading.Interlocked.Exchange(ref _lastHeartbeatUtcTicks, ticks);
        }

        public System.Int64 Progress => System.Threading.Interlocked.Read(ref _progress);

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public void Add(System.Int64 delta, System.String? note)
        {
            if (delta != 0)
            {
                _ = System.Threading.Interlocked.Add(ref _progress, delta);
            }

            if (note is not null)
            {
                LastNote = note;
            }

            var nowTicks = System.DateTimeOffset.UtcNow.UtcDateTime.Ticks;
            _ = System.Threading.Interlocked.Exchange(ref _lastHeartbeatUtcTicks, nowTicks);
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Cancel() => Cts.Cancel();

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        void System.IDisposable.Dispose() => Cancel();

        #endregion Computed Methods

        #region IWorkerHandle

        ISnowflake IWorkerHandle.Id => Id;

        System.String IWorkerHandle.Name => Name;

        System.String IWorkerHandle.Group => Group;

        System.Boolean IWorkerHandle.IsRunning => IsRunning;

        System.Int64 IWorkerHandle.TotalRuns => TotalRuns;

        System.DateTimeOffset IWorkerHandle.StartedUtc => StartedUtc;

        [System.Diagnostics.CodeAnalysis.MaybeNull]
        System.DateTimeOffset? IWorkerHandle.LastHeartbeatUtc => LastHeartbeatUtc;

        System.Int64 IWorkerHandle.Progress => Progress;

        [System.Diagnostics.CodeAnalysis.MaybeNull]
        System.String? IWorkerHandle.LastNote => LastNote;

        IWorkerOptions IWorkerHandle.Options => Options;

        #endregion IWorkerHandle
    }

    [System.Diagnostics.DebuggerDisplay("Recurring {Name} (Every={Interval}, Runs={TotalRuns}, Failures={ConsecutiveFailures})")]
    private sealed class RecurringState(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String name,
        [System.Diagnostics.CodeAnalysis.NotNull] System.TimeSpan iv,
        [System.Diagnostics.CodeAnalysis.NotNull] IRecurringOptions opt,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationTokenSource cts) : IRecurringHandle
    {
        #region Properties / fields

        public readonly System.Threading.SemaphoreSlim Gate = new(1, 1);

        public System.Threading.Tasks.Task? Task;

        public System.String Name { get; } = name;

        public System.TimeSpan Interval { get; } = iv;

        public IRecurringOptions Options { get; } = opt;

        public System.Threading.CancellationTokenSource CancellationTokenSource { get; } = cts;

        public System.Int64 IntervalTicks { get; } = (System.Int64)(iv.TotalSeconds * System.Diagnostics.Stopwatch.Frequency) switch
        {
            <= 0 => 1,
            var x => x
        };

        // backing fields
        private System.Int64 _totalRuns;
        private System.Int32 _consecutiveFailures;
        private System.Int32 _isRunning;          // 0/1
        private System.Int64 _lastRunUtcTicks;    // 0 == null

        public System.Int64 TotalRuns
        {
            get => System.Threading.Interlocked.Read(ref _totalRuns);
            private set => System.Threading.Interlocked.Exchange(ref _totalRuns, value);
        }

        public System.Int32 ConsecutiveFailures
        {
            get => System.Threading.Volatile.Read(ref _consecutiveFailures);
            private set => System.Threading.Volatile.Write(ref _consecutiveFailures, value);
        }

        public System.Boolean IsRunning
        {
            get => System.Threading.Volatile.Read(ref _isRunning) != 0;
            private set => System.Threading.Volatile.Write(ref _isRunning, value ? 1 : 0);
        }

        public System.DateTimeOffset? LastRunUtc
        {
            get
            {
                var t = System.Threading.Interlocked.Read(ref _lastRunUtcTicks);
                return t == 0 ? null : new System.DateTimeOffset(t, System.TimeSpan.Zero);
            }
            private set
            {
                var t = value?.UtcDateTime.Ticks ?? 0L;
                _ = System.Threading.Interlocked.Exchange(ref _lastRunUtcTicks, t);
            }
        }

        public System.DateTimeOffset? NextRunUtc => LastRunUtc?.Add(Interval);

        #endregion Properties / fields

        #region Computed Methods

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public void MarkStart()
        {
            IsRunning = true;
            LastRunUtc = System.DateTimeOffset.UtcNow;
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public void MarkSuccess()
        {
            IsRunning = false;
            ConsecutiveFailures = 0;
            _ = System.Threading.Interlocked.Increment(ref _totalRuns);
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public void MarkFailure()
        {
            IsRunning = false;
            _ = System.Threading.Interlocked.Increment(ref _consecutiveFailures);
            _ = System.Threading.Interlocked.Increment(ref _totalRuns);
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Cancel() => CancellationTokenSource.Cancel();

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        void System.IDisposable.Dispose() => Cancel();

        #endregion Computed Methods

        #region IRecurringHandle

        System.String IRecurringHandle.Name => Name;

        System.Boolean IRecurringHandle.IsRunning => IsRunning;

        System.Int64 IRecurringHandle.TotalRuns => TotalRuns;

        System.Int32 IRecurringHandle.ConsecutiveFailures => ConsecutiveFailures;

        [System.Diagnostics.CodeAnalysis.MaybeNull]
        System.DateTimeOffset? IRecurringHandle.LastRunUtc => LastRunUtc;

        [System.Diagnostics.CodeAnalysis.MaybeNull]
        System.DateTimeOffset? IRecurringHandle.NextRunUtc => NextRunUtc;

        System.TimeSpan IRecurringHandle.Interval => Interval;

        IRecurringOptions IRecurringHandle.Options => Options;

        #endregion IRecurringHandle
    }

    [System.Runtime.CompilerServices.SkipLocalsInit]
    private sealed class WorkerContext(
        [System.Diagnostics.CodeAnalysis.NotNull] WorkerState st,
        [System.Diagnostics.CodeAnalysis.NotNull] TaskManager owner) : IWorkerContext
    {
        private readonly WorkerState _st = st;
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Roslynator", "RCS1213:Remove unused member declaration", Justification = "<Pending>")]
        private readonly TaskManager _owner = owner;

        public ISnowflake Id => _st.Id;
        public System.String Name => _st.Name;
        public System.String Group => _st.Group;

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public void Beat() => _st.Beat();

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Advance(System.Int64 delta, System.String? note = null) => _st.Add(delta, note);

        public System.Boolean IsCancellationRequested => _st.Cts.IsCancellationRequested;
    }
}
