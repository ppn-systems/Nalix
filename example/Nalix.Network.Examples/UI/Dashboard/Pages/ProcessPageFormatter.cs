// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Network.Connections;

namespace Nalix.Network.Examples.UI.Dashboard.Pages;

/// <summary>Formats the Process info page (runtime, memory, threading).</summary>
internal sealed class ProcessPageFormatter : IPageFormatter
{
    public string Label => "Process";

    public string Format(ConnectionHub hub)
    {
        var proc = System.Diagnostics.Process.GetCurrentProcess();
        proc.Refresh();
        ThreadPool.GetMaxThreads(out int maxW, out int maxIO);
        ThreadPool.GetAvailableThreads(out int freeW, out int freeIO);

        return $"""
            PID              : {proc.Id}
            Uptime           : {FormatUptime()}
            Started          : {proc.StartTime:yyyy-MM-dd HH:mm:ss}
            
            Memory:
            Working Set      : {proc.WorkingSet64 / 1024 / 1024:F1} MB
            Private Bytes    : {proc.PrivateMemorySize64 / 1024 / 1024:F1} MB
            Virtual Memory   : {proc.VirtualMemorySize64 / 1024 / 1024:F1} MB
            GC Managed Heap  : {GC.GetTotalMemory(false) / 1024 / 1024:F1} MB
            GC Gen0/1/2      : {GC.CollectionCount(0)} / {GC.CollectionCount(1)} / {GC.CollectionCount(2)}
            
            Threading:
            Proc Threads     : {proc.Threads.Count}
            ThreadPool W     : {maxW - freeW} / {maxW} active
            ThreadPool IO    : {maxIO - freeIO} / {maxIO} active
            Completed Items  : {ThreadPool.CompletedWorkItemCount:N0}
            CPU Time         : {proc.TotalProcessorTime.TotalSeconds:F2}s
            
            Runtime:
            Framework        : {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}
            OS               : {System.Runtime.InteropServices.RuntimeInformation.OSDescription.Trim()}
            Arch             : {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}
            Processors       : {System.Environment.ProcessorCount}
            """;
    }

    private static string FormatUptime()
    {
        TimeSpan up = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime;
        return up.TotalHours >= 1
            ? $"{(int)up.TotalHours}h {up.Minutes}m {up.Seconds}s"
            : up.TotalMinutes >= 1
                ? $"{up.Minutes}m {up.Seconds}s"
                : $"{up.Seconds}s";
    }
}
