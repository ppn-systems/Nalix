using Nalix.Shared.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Configuration options for DispatchQueue
/// </summary>
public sealed class DispatchQueueConfig : ConfigurationBinder
{
    /// <summary>
    /// Maximum number of packets in the queue (0 = unlimited)
    /// </summary>
    public int MaxCapacity { get; set; }

    /// <summary>
    /// Collect detailed statistics
    /// </summary>
    public bool EnableMetrics { get; set; }

    /// <summary>
    /// Check packet validity when dequeuing
    /// </summary>
    public bool EnableValidation { get; set; }

    /// <summary>
    /// Maximum time a packet is allowed to exist in the queue
    /// </summary>
    internal int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum time a packet is allowed to exist in the queue
    /// </summary>
    [Shared.Configuration.Attributes.ConfiguredIgnore]
    public System.TimeSpan Timeout { get; set; }

    /// <remarks>
    /// Initializes a new instance of <see cref="DispatchQueueConfig"/>.
    /// </remarks>
    /// <param name="maxCapacity">Maximum number of packets in the queue (0 = unlimited).</param>
    /// <param name="enableValidation">Indicates whether packet validity should be checked when dequeuing.</param>
    /// <param name="enableMetrics">Indicates whether detailed statistics should be collected.</param>
    /// <param name="timeout">Maximum time a packet is allowed to exist in the queue.</param>
    public DispatchQueueConfig(
        int maxCapacity = 0, System.TimeSpan? timeout = null,
        bool enableValidation = true, bool enableMetrics = false)
    {
        this.MaxCapacity = maxCapacity;
        this.EnableMetrics = enableMetrics;
        this.EnableValidation = enableValidation;
        this.Timeout = timeout ?? System.TimeSpan.FromSeconds(this.TimeoutSeconds);
    }
}
