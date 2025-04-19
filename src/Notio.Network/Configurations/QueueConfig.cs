namespace Notio.Network.Configurations;

/// <summary>
/// Configuration options for PacketPriorityQueue
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="QueueConfig"/>.
/// </remarks>
/// <param name="maxCapacity">Maximum number of packets in the queue (0 = unlimited).</param>
/// <param name="enableValidation">Indicates whether packet validity should be checked when dequeuing.</param>
/// <param name="enableMetrics">Indicates whether detailed statistics should be collected.</param>
/// <param name="timeout">Maximum time a packet is allowed to exist in the queue.</param>
public sealed class QueueConfig(int maxCapacity = 0, System.TimeSpan? timeout = null,
    bool enableValidation = true, bool enableMetrics = false) : Shared.Configuration.ConfigurationBinder
{
    /// <summary>
    /// Maximum number of packets in the queue (0 = unlimited)
    /// </summary>
    public int MaxCapacity { get; set; } = maxCapacity;

    /// <summary>
    /// Check packet validity when dequeuing
    /// </summary>
    public bool EnableValidation { get; set; } = enableValidation;

    /// <summary>
    /// Collect detailed statistics
    /// </summary>
    public bool EnableMetrics { get; set; } = enableMetrics;

    /// <summary>
    /// Maximum time a packet is allowed to exist in the queue
    /// </summary>
    [Shared.Configuration.Attributes.ConfiguredIgnore]
    public System.TimeSpan Timeout { get; set; } = timeout ?? System.TimeSpan.FromSeconds(60);
}
