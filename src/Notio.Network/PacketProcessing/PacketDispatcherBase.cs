using Notio.Common.Logging;
using Notio.Network.PacketProcessing.Options;

namespace Notio.Network.PacketProcessing;

/// <summary>
/// BaseValue36 class for packet dispatchers, providing configuration options.
/// </summary>
/// <remarks>
/// This abstract class serves as a foundation for packet dispatchers,
/// allowing customization through <see cref="PacketDispatcherOptions"/>.
/// </remarks>
public abstract class PacketDispatcherBase
{
    /// <summary>
    /// Gets the options object used to configure this instance.
    /// </summary>
    /// <remarks>
    /// The options object allows registering packet handlers and configuring logging.
    /// </remarks>
    protected PacketDispatcherOptions Options { get; } = new();

    /// <summary>
    /// Gets the logger instance associated with this dispatcher.
    /// </summary>
    /// <remarks>
    /// If logging is enabled, this property provides access to the configured logger.
    /// Otherwise, it may be <c>null</c>.
    /// </remarks>
    protected ILogger? Logger => Options.Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatcherBase" /> class
    /// with the specified configuration action.
    /// </summary>
    /// <param name="configure">
    /// An optional action to configure the <see cref="PacketDispatcherOptions"/> for the instance.
    /// </param>
    /// <remarks>
    /// If a configuration action is provided, it is applied to the <see cref="Options"/> object.
    /// </remarks>
    protected PacketDispatcherBase(System.Action<PacketDispatcherOptions>? configure)
    {
        // Apply the configuration options if provided
        configure?.Invoke(Options);
    }
}
