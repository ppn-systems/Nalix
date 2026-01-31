// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Nalix.Logging;

public sealed partial class NLogix
{
    public static class Events
    {

        // Generic/other (0-99)
        public static readonly EventId UnknownError = new(0, "UnknownError");

        // Worker events (100-199)
        public static readonly EventId WorkerStarted = new(100, "WorkerStarted");
        public static readonly EventId WorkerCompleted = new(101, "WorkerCompleted");
        public static readonly EventId WorkerCancelled = new(102, "WorkerCancelled");
        public static readonly EventId WorkerError = new(110, "WorkerError");
        public static readonly EventId WorkerCallbackError = new(111, "WorkerCallbackError");
        public static readonly EventId WorkerRejectGroupCap = new(112, "WorkerRejectedGroupCapacity");
        public static readonly EventId WorkerDisposeError = new(113, "WorkerDisposeError");

        // Recurring events (200-299)
        public static readonly EventId RecurringStarted = new(200, "RecurringStarted");
        public static readonly EventId RecurringError = new(210, "RecurringError");
        public static readonly EventId RecurringCancelled = new(211, "RecurringCancelled");
        public static readonly EventId RecurringCallbackError = new(212, "RecurringCallbackError");
        public static readonly EventId RecurringDisposeError = new(213, "RecurringDisposeError");

        // Resource and gate events (300-399)
        public static readonly EventId GateReleaseError = new(300, "GateReleaseError");
        public static readonly EventId GateDisposeError = new(301, "GateDisposeError");

        // Manager lifecycle (400-499)
        public static readonly EventId CleanupStarted = new(400, "CleanupStarted");
        public static readonly EventId CleanupDisposed = new(401, "CleanupDisposed");
        public static readonly EventId DisposeError = new(402, "DisposeError");
        public static readonly EventId Init = new(410, "Initialized");

        // Monitoring (500-599)
        public static readonly EventId MonitorConcurrency = new(500, "MonitorConcurrency");
        public static readonly EventId ReportGenerated = new(510, "ReportGenerated");

        // ConfigurationManager events (3000–3099)
        public static readonly EventId ConfigManagerInitialized = new(3000, "ConfigManagerInitialized");
        public static readonly EventId ConfigManagerDisposed = new(3001, "ConfigManagerDisposed");
        public static readonly EventId ConfigManagerConfigPathChanged = new(3010, "ConfigManagerConfigPathChanged");
        public static readonly EventId ConfigManagerInvalidConfigPath = new(3011, "ConfigManagerInvalidConfigPath");
        public static readonly EventId ConfigManagerConfigFileNotFound = new(3012, "ConfigManagerConfigFileNotFound");

        public static readonly EventId ConfigManagerReloadAll = new(3020, "ConfigManagerReloadAll");
        public static readonly EventId ConfigManagerReloadError = new(3021, "ConfigManagerReloadError");

        public static readonly EventId ConfigManagerFlush = new(3030, "ConfigManagerFlush");
        public static readonly EventId ConfigManagerFlushError = new(3031, "ConfigManagerFlushError");

        public static readonly EventId ConfigManagerFileWatcherCreated = new(3040, "ConfigManagerFileWatcherCreated");
        public static readonly EventId ConfigManagerFileChanged = new(3041, "ConfigManagerFileChanged");
        public static readonly EventId ConfigManagerFileWatcherError = new(3042, "ConfigManagerFileWatcherError");

        public static readonly EventId ConfigManagerContainerCreated = new(3050, "ConfigManagerContainerCreated");
        public static readonly EventId ConfigManagerContainerReloaded = new(3051, "ConfigManagerContainerReloaded");
        public static readonly EventId ConfigManagerContainerRemoved = new(3052, "ConfigManagerContainerRemoved");
        public static readonly EventId ConfigManagerContainerCleared = new(3053, "ConfigManagerContainerCleared");
    }
}
