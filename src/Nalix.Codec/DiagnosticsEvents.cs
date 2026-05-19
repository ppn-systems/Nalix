// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Nalix.Codec;

/// <summary>
/// Central registry for all diagnostic event names used within the Codec module.
/// </summary>
public static class DiagnosticsEvents
{
    /// <summary>
    /// The name of the <see cref="DiagnosticListener"/> used by the Codec module.
    /// </summary>
    public const string ListenerName = "Codec";

    /// <summary>
    /// Global diagnostic source for emitting events.
    /// </summary>
    public static readonly DiagnosticListener Source = Environment.Diagnostics.DiagnosticListenerFactory.Create(ListenerName);

    /// <summary>
    /// Serialization and deserialization related diagnostic events.
    /// </summary>
    public static class Serialization
    {
        /// <summary>
        /// Fired when a formatter is registered.
        /// </summary>
        public const string FormatterRegistered = "Serialization.FormatterRegistered";

        /// <summary>
        /// Fired when serialization or deserialization encounters a failure.
        /// </summary>
        public const string Failure = "Serialization.Failure";
    }
}
