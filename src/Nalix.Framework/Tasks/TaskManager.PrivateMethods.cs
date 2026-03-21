// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics;
using Nalix.Common.Identity;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Random;
using Nalix.Framework.Time;

namespace Nalix.Framework.Tasks;

public partial class TaskManager
{
    #region Types

    private sealed record Gate(System.Threading.SemaphoreSlim SemaphoreSlim, System.Int32 Capacity);

    /// <summary>
    /// Snapshot of CPU metrics for safe concurrent access.
    /// </summary>
    private sealed class CpuMetricsSnapshot
    {
        public System.Double CurrentUsagePercent { get; set; }
        public System.Int64 LastUpdateUtc { get; set; }
        public System.Double ProcessorCount { get; set; }
    }

    #endregion Types

    #region Fields (CPU Monitoring)

    private System.Int64 _lastCpuWallClockMs;
    private System.Int64 _lastCpuProcessorTime;
    private readonly System.Diagnostics.Stopwatch _cpuMeasureStopwatch = System.Diagnostics.Stopwatch.StartNew();

    private System.Int32 _lowCpuStreak;
    private System.Int32 _highCpuStreak;

    private volatile System.Boolean _cpuWarmupDone;

    #endregion Fields (CPU Monitoring)

    #region Internal Cleanup

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void CLEANUP_WORKERS()
    {
        if (_disposed)
        {
            return;
        }

        System.DateTimeOffset now = System.DateTimeOffset.UtcNow;
        foreach (var kv in _workers)
        {
            WorkerState st = kv.Value;
            System.TimeSpan? keep = st.Options.RetainFor;

            if (keep is null || keep <= System.TimeSpan.Zero)
            {
                continue;
            }

            if (st.IsRunning)
            {
                continue;
            }

            if (now - st.CompletedUtc < keep.Value)
            {
                continue;
            }

            if (_workers.TryRemove(st.Id, out _))
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[FW.{nameof(TaskManager)}] cleanup-remove-ok id={st.Id}");
                try
                {
                    st.Cts.Dispose();
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[FW.{nameof(TaskManager)}] cleanup-cts-dispose-error id={st.Id} msg={ex.Message}");
                }
            }
        }
    }

    #endregion Internal Cleanup

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private async System.Threading.Tasks.Task RECURRING_LOOP_ASYNC(
        RecurringState s, System.Func<System.Threading.CancellationToken, System.Threading.Tasks.ValueTask> work)
    {
        System.Threading.CancellationToken ct = s.CancellationTokenSource.Token;

        // Initial jitter (unchanged semantics)
        if (s.Options.Jitter is { } j && j > System.TimeSpan.Zero)
        {
            try
            {
                System.Int32 maxMs = (System.Int32)j.TotalMilliseconds;
                if (maxMs > 0)
                {
                    System.TimeSpan jitter = System.TimeSpan.FromMilliseconds(Csprng.GetInt32(0, maxMs));
                    await System.Threading.Tasks.Task.Delay(jitter, ct).ConfigureAwait(false);
                }
            }
            catch (System.OperationCanceledException) { return; }
        }

        // Interval in Stopwatch ticks
        System.Int64 step = s.IntervalTicks;
        System.Int64 freq = System.Diagnostics.Stopwatch.Frequency;
        System.Int64 next = System.Diagnostics.Stopwatch.GetTimestamp() + step;

        // Local helpers for fast delay
        static void BusyWait(System.Int64 untilTicks, System.Threading.CancellationToken ct)
        {
            System.Threading.SpinWait sw = new();
            while (System.Diagnostics.Stopwatch.GetTimestamp() < untilTicks)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                sw.SpinOnce(sleep1Threshold: -1);
            }
        }

        const System.Double BusyWaitMaxSeconds = 0.0002; // 200 µs cap for busy spin

        while (!ct.IsCancellationRequested)
        {
            try
            {
                System.Int64 now = Clock.MonoTicksNow();
                System.Int64 delayTicks = next - now;

                if (delayTicks > 0)
                {
                    System.Double delaySeconds = (System.Double)delayTicks / freq;

                    if (delaySeconds <= BusyWaitMaxSeconds)
                    {
                        BusyWait(next, ct);
                    }
                    else
                    {
                        System.TimeSpan ts = System.TimeSpan.FromSeconds(delaySeconds);
                        await System.Threading.Tasks.Task.Delay(ts, ct)
                                                         .ConfigureAwait(false);
                    }
                }
                else
                {
                    // catch up missed intervals
                    System.Int64 missed = ((-delayTicks) + step - 1) / step;
                    next += (missed + 1) * step;
                }

                if (s.Options.NonReentrant)
                {
                    if (!await s.Gate.WaitAsync(0, ct).ConfigureAwait(false))
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Debug($"[FW.{nameof(TaskManager)}:Internal] gate-acquire-fail name={s.Name}");
                        next += step;
                        continue;
                    }
                }

                try
                {
                    s.MarkStart();

                    if (s.Options.ExecutionTimeout is { } to && to > System.TimeSpan.Zero)
                    {
                        using System.Threading.CancellationTokenSource rcts =
                            System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);

                        rcts.CancelAfter(to);
                        await work(rcts.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        await work(ct).ConfigureAwait(false);
                    }

                    s.MarkSuccess();
                }
                catch (System.OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (System.OperationCanceledException oce)
                {
                    s.MarkFailure();
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[FW.{nameof(TaskManager)}:Internal] recurring-timeout name={s.Name} msg={oce.Message}");

                    await RECURRING_BACKOFF_ASYNC(s, ct).ConfigureAwait(false);
                }
                catch (System.Exception ex)
                {
                    s.MarkFailure();
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[FW.{nameof(TaskManager)}:Internal] recurring-error name={s.Name} msg={ex.Message}");

                    await RECURRING_BACKOFF_ASYNC(s, ct).ConfigureAwait(false);
                }
                finally
                {
                    if (s.Options.NonReentrant)
                    {
                        try
                        {
                            _ = s.Gate.Release();
                        }
                        catch (System.Exception ex)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Warn($"[FW.{nameof(TaskManager)}:Internal] gate-release-error name={s.Name} msg={ex.Message}");
                        }
                    }
                    next += step;
                }
            }
            catch (System.OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (System.Exception ex)
            {
                s.MarkFailure();
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[FW.{nameof(TaskManager)}:Internal] recurring-loop-error name={s.Name} msg={ex.Message}");

                await RECURRING_BACKOFF_ASYNC(s, ct).ConfigureAwait(false);
            }
        }
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static async System.Threading.Tasks.ValueTask RECURRING_BACKOFF_ASYNC(
        RecurringState s,
        System.Threading.CancellationToken ct)
    {
        System.Int32 n = System.Math.Max(1, s.Options.FailuresBeforeBackoff);
        if (s.ConsecutiveFailures < n)
        {
            return;
        }

        System.Int32 pow = System.Math.Min(5, s.ConsecutiveFailures - n); // cap at 2^5 = 32s
        System.Int32 baseMs = 1000 << pow; // base delay: 1000ms * 2^pow
        System.Int32 cap = (System.Int32)System.Math.Max(1, s.Options.BackoffCap.TotalMilliseconds);
        System.Int32 maxDelay = System.Math.Min(baseMs, cap);

        // Full jitter: random(0, min(base * 2^pow, cap))
        // pow = min(5, ConsecutiveFailures - FailuresBeforeBackoff)
        // baseMs = 1000ms * 2^pow, maxDelay = min(baseMs, cap)
        // Prevents thundering herd while maintaining exponential backoff
        System.Int32 delayMs = Csprng.GetInt32(0, maxDelay + 1);

        try
        {
            await System.Threading.Tasks.Task.Delay(delayMs, ct).ConfigureAwait(false);
        }
        catch (System.OperationCanceledException) { }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int32 COUNT_RUNNING_WORKERS()

    {
        System.Int32 n = 0; foreach (System.Collections.Generic.KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            if (kv.Value.IsRunning)
            {
                n++;
            }
        }

        return n;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void RETAIN_OR_REMOVE(WorkerState st)
    {
        System.TimeSpan? keep = st.Options.RetainFor;
        if (keep is null || keep <= System.TimeSpan.Zero)
        {
            _ = _workers.TryRemove(st.Id, out _);

            try
            {
                st.Cts.Dispose();
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[FW.{nameof(TaskManager)}] retain-cts-dispose-error id={st.Id} msg={ex.Message}");
            }

            System.Boolean hasSameGroup = false;
            foreach (WorkerState other in _workers.Values)
            {
                if (System.String.Equals(other.Group, st.Group, System.StringComparison.Ordinal))
                {
                    hasSameGroup = true;
                    break;
                }
            }

            if (!hasSameGroup && _groupGates.TryRemove(st.Group, out Gate? g))
            {
                try
                {
                    g.SemaphoreSlim.Dispose();

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Debug($"[FW.{nameof(TaskManager)}] group-gate-dispose-ok group={st.Group}");
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[FW.{nameof(TaskManager)}] gate-dispose-error-retain group={st.Group} msg={ex.Message}");
                }
            }

            return;
        }
    }

    private async System.Threading.Tasks.Task MONITOR_CONCURRENCY_ASYNC(IWorkerContext ctx, System.Threading.CancellationToken ct)
    {
        TaskManagerOptions options = _options;

        // Số lần liên tiếp vượt ngưỡng trước khi hành động (hysteresis)
        const System.Int32 StreakRequired = 3;

        // Normalize threshold: config là % trên 1 core → scale lên toàn bộ core
        System.Double coreCount = System.Environment.ProcessorCount;
        System.Double threshHigh = options.ThresholdHighCpu * coreCount;
        System.Double threshLow = options.ThresholdLowCpu * coreCount;

        // Khởi tạo baseline CPU trước khi vòng lặp bắt đầu
        INITIALIZE_CPU_MEASUREMENT();

        while (!ct.IsCancellationRequested && options.DynamicAdjustmentEnabled)
        {
            try
            {
                System.Double cpuUsage = MEASURE_CPU_USAGE_PERCENT();

                if (cpuUsage > threshHigh)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Debug($"[FW.{nameof(TaskManager)}:Internal] cpu-high usage={cpuUsage:F1}% threshold={threshHigh:F1}%");
                }

                // --- Hysteresis: tích streak, chỉ hành động khi đủ N lần liên tiếp ---
                if (cpuUsage > threshHigh && _currentConcurrencyLimit > 1)
                {
                    _lowCpuStreak = 0;
                    _highCpuStreak++;

                    if (_highCpuStreak >= StreakRequired)
                    {
                        _highCpuStreak = 0; // reset sau khi hành động
                        System.Int32 newLimit = System.Math.Max(1, _currentConcurrencyLimit - 1);
                        ADJUST_CONCURRENCY(newLimit);
                    }
                }
                else if (cpuUsage < threshLow && _currentConcurrencyLimit < options.MaxWorkers)
                {
                    _highCpuStreak = 0;
                    _lowCpuStreak++;

                    if (_lowCpuStreak >= StreakRequired)
                    {
                        _lowCpuStreak = 0; // reset sau khi hành động
                        System.Int32 newLimit = System.Math.Min(options.MaxWorkers, _currentConcurrencyLimit + 1);
                        ADJUST_CONCURRENCY(newLimit);
                    }
                }
                else
                {
                    // CPU trong vùng ổn định → reset cả hai streak
                    _highCpuStreak = 0;
                    _lowCpuStreak = 0;
                }

                ctx.Beat();
                ctx.Advance(1);

                await System.Threading.Tasks.Task.Delay(options.ObservingInterval, ct);
            }
            catch (System.OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[FW.{nameof(TaskManager)}:Internal] dynamic-adjustment-error ex={ex.Message}");
            }
        }
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void INITIALIZE_CPU_MEASUREMENT()
    {
        System.Diagnostics.Process proc = System.Diagnostics.Process.GetCurrentProcess();
        System.Threading.Volatile.Write(ref _lastCpuProcessorTime, (System.Int64)proc.TotalProcessorTime.TotalMilliseconds);
        System.Threading.Volatile.Write(ref _lastCpuWallClockMs, _cpuMeasureStopwatch.ElapsedMilliseconds);
        _cpuWarmupDone = false;
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private System.Double MEASURE_CPU_USAGE_PERCENT()
    {
        System.Int64 currentWallMs = _cpuMeasureStopwatch.ElapsedMilliseconds;

        // Warmup: trong 60 giây đầu, bỏ qua và KHÔNG cập nhật baseline
        // Điều này để lần đo sau warmup tính delta đúng kể từ lúc baseline được init
        if (!_cpuWarmupDone)
        {
            if (currentWallMs < 60_000)
            {
                return 0.0;
            }

            // Đánh dấu đã xong warmup, cập nhật baseline một lần ngay lúc này
            System.Diagnostics.Process proc0 = System.Diagnostics.Process.GetCurrentProcess();
            System.Threading.Volatile.Write(ref _lastCpuProcessorTime, (System.Int64)proc0.TotalProcessorTime.TotalMilliseconds);
            System.Threading.Volatile.Write(ref _lastCpuWallClockMs, currentWallMs);
            _cpuWarmupDone = true;
            return 0.0;
        }

        System.Diagnostics.Process proc = System.Diagnostics.Process.GetCurrentProcess();
        System.Int64 currentCpuMs = (System.Int64)proc.TotalProcessorTime.TotalMilliseconds;

        System.Int64 prevWallMs = System.Threading.Volatile.Read(ref _lastCpuWallClockMs);
        System.Int64 prevCpuMs = System.Threading.Volatile.Read(ref _lastCpuProcessorTime);

        System.Int64 wallDelta = currentWallMs - prevWallMs;
        System.Int64 cpuDelta = currentCpuMs - prevCpuMs;

        // Cập nhật baseline cho lần đo tiếp theo
        System.Threading.Volatile.Write(ref _lastCpuWallClockMs, currentWallMs);
        System.Threading.Volatile.Write(ref _lastCpuProcessorTime, currentCpuMs);

        // Tránh chia cho 0 hoặc delta âm (clock skew, process refresh lag)
        if (wallDelta <= 0 || cpuDelta < 0)
        {
            return 0.0;
        }

        System.Double processorCount = System.Environment.ProcessorCount;

        // cpuDelta / wallDelta = tỷ lệ sử dụng trên 1 core → nhân processorCount → % trên toàn bộ core
        System.Double cpuUsagePercent = cpuDelta / (System.Double)wallDelta * processorCount * 100.0;

        return System.Math.Min(cpuUsagePercent, processorCount * 100.0);
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void ADJUST_CONCURRENCY(System.Int32 newLimit)
    {
        // Safety: clamp to valid range
        newLimit = System.Math.Clamp(newLimit, 1, _options.MaxWorkers);

        System.Int32 previousLimit = _currentConcurrencyLimit;

        // No change = skip
        if (previousLimit == newLimit)
        {
            return;
        }

        _currentConcurrencyLimit = newLimit;
        System.Int32 delta = newLimit - previousLimit;

        try
        {
            if (delta > 0)
            {
                // Tăng capacity: release thêm slot vào semaphore
                _globalConcurrencyGate.Release(delta);
            }
            else if (delta < 0)
            {
                // Giảm capacity: thu hồi slot bằng Wait(0) non-blocking
                // Nếu slot đang bị giữ (worker đang chạy), chấp nhận revert một phần
                // thay vì block — tránh deadlock
                System.Int32 i;
                for (i = 0; i < -delta; i++)
                {
                    if (!_globalConcurrencyGate.Wait(0))
                    {
                        // Không còn slot rảnh → revert về số đã thu hồi được thực tế
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Debug($"[FW.TaskManager.Internal] concurrency-partial-retreat from={previousLimit} to={previousLimit - i}");
                        _currentConcurrencyLimit = previousLimit - i;
                        break;
                    }
                }
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[FW.TaskManager.Internal] concurrency-limit-adjusted=[{previousLimit}->{_currentConcurrencyLimit}]");
        }
        catch (System.Exception ex)
        {
            // Revert on error
            _currentConcurrencyLimit = previousLimit;

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[FW.TaskManager.Internal] failed-adjust-concurrency ex={ex.Message} from={previousLimit} to={newLimit}");
        }
    }
}