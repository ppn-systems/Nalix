// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Logging.Models;
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
/// The <see cref="NLogix"/> logger supports dependency injection or can be accessed via <see cref="NLogix.Host"/>.
/// Logging targets and behavior can be customized during initialization using <see cref="LogOptions"/>.
/// </remarks>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Logger=NLogix, {GetType().Name,nq}")]
public sealed partial class NLogix : LogEngine, ILogger
{
    #region Constructors

    /// <summary>
    /// Initializes the logging system with optional configuration.
    /// </summary>
    /// <param name="configure">An optional action to configure the logging system.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public NLogix(System.Action<LogOptions>? configure = null)
        : base(configure)
    {
    }

    #endregion Constructors

    #region Private Methods

    // Sanitize log message to prevent log forging
    // Removes potentially dangerous characters (e.g., newlines or control characters)
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.String SanitizeLogMessage(System.String? message)
        => message?.Replace("\n", "").Replace("\r", "") ?? System.String.Empty;

    // Writes a log entry with the specified level, event ProtocolType, message, and optional exception.
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void WriteLog(
        LogLevel level, EventId eventId,
        System.String message, System.Exception? exception = null)
       => base.CreateLogEntry(level, eventId, message, exception);

    #endregion Private Methods
}
