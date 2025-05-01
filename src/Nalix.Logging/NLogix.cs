using Nalix.Common.Logging;
using Nalix.Logging.Engine;
using Nalix.Logging.Options;

namespace Nalix.Logging;

/// <summary>
/// Provides a high-performance, extensible logging engine for applications,
/// combining structured logging and customizable output targets.
///
/// This class is the core of the Nalix logging system, and implements <see cref="ILogger"/> for unified logging.
/// Use this logger to write diagnostic messages, errors, warnings, or audit logs across the application.
/// </summary>
/// <remarks>
/// The <see cref="NLogix"/> logger supports dependency injection or can be accessed via <see cref="NLogix.Host"/>.
/// Logging targets and behavior can be customized during initialization using <see cref="NLogOptions"/>.
/// </remarks>
public sealed partial class NLogix : LogEngine, ILogger
{
    #region Constructors

    /// <summary>
    /// Initializes the logging system with optional configuration.
    /// </summary>
    /// <param name="configure">An optional action to configure the logging system.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public NLogix(System.Action<NLogOptions>? configure = null)
        : base(configure)
    {
    }

    #endregion Constructors

    #region Private Methods

    // Sanitize log message to prevent log forging
    // Removes potentially dangerous characters (e.g., newlines or control characters)
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static string SanitizeLogMessage(string? message)
        => message?.Replace("\n", "").Replace("\r", "") ?? string.Empty;

    // Writes a log entry with the specified level, event Number, message, and optional exception.
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void WriteLog(LogLevel level, EventId eventId, string message, System.Exception? exception = null)
       => base.CreateLogEntry(level, eventId, message, exception);

    #endregion Private Methods
}
