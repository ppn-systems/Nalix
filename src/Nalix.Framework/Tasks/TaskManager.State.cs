// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Tasks;
using Nalix.Common.Tasks.Options;

namespace Nalix.Framework.Tasks;

public partial class TaskManager
{
    private sealed class WorkerState(
    IIdentifier id,
    System.String name,
    System.String group,
    IWorkerOptions opt,
    System.Threading.CancellationTokenSource cts) : IWorkerHandle
    {
        #region Fields

        private System.Int64 _progress;

        #endregion Fields

        #region Properties

        public System.Threading.Tasks.Task? Task;

        public IIdentifier Id { get; } = id;

        public System.String Name { get; } = name;

        public System.String Group { get; } = group;

        public IWorkerOptions Options { get; } = opt;

        public System.Threading.CancellationTokenSource Cts { get; } = cts;

        public System.String? LastNote { get; private set; }

        public System.Boolean IsRunning { get; private set; }

        public System.Int64 TotalRuns { get; private set; }

        public System.DateTimeOffset StartedUtc { get; private set; }

        public System.DateTimeOffset? LastHeartbeatUtc { get; private set; }

        // Mark khi worker kết thúc (thành công/huỷ/lỗi) để cleanup theo Retention
        internal System.DateTimeOffset? CompletedUtc { get; private set; }

        #endregion Properties

        #region Computed Properties

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void MarkStart()
        {
            IsRunning = true;
            StartedUtc = System.DateTimeOffset.UtcNow;
            LastHeartbeatUtc = StartedUtc;
            CompletedUtc = null;
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void MarkStop()
        {
            IsRunning = false;
            TotalRuns++;
            CompletedUtc = System.DateTimeOffset.UtcNow;
            LastHeartbeatUtc = CompletedUtc;
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void MarkError(System.Exception _)
        {
            IsRunning = false;
            TotalRuns++;
            CompletedUtc = System.DateTimeOffset.UtcNow;
            // có thể lưu chi tiết lỗi sau nếu cần
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Beat() => LastHeartbeatUtc = System.DateTimeOffset.UtcNow;


        public System.Int64 Progress => System.Threading.Interlocked.Read(ref _progress);

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

            LastHeartbeatUtc = System.DateTimeOffset.UtcNow;
        }

        public void Cancel() => Cts.Cancel();

        void System.IDisposable.Dispose() => Cancel();

        #endregion Computed Properties

        #region IWorkerHandle

        IIdentifier IWorkerHandle.Id => Id;

        System.String IWorkerHandle.Name => Name;

        System.String IWorkerHandle.Group => Group;

        System.Boolean IWorkerHandle.IsRunning => IsRunning;

        System.Int64 IWorkerHandle.TotalRuns => TotalRuns;

        System.DateTimeOffset IWorkerHandle.StartedUtc => StartedUtc;

        System.DateTimeOffset? IWorkerHandle.LastHeartbeatUtc => LastHeartbeatUtc;

        System.Int64 IWorkerHandle.Progress => Progress;

        System.String? IWorkerHandle.LastNote => LastNote;

        IWorkerOptions IWorkerHandle.Options => Options;

        #endregion IWorkerHandle
    }

    private sealed class RecurringState(
        System.String name,
        System.TimeSpan iv,
        IRecurringOptions opt,
        System.Threading.CancellationTokenSource cts) : IRecurringHandle
    {
        #region Properties

        public readonly System.Threading.SemaphoreSlim Gate = new(1, 1);

        public System.Threading.Tasks.Task? Task;

        public System.String Name { get; } = name;

        public System.TimeSpan Interval { get; } = iv;

        public IRecurringOptions Options { get; } = opt;

        public System.Threading.CancellationTokenSource Cts { get; } = cts;

        public System.Int64 TotalRuns { get; private set; }

        public System.Int32 ConsecutiveFailures { get; private set; }

        public System.Boolean IsRunning { get; private set; }

        public System.DateTimeOffset? LastRunUtc { get; private set; }

        public System.DateTimeOffset? NextRunUtc => LastRunUtc?.Add(Interval);

        #endregion Properties

        #region Computed Properties

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void MarkStart()
        {
            IsRunning = true;
            LastRunUtc = System.DateTimeOffset.UtcNow;
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void MarkSuccess()
        {
            IsRunning = false;
            ConsecutiveFailures = 0;
            TotalRuns++;
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void MarkFailure()
        {
            IsRunning = false;
            ConsecutiveFailures++;
            TotalRuns++;
        }

        public void Cancel() => Cts.Cancel();

        void System.IDisposable.Dispose() => Cancel();

        #endregion Computed Properties

        #region IRecurringHandle

        System.String IRecurringHandle.Name => Name;

        System.Boolean IRecurringHandle.IsRunning => IsRunning;

        System.Int64 IRecurringHandle.TotalRuns => TotalRuns;

        System.Int32 IRecurringHandle.ConsecutiveFailures => ConsecutiveFailures;

        System.DateTimeOffset? IRecurringHandle.LastRunUtc => LastRunUtc;

        System.DateTimeOffset? IRecurringHandle.NextRunUtc => NextRunUtc;

        System.TimeSpan IRecurringHandle.Interval => Interval;

        IRecurringOptions IRecurringHandle.Options => Options;

        #endregion IRecurringHandle
    }

    private sealed class WorkerContext(WorkerState st, TaskManager owner) : IWorkerContext
    {
        private readonly WorkerState _st = st;
        private readonly TaskManager _owner = owner;

        public IIdentifier Id => _st.Id;
        public System.String Name => _st.Name;
        public System.String Group => _st.Group;

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Heartbeat() => _st.Beat();

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void AddProgress(System.Int64 delta, System.String? note = null) => _st.Add(delta, note);

        public System.Boolean IsCancellationRequested => _st.Cts.IsCancellationRequested;
    }
}
