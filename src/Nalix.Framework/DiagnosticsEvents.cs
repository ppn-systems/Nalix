// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Nalix.Framework;

/// <summary>
/// Central registry for all diagnostic event names used within the Framework module.
/// 
/// Design goals:
/// - Provide strongly-typed event names (avoid magic strings)
/// - Keep naming consistent across modules
/// - Reduce duplication of prefixes (namespace already scopes context)
/// </summary>
public static class DiagnosticsEvents
{
    /// <summary>
    /// The name of the <see cref="DiagnosticListener"/> used by the Framework module.
    /// Consumers can subscribe to this listener to observe all emitted events.
    /// </summary>
    public const string ListenerName = "Framework";

    /// <summary>
    /// Global diagnostic source for emitting events.
    /// </summary>
    public static readonly DiagnosticListener Source = Environment.Diagnostics.DiagnosticListenerFactory.Create(ListenerName);

    /// <summary>
    /// Writes a diagnostic event payload through <see cref="Source"/> in an AOT-safe manner.
    /// </summary>
    /// <typeparam name="T">The diagnostic payload type.</typeparam>
    /// <param name="name">The event name.</param>
    /// <param name="payload">The event payload.</param>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "DiagnosticSource.Write<T>() requires unreferenced-code analysis for payload property discovery. " +
            "Nalix diagnostic payloads are observational only and do not affect runtime behavior.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2091",
        Justification = "Diagnostic payloads are observational only; public-property preservation is not required for runtime behavior.")]
    public static void Write<T>(string name, T payload) => Source.Write(name, payload);

    /// <summary>
    /// Task-related diagnostic events.
    /// </summary>
    public static class Tasks
    {
        /// <summary>
        /// Fired when a worker task is started.
        /// </summary>
        public const string Started = "Tasks.WorkerStarted";

        /// <summary>
        /// Fired when a worker task completes successfully.
        /// </summary>
        public const string Completed = "Tasks.WorkerCompleted";

        /// <summary>
        /// Fired when a worker task fails with an exception.
        /// </summary>
        public const string Failed = "Tasks.WorkerFailed";

        /// <summary>
        /// Fired when a worker task is cancelled.
        /// </summary>
        public const string Cancelled = "Tasks.WorkerCancelled";

        /// <summary>
        /// Fired when a recurring job is executed.
        /// </summary>
        public const string RecurringExecuted = "Tasks.RecurringExecuted";

        /// <summary>
        /// Fired when the dispatcher starts or stops.
        /// </summary>
        public const string Dispatcher = "Tasks.Dispatcher";

        /// <summary>
        /// Fired when a worker is disposed or cleaned up.
        /// </summary>
        public const string Disposed = "Tasks.WorkerDisposed";
    }

    /// <summary>
    /// Memory and object pool diagnostic events.
    /// </summary>
    public static class Memory
    {
        /// <summary>
        /// Fired when a pool is expanded (capacity increased).
        /// </summary>
        public const string PoolExpanded = "Memory.PoolExpanded";

        /// <summary>
        /// Fired when a pool is trimmed (unused objects released).
        /// </summary>
        public const string PoolTrimmed = "Memory.PoolTrimmed";

        /// <summary>
        /// Fired when an object is returned to the pool.
        /// </summary>
        public const string PoolReturned = "Memory.PoolReturned";

        /// <summary>
        /// Fired when the pool encounters a failure (e.g. validation error).
        /// </summary>
        public const string PoolFailure = "Memory.PoolFailure";

        /// <summary>
        /// Fired when a buffer is allocated from the buffer pool.
        /// </summary>
        public const string BufferAllocated = "Memory.BufferAllocated";

        /// <summary>
        /// Fired when a buffer is released back to the pool.
        /// </summary>
        public const string BufferReleased = "Memory.BufferReleased";

        /// <summary>
        /// Fired when a sentinel detects a leak or lifecycle issue.
        /// </summary>
        public const string SentinelWarning = "Memory.SentinelWarning";
    }

    /// <summary>
    /// Dependency injection and instance management diagnostic events.
    /// </summary>
    public static class Injection
    {
        /// <summary>
        /// Fired when an instance is registered into the container.
        /// </summary>
        public const string Registered = "Injection.Registered";

        /// <summary>
        /// Fired when an instance is resolved from the container.
        /// </summary>
        public const string Resolved = "Injection.Resolved";

        /// <summary>
        /// Fired when instance registration fails or throws.
        /// </summary>
        public const string Failure = "Injection.Failure";
    }
}
