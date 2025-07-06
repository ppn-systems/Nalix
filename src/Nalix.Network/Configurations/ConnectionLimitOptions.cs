using Nalix.Common.Security;
using Nalix.Network.Security.Settings;
using Nalix.Shared.Configuration.Attributes;
using Nalix.Shared.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Configuration for limiting the Number of concurrent connections per IP address.
/// This configuration helps manage and control the Number of simultaneous connections from each IP.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ConnectionLimitOptions"/> class with the specified connection limit settings.
/// </remarks>
/// <param name="settings">The connection limit settings to apply.</param>
public sealed class ConnectionLimitOptions(ConnectionLimitSettings settings) : ConfigurationLoader
{
    #region Predefined Connection Limit Settings

    // Pre-defined connection limits to avoid repetitive calculations
    private static readonly ConnectionLimitSettings LowSettings = new(20, 30_000, 120_000);

    private static readonly ConnectionLimitSettings MediumSettings = new(100, 60_000, 300_000);
    private static readonly ConnectionLimitSettings HighSettings = new(500, 120_000, 600_000);
    private static readonly ConnectionLimitSettings UnlimitedSettings = new(10_000, 300_000, 1_800_000);

    private static readonly System.Collections.Generic.Dictionary<ConnectionLimitType, ConnectionLimitSettings>
        SettingsMap = new()
        {
            { ConnectionLimitType.Low, LowSettings },
            { ConnectionLimitType.Medium, MediumSettings },
            { ConnectionLimitType.High, HighSettings },
            { ConnectionLimitType.Unlimited, UnlimitedSettings }
        };

    #endregion Predefined Connection Limit Settings

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionLimitOptions"/> class with a specified connection limit.
    /// </summary>
    /// <param name="limit">The predefined connection limit to apply.</param>
    public ConnectionLimitOptions(ConnectionLimitType limit)
        : this(GetConnectionSettings(limit))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionLimitOptions"/> class with a default connection limit of <see cref="ConnectionLimitType.Medium"/> .
    /// </summary>
    public ConnectionLimitOptions()
        : this(ConnectionLimitType.Medium)
    {
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Gets or sets the maximum Number of connections allowed per IP address.
    /// </summary>
    /// <remarks>
    /// This value is limited to a range of 1 to 10000, where 100 is the default value.
    /// The configuration defines how many connections a single IP address can maintain simultaneously.
    /// </remarks>
    public int MaxConnectionsPerIpAddress { get; set; } = settings.MaxConnections;

    /// <summary>
    /// Gets or sets the interval in milliseconds between cleanup operations.
    /// </summary>
    public int CleanupIntervalMs { get; set; } = settings.CleanupIntervalMs;

    /// <summary>
    /// Gets or sets the threshold in milliseconds after which an inactive connection entry is considered stale.
    /// </summary>
    public int InactivityThresholdMs { get; set; } = settings.InactivityThresholdMs;

    /// <summary>
    /// Gets the cleanup interval as a TimeSpan.
    /// </summary>
    [ConfiguredIgnore]
    public System.TimeSpan CleanupInterval => System.TimeSpan.FromMilliseconds(CleanupIntervalMs);

    /// <summary>
    /// Gets the inactivity threshold as a TimeSpan.
    /// </summary>
    [ConfiguredIgnore]
    public System.TimeSpan InactivityThreshold => System.TimeSpan.FromMilliseconds(InactivityThresholdMs);

    #endregion Properties

    #region Private Methods

    /// <summary>
    /// Gets predefined settings for a connection limit level.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static ConnectionLimitSettings GetConnectionSettings(ConnectionLimitType limit)
        => SettingsMap.TryGetValue(limit, out var settings) ? settings : MediumSettings;

    #endregion Private Methods
}
