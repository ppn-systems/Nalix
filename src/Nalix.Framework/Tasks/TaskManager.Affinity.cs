// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;

namespace Nalix.Framework.Tasks;

public partial class TaskManager
{
    /// <summary>
    /// Pins the current thread to the specified CPU core.
    /// Falls back gracefully if the core index is invalid or the OS is unsupported.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SET_THREAD_AFFINITY(int coreIndex, string workerName)
    {
        int processorCount = System.Environment.ProcessorCount;
        if (coreIndex < 0 || coreIndex >= processorCount)
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Tasks.Dispatcher))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Tasks.Dispatcher,
                    new DiagnosticLog("FW.TaskManager:Internal",
                        $"affinity-skip name={workerName} core={coreIndex} max={processorCount}"));
            }
            return;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                nint mask = (nint)(1L << coreIndex);
                nint handle = GetCurrentThread();
                nint prev = SetThreadAffinityMask(handle, mask);

                if (prev == 0)
                {
                    int error = Marshal.GetLastPInvokeError();
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Tasks.Failed))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Tasks.Failed,
                            new DiagnosticLog("FW.TaskManager:Internal",
                                $"affinity-failed name={workerName} core={coreIndex} error={error}"));
                    }
                }
                else if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Tasks.Dispatcher))
                {
                    DiagnosticsEvents.Write(DiagnosticsEvents.Tasks.Dispatcher,
                        new DiagnosticLog("FW.TaskManager:Internal",
                            $"affinity-set name={workerName} core={coreIndex}"));
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                // sched_setaffinity expects the mask passed by reference, size in bytes.
                ulong mask = 1UL << coreIndex;
                int result = sched_setaffinity(0, (nuint)sizeof(ulong), ref mask);

                if (result != 0)
                {
                    int error = Marshal.GetLastPInvokeError();
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Tasks.Failed))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Tasks.Failed,
                            new DiagnosticLog("FW.TaskManager:Internal",
                                $"affinity-failed name={workerName} core={coreIndex} error={error}"));
                    }
                }
                else if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Tasks.Dispatcher))
                {
                    DiagnosticsEvents.Write(DiagnosticsEvents.Tasks.Dispatcher,
                        new DiagnosticLog("FW.TaskManager:Internal",
                            $"affinity-set name={workerName} core={coreIndex}"));
                }
            }
            else
            {
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Tasks.Dispatcher))
                {
                    DiagnosticsEvents.Write(DiagnosticsEvents.Tasks.Dispatcher,
                        new DiagnosticLog("FW.TaskManager:Internal",
                            $"affinity-skip-os name={workerName} os=unsupported"));
                }
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Tasks.Failed))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Tasks.Failed,
                    new DiagnosticLog("FW.TaskManager:Internal",
                        $"affinity-error name={workerName} core={coreIndex}", ex));
            }
        }
    }

    // Windows P/Invoke
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint SetThreadAffinityMask(nint hThread, nint dwThreadAffinityMask);

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentThread();

    // Linux P/Invoke
    [LibraryImport("libc", SetLastError = true)]
    private static partial int sched_setaffinity(int pid, nuint cpusetsize, ref ulong mask);
}
