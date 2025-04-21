namespace Nalix.Logging.Options;

/// <summary>
/// Provides configuration options for ConsoleLogTarget.
/// </summary>
public sealed class ConsoleLogOptions
{
    /// <summary>
    /// Determines whether log messages should use color.
    /// </summary>
    public bool EnableColors { get; init; } = true;

    /// <summary>
    /// Determines whether log messages should be written to standard error (stderr).
    /// Default is false (logs to stdout).
    /// </summary>
    public bool UseStandardError { get; init; } = false;
}
