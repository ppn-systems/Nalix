// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Diagnostics;

namespace Nalix.Framework.Tasks;

public sealed partial class TaskManager
{
    #region IDisposable

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _workerDispatcherCts.Cancel();
            _ = _pendingWorkersSignal.Release();

            if (_workerDispatcherTask.IsCompleted)
            {
                if (_workerDispatcherTask.Exception?.GetBaseException() is Exception ex)
                {
                    if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Failed))
                    {
                        Listener.Write(DiagnosticsEvents.Tasks.Failed, new DiagnosticLog("FW.TaskManager:Internal", "dispatcher-faulted", ex));
                    }
                }
            }
            else
            {
                _ = _workerDispatcherTask.ContinueWith(static (task) =>
                {
                    if (task.Exception?.GetBaseException() is Exception bgEx)
                    {
                        if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Failed))
                        {
                            Listener.Write(DiagnosticsEvents.Tasks.Failed, new DiagnosticLog("FW.TaskManager:Internal", "dispatcher-faulted-after-dispose", bgEx));
                        }
                    }
                }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Failed))
            {
                Listener.Write(DiagnosticsEvents.Tasks.Failed, new DiagnosticLog("FW.TaskManager:Internal", "dispatcher-stop-error", ex));
            }
        }

        try
        {
            _cleanupCts.Cancel();
            _cleanupPeriodicTimer.Dispose();

            // Đợi tối đa 3 giây cho cleanup loop kết thúc
            if (!_cleanupTask.IsCompleted)
            {
                _ = _cleanupTask.Wait(TimeSpan.FromSeconds(3));
            }

            _cleanupCts.Dispose();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Failed))
            {
                Listener.Write(DiagnosticsEvents.Tasks.Failed, new DiagnosticLog("FW.TaskManager:Internal", "cleanup-shutdown-error", ex));
            }
        }

        foreach (KeyValuePair<string, RecurringState> kv in _recurring)
        {
            RecurringState st = kv.Value;
            st.Cancel();

            Task? t = st.Task;
            if (t is not null)
            {
                _ = t.ContinueWith(_ =>
                    {
                        try { st.CancellationTokenSource.Dispose(); }
                        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                        {
                            if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Failed))
                            {
                                Listener.Write(DiagnosticsEvents.Tasks.Failed, new DiagnosticLog("FW.TaskManager:Internal", $"recurring-cts-dispose-error name={st.Name}", ex));
                            }
                        }
                        try { st.Gate.Dispose(); }
                        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                        {
                            if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Failed))
                            {
                                Listener.Write(DiagnosticsEvents.Tasks.Failed, new DiagnosticLog("FW.TaskManager:Internal", $"recurring-gate-dispose-error name={st.Name}", ex));
                            }
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default
                );
            }
            else
            {
                try
                {
                    st.CancellationTokenSource.Dispose();
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Failed))
                    {
                        Listener.Write(DiagnosticsEvents.Tasks.Failed, new DiagnosticLog("FW.TaskManager:Internal", $"recurring-cts-dispose-error-sync name={st.Name}", ex));
                    }
                }
                try
                {
                    st.Gate.Dispose();
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Failed))
                    {
                        Listener.Write(DiagnosticsEvents.Tasks.Failed, new DiagnosticLog("FW.TaskManager:Internal", $"recurring-gate-dispose-error-sync name={st.Name}", ex));
                    }
                }
            }
        }

        foreach (KeyValuePair<ISnowflake, WorkerState> kv in _workers)
        {
            WorkerState st = kv.Value;
            st.Cancel();

            Task? t = st.Task;
            if (t?.IsCompleted == true)
            {
                try
                {
                    st.Cts.Dispose();
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Failed))
                    {
                        Listener.Write(DiagnosticsEvents.Tasks.Failed, new DiagnosticLog("FW.TaskManager:Internal", $"worker-cts-dispose-error id={st.Id}", ex));
                    }
                }
            }
            else if (t is not null)
            {
                _ = t.ContinueWith(_ =>
                {
                    try
                    {
                        st.Cts.Dispose();
                    }
                    catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                    {
                        if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Failed))
                        {
                            Listener.Write(DiagnosticsEvents.Tasks.Failed, new DiagnosticLog("FW.TaskManager:Internal", $"worker-cts-dispose-error-async id={st.Id}", ex));
                        }
                    }
                }, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            }
            else
            {
                try
                {
                    st.Cts.Dispose();
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Failed))
                    {
                        Listener.Write(DiagnosticsEvents.Tasks.Failed, new DiagnosticLog("FW.TaskManager:Internal", $"worker-cts-dispose-error-no-task id={st.Id}", ex));
                    }
                }
            }
        }

        _recurring.Clear(); _workers.Clear();

        foreach (KeyValuePair<string, Gate> g in _groupGates)
        {
            try
            {
                g.Value.SemaphoreSlim.Dispose();
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Failed))
                {
                    Listener.Write(DiagnosticsEvents.Tasks.Failed, new DiagnosticLog("FW.TaskManager:Internal", $"gate-dispose-error group={g.Key}", ex));
                }
            }
        }

        _groupGates.Clear();

        try
        {
            _pendingWorkersSignal.Dispose();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Failed))
            {
                Listener.Write(DiagnosticsEvents.Tasks.Failed, new DiagnosticLog("FW.TaskManager:Internal", "pending-signal-dispose-error", ex));
            }
        }

        try
        {
            _globalConcurrencyGate.Dispose();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Failed))
            {
                Listener.Write(DiagnosticsEvents.Tasks.Failed, new DiagnosticLog("FW.TaskManager:Internal", "global-gate-dispose-error", ex));
            }
        }

        try
        {
            _workerDispatcherCts.Dispose();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Failed))
            {
                Listener.Write(DiagnosticsEvents.Tasks.Failed, new DiagnosticLog("FW.TaskManager:Internal", "dispatcher-cts-dispose-error", ex));
            }
        }

        if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Disposed))
        {
            Listener.Write(DiagnosticsEvents.Tasks.Disposed, new DiagnosticLog("FW.TaskManager:Internal", "disposed"));
        }

        GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}

