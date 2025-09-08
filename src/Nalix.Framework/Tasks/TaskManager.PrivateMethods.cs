// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Framework.Randomization;

namespace Nalix.Framework.Tasks;

public partial class TaskManager
{
    private async System.Threading.Tasks.Task RecurringLoopAsync(
        RecurringState s,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.ValueTask> work)
    {
        var ct = s.Cts.Token;

        // jitter
        if (s.Options.Jitter is { } j && j > System.TimeSpan.Zero)
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(SecureRandom.GetInt32(0, (System.Int32)j.TotalMilliseconds), ct)
                                                 .ConfigureAwait(false);
            }
            catch (System.OperationCanceledException)
            {
                return;
            }
        }

        System.Double freq = System.Diagnostics.Stopwatch.Frequency;
        System.Int64 step = (System.Int64)(s.Interval.TotalSeconds * freq);
        if (step <= 0)
        {
            step = 1;
        }

        System.Int64 next = System.Diagnostics.Stopwatch.GetTimestamp() + step;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                System.Int64 now = System.Diagnostics.Stopwatch.GetTimestamp();
                System.Int64 delayTicks = next - now;
                if (delayTicks > 0)
                {
                    System.Int32 ms = (System.Int32)(delayTicks * 1000 / freq);
                    if (ms > 1)
                    {
                        await System.Threading.Tasks.Task.Delay(ms, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await System.Threading.Tasks.Task.Yield();
                    }
                }
                else
                {
                    System.Int64 missed = (-delayTicks + step - 1) / step;
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

                    if (s.Options.RunTimeout is { } to && to > System.TimeSpan.Zero)
                    {
                        using var rcts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);
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
                                            .Error($"[{nameof(TaskManager)}] recurring-timeout name={s.Name} msg={oce.Message}");

                    await RecurringBackoffAsync(s, ct).ConfigureAwait(false);
                }
                catch (System.Exception ex)
                {
                    s.MarkFailure();
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[{nameof(TaskManager)}] recurring-error name={s.Name} msg={ex.Message}");

                    await RecurringBackoffAsync(s, ct).ConfigureAwait(false);
                }
                finally
                {
                    if (s.Options.NonReentrant)
                    {
                        _ = s.Gate.Release();
                    }

                    next += step;
                }
            }
            catch (System.OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (System.Exception ex)
            {
                s.MarkFailure();
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(TaskManager)}] recurring-loop-error name={s.Name} msg={ex.Message}");

                await RecurringBackoffAsync(s, ct).ConfigureAwait(false);
            }
        }
    }

    private static async System.Threading.Tasks.ValueTask RecurringBackoffAsync(
        RecurringState s,
        System.Threading.CancellationToken ct)
    {
        System.Int32 n = System.Math.Max(1, s.Options.MaxFailuresBeforeBackoff);
        if (s.ConsecutiveFailures < n)
        {
            return;
        }

        System.Int32 pow = System.Math.Min(5, s.ConsecutiveFailures - n); // cap 32s
        System.Int32 ms = 1000 << pow;
        System.Int32 cap = (System.Int32)System.Math.Max(1, s.Options.MaxBackoff.TotalMilliseconds);
        if (ms > cap)
        {
            ms = cap;
        }

        try
        {
            await System.Threading.Tasks.Task.Delay(ms, ct).ConfigureAwait(false);
        }
        catch (System.OperationCanceledException) { }
    }

    private System.Int32 CountRunningWorkers()
    {
        System.Int32 n = 0; foreach (var kv in _workers)
        {
            if (kv.Value.IsRunning)
            {
                n++;
            }
        }

        return n;
    }

    private void RetainOrRemove(WorkerState st)
    {
        var keep = st.Options.Retention;
        if (keep is null || keep <= System.TimeSpan.Zero)
        {
            _ = _workers.TryRemove(st.Id, out _);
            return;
        }

        // schedule delayed removal
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(keep.Value).ConfigureAwait(false);
            }
            catch { }
            _ = _workers.TryRemove(st.Id, out _);
        });
    }
}
