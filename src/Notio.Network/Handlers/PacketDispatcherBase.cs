using System;

namespace Notio.Network.Handlers;

/// <summary>
/// Base class for packet dispatchers, providing configuration options.
/// </summary>
public abstract class PacketDispatcherBase
{
    /// <summary>
    /// Gets the options object used to configure this instance.
    /// </summary>
    public PacketDispatcherOptions Options { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatcherBase" /> class with the specified configuration action.
    /// </summary>
    /// <param name="configure">
    /// An optional action to configure the <see cref="PacketDispatcherOptions"/> for the instance.
    /// </param>
    protected PacketDispatcherBase(Action<PacketDispatcherOptions>? configure)
    {
        // Apply the configuration options if provided
        configure?.Invoke(Options);
    }
}
