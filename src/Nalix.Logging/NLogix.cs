// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Nalix.Common.Diagnostics;
using Nalix.Logging.Configuration;
using Nalix.Logging.Engine;

namespace Nalix.Logging;

/// <summary>
/// <para>
/// Provides a high-performance, extensible logging engine for applications,
/// combining structured logging and customizable output targets.
/// </para>
/// <para>
/// This class is the core of the Nalix logging system, and implements <see cref="ILogger"/> for unified logging.
/// Use this logger to write diagnostic messages, errors, warnings, or audit logs across the application.
/// </para>
/// </summary>
/// <remarks>
/// The <see cref="NLogix"/> logger supports dependency injection or can be accessed via <see cref="Host"/>.
/// Logging targets and behavior can be customized during initialization using <see cref="NLogixOptions"/>.
/// </remarks>
[DebuggerNonUserCode]
[ExcludeFromCodeCoverage]
[DebuggerDisplay("Logger=NLogix, {GetType().Name,nq}")]
public sealed partial class NLogix : NLogixEngine, ILogger
{
    #region Constructors

    /// <summary>
    /// Initializes the logging system with optional configuration.
    /// </summary>
    /// <param name="configure">An optional action to configure the logging system.</param>
    [SuppressMessage(
        "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public NLogix(Action<NLogixOptions>? configure = null)
        : base(configure)
    {
    }

    #endregion Constructors

    #region Private Methods

    /// <summary>
    /// Sanitize log message to prevent log forging
    /// Removes potentially dangerous characters (e.g., newlines or control characters)
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string SanitizeLogMessage(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        // Chỉ allocate nếu thực sự có ký tự cần xóa
        if (MemoryExtensions.IndexOfAny(MemoryExtensions.AsSpan(message), '\n', '\r') < 0)
        {
            return message; // fast path: không cần sanitize
        }

        return message.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
    }

    /// <summary>
    /// Writes a log entry with the specified level, event ProtocolType, message, and optional exception.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WriteLog(
        LogLevel level,
        EventId eventId,
        string message,
        Exception? exception = null) => this.Publish(level, eventId, message, exception);

    #endregion Private Methods
}
