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
