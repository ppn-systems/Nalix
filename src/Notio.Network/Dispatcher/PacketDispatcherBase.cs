namespace Notio.Network.Dispatcher;

/// <summary>
/// Base class for packet dispatchers, providing configuration options.
/// </summary>
public abstract class PacketDispatcherBase<TPacket>
    where TPacket : Common.Package.IPacket, Common.Package.IPacketDeserializer<TPacket>
{
    /// <summary>
    /// Gets the logger instance associated with this dispatcher.
    /// </summary>
    protected Common.Logging.ILogger? Logger => Options.Logger;

    /// <summary> 
    /// Gets the options object used to configure this instance.
    /// </summary>
    protected readonly Options.PacketDispatcherOptions<TPacket> Options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatcherBase{TPacket}" /> class
    /// with the specified configuration options.
    /// </summary>
    /// <param name="options">An existing <see cref="Options.PacketDispatcherOptions{TPacket}"/> instance.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    protected PacketDispatcherBase(Options.PacketDispatcherOptions<TPacket> options)
        => this.Options = options ?? throw new System.ArgumentNullException(nameof(options));

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatcherBase{TPacket}" /> class
    /// with an optional configuration action.
    /// </summary>
    /// <param name="configure">An action to configure the <see cref="Options.PacketDispatcherOptions{TPacket}"/>.</param>
    protected PacketDispatcherBase(System.Action<Options.PacketDispatcherOptions<TPacket>>? configure = null)
        : this(new Options.PacketDispatcherOptions<TPacket>()) => configure?.Invoke(this.Options);
}
