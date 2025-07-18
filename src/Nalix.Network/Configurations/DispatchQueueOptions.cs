using Nalix.Shared.Configuration.Attributes;
using Nalix.Shared.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Configuration options for MultiLevelQueue
/// </summary>
public sealed class DispatchQueueOptions : ConfigurationLoader
{
    /// <summary>
    /// Maximum number of packets in the queue (0 = unlimited)
    /// </summary>
    public System.Int32 MaxCapacity { get; set; } = 0;

    /// <summary>
    /// Collect detailed statistics
    /// </summary>
    public System.Boolean EnableMetrics { get; set; } = false;

    /// <summary>
    /// Check packet validity when dequeuing
    /// </summary>
    public System.Boolean EnableValidation { get; set; } = true;

    /// <summary>
    /// Maximum time a packet is allowed to exist in the queue
    /// </summary>
    public System.Int32 TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum time a packet is allowed to exist in the queue
    /// </summary>
    [ConfiguredIgnore]
    public System.TimeSpan Timeout { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchQueueOptions"/> class with default values.
    /// </summary>
    public DispatchQueueOptions() => this.Timeout = System.TimeSpan.FromSeconds(this.TimeoutSeconds);

    /// <remarks>
    /// Initializes a new instance of <see cref="DispatchQueueOptions"/>.
    /// </remarks>
    /// <param name="maxCapacity">Maximum number of packets in the queue (0 = unlimited).</param>
    /// <param name="enableValidation">Indicates whether packet validity should be checked when dequeuing.</param>
    /// <param name="enableMetrics">Indicates whether detailed statistics should be collected.</param>
    /// <param name="timeout">Maximum time a packet is allowed to exist in the queue.</param>
    public DispatchQueueOptions(
        System.Int32 maxCapacity = 0, System.TimeSpan? timeout = null,
        System.Boolean enableValidation = true, System.Boolean enableMetrics = false)
    {
        this.MaxCapacity = maxCapacity;
        this.EnableMetrics = enableMetrics;
        this.EnableValidation = enableValidation;
        this.Timeout = timeout ?? System.TimeSpan.FromSeconds(this.TimeoutSeconds);
    }
}