// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Nalix.Runtime;

/// <summary>
/// Provides the diagnostic event source and event-name registry for the
/// <c>Nalix.Runtime</c> module.
/// </summary>
/// <remarks>
/// Runtime components should emit events through <see cref="Source"/> instead
/// of depending directly on a logging abstraction. Host/runtime layers may
/// subscribe to this listener and bridge events to logging, metrics, tracing,
/// or telemetry systems.
/// </remarks>
public static class DiagnosticsEvents
{
    /// <summary>
    /// The diagnostic listener name used by the Runtime module.
    /// </summary>
    public const string ListenerName = "Runtime";

    /// <summary>
    /// The shared diagnostic listener used to publish Runtime diagnostic events.
    /// </summary>
    /// <remarks>
    /// Hot paths should always call <see cref="DiagnosticListener.IsEnabled(string)"/>
    /// before allocating event payload objects.
    /// </remarks>
    public static readonly DiagnosticListener Source =
        Environment.Diagnostics.DiagnosticListenerFactory.Create(ListenerName);

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
    /// Diagnostic event names for internal Runtime module faults, warnings, and traces.
    /// </summary>
    /// <remarks>
    /// These events are intended for infrastructure diagnostics and should not
    /// expose raw packet payloads, secrets, keys, tokens, or sensitive user data.
    /// </remarks>
    public static class Internal
    {
        /// <summary>
        /// Raised for internal trace-level diagnostic messages.
        /// </summary>
        public const string Trace = "Internal.Trace";

        /// <summary>
        /// Raised for internal debug-level diagnostic messages.
        /// </summary>
        public const string Debug = "Internal.Debug";

        /// <summary>
        /// Raised for internal informational diagnostic messages.
        /// </summary>
        public const string Information = "Internal.Information";

        /// <summary>
        /// Raised for internal warning-level diagnostic messages.
        /// </summary>
        public const string Warning = "Internal.Warning";

        /// <summary>
        /// Raised for internal error-level diagnostic messages.
        /// </summary>
        public const string Error = "Internal.Error";

        /// <summary>
        /// Raised for internal critical diagnostic messages.
        /// </summary>
        public const string Critical = "Internal.Critical";
    }
}
