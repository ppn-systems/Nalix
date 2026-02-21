// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Diagnostics;
using Nalix.Framework.Injection;
using Nalix.Framework.Random;

namespace Nalix.Framework.Tasks;

public partial class TaskManager
{
    #region Types

    private sealed record Gate(System.Threading.SemaphoreSlim SemaphoreSlim, System.Int32 Capacity);

    #endregion Types

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

        try
        {
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

                System.DateTimeOffset? completed = st.CompletedUtc;
                if (completed is null)
                {
                    continue;
                }

                if (now - completed.Value < keep.Value)
                {
                    continue;
                }

                if (_workers.TryRemove(st.Id, out _))
                {
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
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[FW.{nameof(TaskManager)}] cleanup-error msg={ex.Message}");
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
                System.Int64 now = System.Diagnostics.Stopwatch.GetTimestamp();
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
                        await System.Threading.Tasks.Task.Delay(ts, ct).ConfigureAwait(false);
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
}
