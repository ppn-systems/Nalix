// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;

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
                        Listener.Write(DiagnosticsEvents.Tasks.Failed, new { Action = "DispatcherFaulted", Exception = ex.Message });
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
                            Listener.Write(DiagnosticsEvents.Tasks.Failed, new { Action = "DispatcherFaultedAfterDispose", Exception = bgEx.Message });
                        }
                    }
                }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Failed))
            {
                Listener.Write(DiagnosticsEvents.Tasks.Failed, new { Action = "DispatcherStopError", Exception = ex.Message });
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
                Listener.Write(DiagnosticsEvents.Tasks.Failed, new { Action = "CleanupShutdownError", Exception = ex.Message });
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
                                Listener.Write(DiagnosticsEvents.Tasks.Failed, new { Action = "RecurringCtsDisposeError", st.Name, Exception = ex.Message });
                            }
                        }
                        try { st.Gate.Dispose(); }
                        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                        {
                            if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Failed))
                            {
                                Listener.Write(DiagnosticsEvents.Tasks.Failed, new { Action = "RecurringGateDisposeError", st.Name, Exception = ex.Message });
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
                        Listener.Write(DiagnosticsEvents.Tasks.Failed, new { Action = "RecurringCtsDisposeErrorSync", st.Name, Exception = ex.Message });
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
                        Listener.Write(DiagnosticsEvents.Tasks.Failed, new { Action = "RecurringGateDisposeErrorSync", st.Name, Exception = ex.Message });
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
                        Listener.Write(DiagnosticsEvents.Tasks.Failed, new { Action = "WorkerCtsDisposeError", st.Id, Exception = ex.Message });
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
                            Listener.Write(DiagnosticsEvents.Tasks.Failed, new { Action = "WorkerCtsDisposeErrorAsync", st.Id, Exception = ex.Message });
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
                        Listener.Write(DiagnosticsEvents.Tasks.Failed, new { Action = "WorkerCtsDisposeErrorNoTask", st.Id, Exception = ex.Message });
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
                    Listener.Write(DiagnosticsEvents.Tasks.Failed, new { Action = "GateDisposeError", Group = g.Key, Exception = ex.Message });
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
                Listener.Write(DiagnosticsEvents.Tasks.Failed, new { Action = "PendingSignalDisposeError", Exception = ex.Message });
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
                Listener.Write(DiagnosticsEvents.Tasks.Failed, new { Action = "GlobalGateDisposeError", Exception = ex.Message });
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
                Listener.Write(DiagnosticsEvents.Tasks.Failed, new { Action = "DispatcherCtsDisposeError", Exception = ex.Message });
            }
        }

        if (Listener.IsEnabled(DiagnosticsEvents.Tasks.Disposed))
        {
            Listener.Write(DiagnosticsEvents.Tasks.Disposed, new { });
        }

        GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}

