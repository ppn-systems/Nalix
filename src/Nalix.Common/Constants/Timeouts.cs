namespace Nalix.Common.Constants;

/// <summary>
/// Defines standard packet timeouts for different use cases to ensure responsiveness and prevent resource abuse.
/// </summary>
public static class Timeouts
{
    /// <summary>
    /// For ultra-fast control packets (e.g., ping, small config).
    /// </summary>
    public const System.Int32 Instant = 500;

    /// <summary>
    /// For lightweight operations such as setting connection options.
    /// </summary>
    public const System.Int32 Short = 1000;

    /// <summary>
    /// Default timeout for general-purpose packets.
    /// </summary>
    public const System.Int32 Default = 5000;

    /// <summary>
    /// For operations that may take moderate time (e.g., compression, key exchange).
    /// </summary>
    public const System.Int32 Moderate = 8000;

    /// <summary>
    /// For long-running tasks (e.g., database query, file processing).
    /// </summary>
    public const System.Int32 Long = 15000;

    /// <summary>
    /// For very long-running operations (e.g., file upload, sync).
    /// </summary>
    public const System.Int32 Extended = 30000;
}
