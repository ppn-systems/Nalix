// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.Logging;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Defines a contract for a log processing target.
/// </summary>
/// <remarks>
/// Implementations of this interface determine how and where log entries are stored or displayed.
/// Examples include writing logs to a file, sending them to a remote server, or displaying them in the console.
/// </remarks>
public interface INLogixTarget
{
    /// <summary>
    /// Publishes a log entry to the configured log target.
    /// </summary>
    void Publish(
        DateTime timestampUtc,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception);
}
