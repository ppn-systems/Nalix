namespace Nalix.Common.Constants;

/// <summary>
/// Defines standard packet timeouts for different use cases to ensure responsiveness and prevent resource abuse.
/// </summary>
public static class Timeouts
{
    /// <summary>
    /// For ultra-fast control packets (e.g., ping, small config).
    /// </summary>
    public const ushort Instant = 500;

    /// <summary>
    /// For lightweight operations such as setting connection options.
    /// </summary>
    public const ushort Short = 1000;

    /// <summary>
    /// Default timeout for general-purpose packets.
    /// </summary>
    public const ushort Default = 5000;

    /// <summary>
    /// For operations that may take moderate time (e.g., compression, key exchange).
    /// </summary>
    public const ushort Moderate = 8000;

    /// <summary>
    /// For long-running tasks (e.g., database query, file processing).
    /// </summary>
    public const ushort Long = 15000;

    /// <summary>
    /// For very long-running operations (e.g., file upload, sync).
    /// </summary>
    public const ushort Extended = 30000;
}
