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

        /// <summary>
        /// Fired when a DataReader is poisoned due to malformed or out-of-bounds data.
        /// </summary>
        public const string Poisoned = "Serialization.Poisoned";

        /// <summary>
        /// Fired when the serialization system is initialized.
        /// </summary>
        public const string Initialization = "Serialization.Initialization";
    }

    /// <summary>
    /// Packet processing related diagnostic events.
    /// </summary>
    public static class Packet
    {
        /// <summary>
        /// Fired when a packet payload is malformed, truncated, or fails deserialization validation.
        /// </summary>
        public const string Malformed = "Packet.Malformed";
    }

    /// <summary>
    /// Sequence-counter related diagnostic events.
    /// </summary>
    public static class Sequence
    {
        /// <summary>
        /// Fired when an inbound sequence number is rejected by <c>ISequenceCounter.IsValid</c>
        /// (out-of-window or replayed). Observational only; does not affect the accept/reject decision.
        /// </summary>
        public const string Rejected = "Sequence.Rejected";
    }
}
