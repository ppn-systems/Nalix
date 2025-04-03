using Notio.Common.Logging;
using Notio.Network.Dispatcher.Options;

namespace Notio.Network.Dispatcher;

/// <summary>
/// Base class for packet dispatchers, providing configuration options.
/// </summary>
public abstract class PacketDispatcherBase<TPacket> where TPacket : class
{
    /// <summary>
    /// Gets the logger instance associated with this dispatcher.
    /// </summary>
    protected ILogger? Logger => Options.Logger;

    /// <summary>
    /// Gets the options object used to configure this instance.
    /// </summary>
    protected readonly PacketDispatcherOptions<TPacket> Options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatcherBase{TPacket}" /> class
    /// with the specified configuration options.
    /// </summary>
    /// <param name="options">An existing <see cref="PacketDispatcherOptions{TPacket}"/> instance.</param>
    protected PacketDispatcherBase(PacketDispatcherOptions<TPacket> options)
        => this.Options = options ?? throw new System.ArgumentNullException(nameof(options));

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatcherBase{TPacket}" /> class
    /// with an optional configuration action.
    /// </summary>
    /// <param name="configure">An action to configure the <see cref="PacketDispatcherOptions{TPacket}"/>.</param>
    protected PacketDispatcherBase(System.Action<PacketDispatcherOptions<TPacket>>? configure = null)
        : this(new PacketDispatcherOptions<TPacket>()) => configure?.Invoke(this.Options);
}
